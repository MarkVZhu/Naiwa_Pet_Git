using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Naiwa.UI
{
    /// <summary>
    /// 右键菜单「调整大小」打开的滑块面板：标题 + 当前百分比 + 关闭 ×；滑块（scaleMin~scaleMax，按步长吸附）；
    /// 底部两端标注最小/最大值，中间「恢复默认」。
    /// 拖动时只更新百分比，松手才提交（窗口缩放时面板本身也会移动，边拖边缩放会让滑块追着光标跑）。
    /// </summary>
    public sealed class SizePanel : MonoBehaviour
    {
        public const int Width = 240;
        public const int Height = 104;
        const int Pad = 14;
        const int HandlePx = 20;

        Slider _slider;
        Text _value;
        Text _reset;
        Func<float, float> _snap;
        float _committed = 1f;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>松手 / 点「恢复默认」时提交的新缩放。</summary>
        public event Action<float> Committed;
        public event Action CloseRequested;

        public void Build(UiKit kit, float min, float max, Func<float, float> snap)
        {
            _snap = snap ?? (v => v);
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(Width, Height);

            var brown = UiKit.Hex("#6E4828", Color.black);
            var light = UiKit.Hex("#9A7B5C", brown);
            var gold = UiKit.Hex("#E2C48F", brown);
            var bg = kit.Image("Bg", transform, kit.Rounded(14, 2, Color.white, gold), Color.white, raycast: true, sliced: true);
            UiKit.Stretch(bg.rectTransform);

            float top = Height / 2f - Pad;
            var title = kit.Text("Title", transform, "调整大小", 15, brown, TextAnchor.MiddleLeft, bold: true);
            PlaceLeft(title.rectTransform, -Width / 2f + Pad + 2, top - 12, 100, 24);
            _value = kit.Text("Value", transform, "100%", 15, brown, TextAnchor.MiddleRight, bold: true, number: true);
            PlaceRight(_value.rectTransform, Width / 2f - Pad - 30, top - 12, 70, 24);

            var closeBg = kit.Image("Close", transform, kit.Icons.Circle, gold, raycast: true);
            UiKit.Place(closeBg.rectTransform, Width / 2f - Pad - 11, top - 12, 22, 22);
            var closeX = kit.Image("X", closeBg.transform, kit.Icons.Close, Color.white);
            UiKit.Place(closeX.rectTransform, 0, 0, 18, 18);
            var closeClick = closeBg.gameObject.AddComponent<UiClickable>();
            closeClick.Clicked = () => CloseRequested?.Invoke();
            closeClick.HoverChanged = h => closeBg.color = h ? brown : gold;

            BuildSlider(kit, min, max, 2);

            float bottom = -Height / 2f + Pad + 8;
            var minLabel = kit.Text("Min", transform, Percent(min), 11, light, TextAnchor.MiddleLeft, number: true);
            PlaceLeft(minLabel.rectTransform, -Width / 2f + Pad, bottom, 50, 16);
            var maxLabel = kit.Text("Max", transform, Percent(max), 11, light, TextAnchor.MiddleRight, number: true);
            PlaceRight(maxLabel.rectTransform, Width / 2f - Pad, bottom, 50, 16);

            _reset = kit.Text("Reset", transform, "恢复默认", 12, light);
            _reset.raycastTarget = true;
            UiKit.Place(_reset.rectTransform, 0, bottom, 80, 20);
            var resetClick = _reset.gameObject.AddComponent<UiClickable>();
            resetClick.Clicked = () =>
            {
                _slider.SetValueWithoutNotify(1f);
                UpdateLabel(1f);
                Commit();
            };
            resetClick.HoverChanged = h => _reset.color = h ? brown : light;

            gameObject.SetActive(false);
        }

        void BuildSlider(UiKit kit, float min, float max, float y)
        {
            float w = Width - Pad * 2 - 6;
            var root = UiKit.Rect("Slider", transform);
            UiKit.Place(root, 0, y, w, 28);
            // 整行都是射线目标：点在轨道两侧的空白处也能直接跳到那个位置
            var hit = kit.Image("Hit", root, null, new Color(1, 1, 1, 0), raycast: true);
            UiKit.Stretch(hit.rectTransform);

            float trackH = 8f;
            var trackSprite = kit.Rounded(trackH / 2f, 0, Color.white, Color.white);
            var track = kit.Image("Track", root, trackSprite, UiKit.Hex("#ECDEBE", Color.gray), sliced: true);
            StretchX(track.rectTransform, 0, 0, trackH);

            var fillArea = UiKit.Rect("FillArea", root);
            StretchX(fillArea, HandlePx / 2f, HandlePx / 2f, trackH);
            var fill = kit.Image("Fill", fillArea, trackSprite, UiKit.Hex("#F2B92B", Color.yellow), sliced: true);
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(HandlePx, 0f);
            frt.anchoredPosition = Vector2.zero;

            var handleArea = UiKit.Rect("HandleArea", root);
            StretchX(handleArea, HandlePx / 2f, HandlePx / 2f, HandlePx);
            var brown = UiKit.Hex("#6E4828", Color.black);
            var handle = kit.Image("Handle", handleArea, kit.Icons.Circle, brown);
            var hrt = handle.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 0.5f);
            hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = new Vector2(HandlePx, HandlePx);
            hrt.anchoredPosition = Vector2.zero;
            var dot = kit.Image("Dot", handle.transform, kit.Icons.Circle, Color.white);
            UiKit.Place(dot.rectTransform, 0, 0, HandlePx - 8, HandlePx - 8);

            _slider = root.gameObject.AddComponent<Slider>();
            _slider.transition = Selectable.Transition.None;
            _slider.navigation = new Navigation { mode = Navigation.Mode.None };
            _slider.targetGraphic = handle;
            _slider.fillRect = frt;
            _slider.handleRect = hrt;
            _slider.direction = Slider.Direction.LeftToRight;
            _slider.minValue = min;
            _slider.maxValue = max;
            _slider.wholeNumbers = false;
            _slider.SetValueWithoutNotify(1f);
            _slider.onValueChanged.AddListener(OnValueChanged);

            // Slider 自己的 OnPointerUp 不对外通知，挂一个监听组件拿到「松手」
            root.gameObject.AddComponent<ReleaseListener>().Released = Commit;
        }

        public void Open(float current)
        {
            _committed = _snap(current);
            _slider.SetValueWithoutNotify(_committed);
            UpdateLabel(_committed);
            gameObject.SetActive(true);
        }

        public void Close() => gameObject.SetActive(false);

        void OnValueChanged(float v)
        {
            float snapped = _snap(v);
            if (!Mathf.Approximately(snapped, v)) _slider.SetValueWithoutNotify(snapped);
            UpdateLabel(snapped);
        }

        void Commit()
        {
            float v = _snap(_slider.value);
            if (Mathf.Approximately(v, _committed)) return;
            _committed = v;
            Committed?.Invoke(v);
        }

        void UpdateLabel(float v)
        {
            string s = Percent(v);
            if (_value.text != s) _value.text = s;
        }

        static string Percent(float v) => Mathf.RoundToInt(v * 100f).ToString(CultureInfo.InvariantCulture) + "%";

        static void StretchX(RectTransform rt, float left, float right, float height)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, -height / 2f);
            rt.offsetMax = new Vector2(-right, height / 2f);
        }

        static void PlaceLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void PlaceRight(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>挂在滑块上：滑块收到 pointerUp（点按或拖动结束）时通知面板提交。</summary>
        sealed class ReleaseListener : MonoBehaviour, IPointerUpHandler
        {
            public Action Released;
            public void OnPointerUp(PointerEventData e) => Released?.Invoke();
        }
    }
}
