using System;
using UnityEngine;
using UnityEngine.UI;

namespace Naiwa.UI
{
    /// <summary>
    /// 图鉴格子（v1.0 §8.3）：Playable 悬停描边变黄 + 中央播放三角，点击请求播放；
    /// ViewOnly 悬停 / 点击都没有任何变化；Locked 显示黑色剪影 + 右下角锁，名字 ？？？。
    /// v2.0 重生表情：边框始终为红色；未解锁时整个方框被灰黑色覆盖、中间一个问号（不显示剪影），解锁后正常显示。
    /// </summary>
    public sealed class CollectionSlotView : MonoBehaviour
    {
        const float RevealSec = 0.6f;
        const float RebornOutlinePx = 2.5f;

        Image _bg;
        Sprite _normalBg, _rebornBg;
        Image _hoverFrame;
        Image _color;
        Image _silhouette;
        Image _lock;
        Image _play;
        Image _cover;
        Text _question;
        Text _name;
        UiClickable _click;
        SlotModel _model;
        float _revealT = -1f;
        bool _revealReborn;

        public string Id => _model.Id;
        public SlotState State => _model.State;
        public event Action<string> PlayRequested;

        public void Build(UiKit kit, int slotPx, Color silhouetteColor)
        {
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(slotPx, slotPx + 22);

            var outline = UiKit.Hex("#E2C48F", Color.gray);
            var fill = UiKit.Hex("#FFF9EC", Color.white);
            _normalBg = kit.Rounded(12, 1.5f, fill, outline);
            _rebornBg = kit.Rounded(12, RebornOutlinePx, fill, UiKit.Hex("#E53935", Color.red));
            _bg = kit.Image("Bg", transform, _normalBg, Color.white, raycast: true, sliced: true);
            UiKit.Place(_bg.rectTransform, 0, 11, slotPx, slotPx);
            _click = _bg.gameObject.AddComponent<UiClickable>();
            _click.Clicked = OnClicked;
            _click.HoverChanged = OnHover;

            float iconPx = slotPx - 14;
            _color = kit.Image("Icon", _bg.transform, null, Color.white);
            UiKit.Place(_color.rectTransform, 0, 0, iconPx, iconPx);
            _silhouette = kit.Image("Silhouette", _bg.transform, null, Color.white);
            _silhouette.material = kit.SilhouetteMaterial;
            UiKit.Place(_silhouette.rectTransform, 0, 0, iconPx, iconPx);
            _color.preserveAspect = _silhouette.preserveAspect = true;

            // 重生表情未解锁：灰黑色覆盖（内缩露出红色边框）+ 问号
            var coverColor = UiKit.Hex("#3A3634", Color.black);
            _cover = kit.Image("RebornCover", _bg.transform, kit.Rounded(10, 0, coverColor, coverColor), new Color(1, 1, 1, 0.94f), sliced: true);
            UiKit.Stretch(_cover.rectTransform, RebornOutlinePx, RebornOutlinePx, RebornOutlinePx, RebornOutlinePx);
            _question = kit.Text("Question", _cover.transform, "?", Mathf.RoundToInt(slotPx * 0.46f), UiKit.Hex("#D9D2CC", Color.white), bold: true);
            UiKit.Stretch(_question.rectTransform);
            _cover.enabled = _question.enabled = false;

            _hoverFrame = kit.Image("Hover", _bg.transform, kit.Rounded(12, 3f, new Color(1, 1, 1, 0), UiKit.Hex("#F2B92B", Color.yellow)), Color.white, sliced: true);
            UiKit.Stretch(_hoverFrame.rectTransform);
            _hoverFrame.enabled = false;

            _lock = kit.Image("Lock", _bg.transform, kit.Icons.Lock, Color.white);
            UiKit.Place(_lock.rectTransform, slotPx / 2f - 16, -slotPx / 2f + 16, 26, 26);
            _play = kit.Image("Play", _bg.transform, kit.Icons.Play, Color.white);
            UiKit.Place(_play.rectTransform, 0, 0, 40, 40);
            _play.enabled = false;

            _name = kit.Text("Name", transform, "", 13, UiKit.Hex("#6E4828", Color.black));
            UiKit.Place(_name.rectTransform, 0, -slotPx / 2f, slotPx + 10, 18);
        }

        bool IsReborn => _model.Def != null && _model.Def.IsReborn;

        public void Bind(SlotModel model, Sprite icon)
        {
            _model = model;
            _revealT = -1f;
            bool locked = model.State == SlotState.Locked;
            bool reborn = IsReborn;
            _bg.sprite = reborn ? _rebornBg : _normalBg;
            _color.sprite = icon;
            _silhouette.sprite = icon;
            _color.enabled = !locked && icon != null;
            _silhouette.enabled = locked && !reborn && icon != null;
            SetAlpha(_color, 1f);
            SetAlpha(_silhouette, 1f);
            _cover.enabled = _question.enabled = locked && reborn;
            SetAlpha(_cover, 0.94f);
            SetAlpha(_question, 1f);
            _lock.enabled = locked && !reborn;
            _lock.rectTransform.localScale = Vector3.one;
            _name.text = model.Name;
            ApplyHover(_click.Hovered);
        }

        /// <summary>
        /// 打开期间解锁：0.6s 从剪影渐变成彩色，锁缩小消失（Bind 新状态之后调用）。
        /// 重生表情：灰黑色覆盖与问号淡出、彩色图标淡入。
        /// </summary>
        public void PlayUnlockReveal()
        {
            _revealT = 0f;
            _revealReborn = IsReborn;
            _color.enabled = _color.sprite != null;
            SetAlpha(_color, 0f);
            if (_revealReborn)
            {
                _cover.enabled = _question.enabled = true;
                SetAlpha(_cover, 0.94f);
                SetAlpha(_question, 1f);
                return;
            }
            _silhouette.enabled = _silhouette.sprite != null;
            _lock.enabled = true;
            SetAlpha(_silhouette, 1f);
        }

        void Update()
        {
            if (_revealT < 0f) return;
            _revealT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_revealT / RevealSec);
            SetAlpha(_color, k);
            if (_revealReborn)
            {
                SetAlpha(_cover, 0.94f * (1f - k));
                SetAlpha(_question, 1f - k);
                _question.rectTransform.localScale = Vector3.one * (1f + 0.3f * UiKit.EaseOutCubic(k));
            }
            else
            {
                SetAlpha(_silhouette, 1f - k);
                _lock.rectTransform.localScale = Vector3.one * (1f - UiKit.EaseOutCubic(k));
            }
            if (k >= 1f)
            {
                _revealT = -1f;
                _silhouette.enabled = false;
                _lock.enabled = false;
                _lock.rectTransform.localScale = Vector3.one;
                _cover.enabled = _question.enabled = false;
                _question.rectTransform.localScale = Vector3.one;
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

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }
    }
}
