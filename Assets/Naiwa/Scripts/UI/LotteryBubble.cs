using System;
using Naiwa.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Naiwa.UI
{
    /// <summary>
    /// 抽奖气泡（v1.0 §5.5）：白底圆角对话框 + 转盘 + 消耗数字。
    /// 出现 0→1.1→1（0.25s）；每 ~2.5s 左右摇一次（±6°，0.4s）；悬停放大 1.08；
    /// 点击 → 由外部 TryDraw 成功后调用 PlaySpin：转盘减速转 N 圈 → 1→1.2→0 消失 → 回调揭晓。
    /// </summary>
    public sealed class LotteryBubble : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        enum Phase { Hidden, Appearing, Idle, Spinning, Popping }

        const float AppearSec = 0.25f;
        const float WobbleSec = 0.4f;
        const float WobbleDeg = 6f;
        const float PopSec = 0.2f;
        const float HoverScale = 1.08f;

        RectTransform _content;
        RectTransform _wheel;
        Image _bg;
        Image _tail;
        LotteryConfig _cfg;
        Phase _phase = Phase.Hidden;
        float _t;
        float _nextWobble;
        bool _hover;
        float _hoverScale = 1f;
        Action _onSpinDone;

        public event Action Clicked;

        public bool Visible => _phase != Phase.Hidden;
        public bool IsBusy => _phase == Phase.Spinning || _phase == Phase.Popping;

        public void Build(UiKit kit, LotteryConfig cfg, HudConfig hud)
        {
            _cfg = cfg;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(hud.bubbleWidthPx, hud.bubbleHeightPx);

            _content = UiKit.Rect("Content", transform);
            UiKit.Stretch(_content);

            var brown = UiKit.Hex("#6E4828", new Color(0.43f, 0.28f, 0.16f));
            _bg = kit.Image("Bg", _content, kit.Rounded(10, 2, Color.white, brown), Color.white, raycast: true, sliced: true);
            UiKit.Stretch(_bg.rectTransform);

            // 右侧指向点击量框的尖角（白色三角 + 棕色描边三角）
            var tailOutline = kit.Image("TailOutline", _content, kit.Icons.Triangle, brown);
            UiKit.Place(tailOutline.rectTransform, hud.bubbleWidthPx / 2f + 4f, -hud.bubbleHeightPx / 2f + 13f, 12, 14);
            _tail = kit.Image("Tail", tailOutline.rectTransform, kit.Icons.Triangle, Color.white);
            UiKit.Place(_tail.rectTransform, -2f, 0f, 8, 9);
            tailOutline.preserveAspect = _tail.preserveAspect = false;

            _wheel = kit.Image("Wheel", _content, kit.Icons.Wheel, Color.white).rectTransform;
            UiKit.Place(_wheel, 0, 10, 38, 38);
            var pointer = kit.Image("Pointer", _content, kit.Icons.WheelPointer, Color.white);
            UiKit.Place(pointer.rectTransform, 0, 10 + 19 + 2, 10, 10);

            var cost = kit.Text("Cost", _content, UiTextFormat.BubbleCost(cfg.cost), 12, brown, bold: true, number: true);
            UiKit.Place(cost.rectTransform, 0, -hud.bubbleHeightPx / 2f + 13f, hud.bubbleWidthPx, 16);

            gameObject.SetActive(false);
        }

        public void SetTailVisible(bool visible)
        {
            if (_tail != null) _tail.transform.parent.gameObject.SetActive(visible);
        }

        public void Show()
        {
            if (_phase != Phase.Hidden) return;
            gameObject.SetActive(true);
            _phase = Phase.Appearing;
            _t = 0f;
            _wheel.localRotation = Quaternion.identity;
            _content.localRotation = Quaternion.identity;
            _nextWobble = _cfg.bubbleWobbleIntervalSec;
            _content.localScale = Vector3.zero;
        }

        public void HideImmediate()
        {
            if (IsBusy) return;
            _phase = Phase.Hidden;
            _hover = false;
            gameObject.SetActive(false);
        }

        /// <summary>抽奖成功后播放转盘；结束后气泡消失并回调。</summary>
        public void PlaySpin(Action onDone)
        {
            gameObject.SetActive(true);
            _onSpinDone = onDone;
            _phase = Phase.Spinning;
            _t = 0f;
            _content.localRotation = Quaternion.identity;
        }

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) => _hover = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (_phase == Phase.Appearing || _phase == Phase.Idle) Clicked?.Invoke();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            _hoverScale = Mathf.MoveTowards(_hoverScale, _hover && !IsBusy ? HoverScale : 1f, dt * 1.2f);

            switch (_phase)
            {
                case Phase.Appearing:
                {
                    float k = _t / AppearSec;
                    float s = k < 0.6f ? Mathf.Lerp(0f, 1.1f, k / 0.6f) : Mathf.Lerp(1.1f, 1f, (k - 0.6f) / 0.4f);
                    _content.localScale = Vector3.one * (k >= 1f ? 1f : s) * _hoverScale;
                    if (k >= 1f) { _phase = Phase.Idle; _t = 0f; }
                    break;
                }
                case Phase.Idle:
                {
                    _content.localScale = Vector3.one * _hoverScale;
                    float w = _t - _nextWobble;
                    if (w >= 0f && w < WobbleSec)
                        _content.localRotation = Quaternion.Euler(0, 0, WobbleDeg * Mathf.Sin(w / WobbleSec * Mathf.PI * 2f));
                    else if (w >= WobbleSec)
                    {
                        _content.localRotation = Quaternion.identity;
                        _nextWobble = _t + _cfg.bubbleWobbleIntervalSec;
                    }
                    break;
                }
                case Phase.Spinning:
                {
                    float k = _t / Mathf.Max(0.05f, _cfg.spinSec);
                    _content.localScale = Vector3.one;
                    _wheel.localRotation = Quaternion.Euler(0, 0, -360f * _cfg.spinTurns * UiKit.EaseOutCubic(k));
                    if (k >= 1f) { _phase = Phase.Popping; _t = 0f; }
                    break;
                }
                case Phase.Popping:
                {
                    float k = _t / PopSec;
                    float s = k < 0.5f ? Mathf.Lerp(1f, 1.2f, k / 0.5f) : Mathf.Lerp(1.2f, 0f, (k - 0.5f) / 0.5f);
                    _content.localScale = Vector3.one * Mathf.Max(0f, s);
                    if (k >= 1f)
                    {
                        _phase = Phase.Hidden;
                        _hover = false;
                        gameObject.SetActive(false);
                        var cb = _onSpinDone;
                        _onSpinDone = null;
                        cb?.Invoke();
                    }
                    break;
                }
            }
        }
    }
}
