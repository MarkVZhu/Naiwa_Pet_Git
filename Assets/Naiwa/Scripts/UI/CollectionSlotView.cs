using System;
using UnityEngine;
using UnityEngine.UI;

namespace Naiwa.UI
{
    /// <summary>
    /// 图鉴格子（v1.0 §8.3）：Playable 悬停描边变黄 + 中央播放三角，点击请求播放；
    /// ViewOnly 悬停 / 点击都没有任何变化；Locked 显示黑色剪影 + 右下角锁，名字 ？？？。
    /// </summary>
    public sealed class CollectionSlotView : MonoBehaviour
    {
        const float RevealSec = 0.6f;

        Image _hoverFrame;
        Image _color;
        Image _silhouette;
        Image _lock;
        Image _play;
        Text _name;
        UiClickable _click;
        SlotModel _model;
        float _revealT = -1f;

        public string Id => _model.Id;
        public SlotState State => _model.State;
        public event Action<string> PlayRequested;

        public void Build(UiKit kit, int slotPx, Color silhouetteColor)
        {
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(slotPx, slotPx + 22);

            var outline = UiKit.Hex("#E2C48F", Color.gray);
            var bg = kit.Image("Bg", transform, kit.Rounded(12, 1.5f, UiKit.Hex("#FFF9EC", Color.white), outline), Color.white, raycast: true, sliced: true);
            UiKit.Place(bg.rectTransform, 0, 11, slotPx, slotPx);
            _click = bg.gameObject.AddComponent<UiClickable>();
            _click.Clicked = OnClicked;
            _click.HoverChanged = OnHover;

            _hoverFrame = kit.Image("Hover", bg.transform, kit.Rounded(12, 3f, new Color(1, 1, 1, 0), UiKit.Hex("#F2B92B", Color.yellow)), Color.white, sliced: true);
            UiKit.Stretch(_hoverFrame.rectTransform);
            _hoverFrame.enabled = false;

            float iconPx = slotPx - 14;
            _color = kit.Image("Icon", bg.transform, null, Color.white);
            UiKit.Place(_color.rectTransform, 0, 0, iconPx, iconPx);
            _silhouette = kit.Image("Silhouette", bg.transform, null, Color.white);
            _silhouette.material = kit.SilhouetteMaterial;
            UiKit.Place(_silhouette.rectTransform, 0, 0, iconPx, iconPx);
            _color.preserveAspect = _silhouette.preserveAspect = true;

            _lock = kit.Image("Lock", bg.transform, kit.Icons.Lock, Color.white);
            UiKit.Place(_lock.rectTransform, slotPx / 2f - 16, -slotPx / 2f + 16, 26, 26);
            _play = kit.Image("Play", bg.transform, kit.Icons.Play, Color.white);
            UiKit.Place(_play.rectTransform, 0, 0, 40, 40);
            _play.enabled = false;

            _name = kit.Text("Name", transform, "", 13, UiKit.Hex("#6E4828", Color.black));
            UiKit.Place(_name.rectTransform, 0, -slotPx / 2f, slotPx + 10, 18);
        }

        public void Bind(SlotModel model, Sprite icon)
        {
            _model = model;
            _revealT = -1f;
            bool locked = model.State == SlotState.Locked;
            _color.sprite = icon;
            _silhouette.sprite = icon;
            _color.enabled = !locked && icon != null;
            _silhouette.enabled = locked && icon != null;
            SetAlpha(_color, 1f);
            SetAlpha(_silhouette, 1f);
            _lock.enabled = locked;
            _lock.rectTransform.localScale = Vector3.one;
            _name.text = model.Name;
            ApplyHover(_click.Hovered);
        }

        /// <summary>打开期间抽中：0.6s 从剪影渐变成彩色，锁缩小消失（Bind 新状态之后调用）。</summary>
        public void PlayUnlockReveal()
        {
            _revealT = 0f;
            _color.enabled = _color.sprite != null;
            _silhouette.enabled = _silhouette.sprite != null;
            _lock.enabled = true;
            SetAlpha(_color, 0f);
            SetAlpha(_silhouette, 1f);
        }

        void Update()
        {
            if (_revealT < 0f) return;
            _revealT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_revealT / RevealSec);
            SetAlpha(_color, k);
            SetAlpha(_silhouette, 1f - k);
            _lock.rectTransform.localScale = Vector3.one * (1f - UiKit.EaseOutCubic(k));
            if (k >= 1f)
            {
                _revealT = -1f;
                _silhouette.enabled = false;
                _lock.enabled = false;
                _lock.rectTransform.localScale = Vector3.one;
            }
        }

        void OnHover(bool hovered) => ApplyHover(hovered);

        void ApplyHover(bool hovered)
        {
            bool show = hovered && _model.State == SlotState.Playable;
            _hoverFrame.enabled = show;
            _play.enabled = show;
        }

        void OnClicked()
        {
            if (_model.State == SlotState.Playable) PlayRequested?.Invoke(_model.Id);
        }

        static void SetAlpha(Image img, float a)
        {
            var c = img.color;
            c.a = a;
            img.color = c;
        }
    }
}
