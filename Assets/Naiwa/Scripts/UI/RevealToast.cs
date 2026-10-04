using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Naiwa.UI
{
    /// <summary>
    /// 揭晓卡片（v1.0 §5.6）：220×84 白底圆角，左侧 64px 图标，右侧三行。
    /// 显示 duration 秒后 0.3s 淡出，点击立即关闭。也用于「新形态解锁了」提示。
    /// </summary>
    public sealed class RevealToast : MonoBehaviour, IPointerClickHandler
    {
        const float FadeSec = 0.3f;
        const float InSec = 0.15f;

        CanvasGroup _group;
        Image _icon;
        Text _line1, _line2, _line3;
        float _t, _duration;

        public bool Visible => gameObject.activeSelf;

        public void Build(UiKit kit, int width, int height)
        {
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(width, height);
            _group = gameObject.AddComponent<CanvasGroup>();

            var brown = UiKit.Hex("#6E4828", Color.black);
            var bg = kit.Image("Bg", transform, kit.Rounded(14, 2, Color.white, UiKit.Hex("#E2C48F", brown)), Color.white, raycast: true, sliced: true);
            UiKit.Stretch(bg.rectTransform);

            _icon = kit.Image("Icon", transform, null, Color.white);
            _icon.preserveAspect = true;
            UiKit.Place(_icon.rectTransform, -width / 2f + 10 + 32, 0, 64, 64);

            float textX = -width / 2f + 10 + 64 + 10;
            float textW = width - (10 + 64 + 10) - 10;
            _line1 = kit.Text("L1", transform, "", 15, brown, TextAnchor.MiddleLeft, bold: true);
            _line2 = kit.Text("L2", transform, "", 14, UiKit.Hex("#4A3220", brown), TextAnchor.MiddleLeft);
            _line3 = kit.Text("L3", transform, "", 11, UiKit.Hex("#9A7B5C", brown), TextAnchor.MiddleLeft);
            PlaceLine(_line1, textX, textW, 22);
            PlaceLine(_line2, textX, textW, 0);
            PlaceLine(_line3, textX, textW, -22);

            gameObject.SetActive(false);
        }

        static void PlaceLine(Text t, float x, float w, float y)
        {
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, 20);
        }

        public void Show(Sprite icon, string l1, string l2, string l3, float duration)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _line1.text = l1;
            _line2.text = l2;
            _line3.text = l3;
            _duration = Mathf.Max(0.5f, duration);
            _t = 0f;
            _group.alpha = 0f;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        public void OnPointerClick(PointerEventData e) => Hide();

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (_t < InSec) _group.alpha = _t / InSec;
            else if (_t < _duration) _group.alpha = 1f;
            else if (_t < _duration + FadeSec) _group.alpha = 1f - (_t - _duration) / FadeSec;
            else Hide();
        }
    }
}
