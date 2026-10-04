using Naiwa.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Naiwa.Pet
{
    /// <summary>
    /// 头顶「新！」标记（v1.0 §6.3）：白底红字圆角标签，位于头顶上方 8px，上下浮动 ±3px（周期 1s），
    /// 显示 newBadgeSec 秒后 0.3s 淡出。位置由 HudController 每帧给出。
    /// </summary>
    public sealed class NewBadge : MonoBehaviour
    {
        const float FadeSec = 0.3f;
        const float BobPx = 3f;
        const float GapPx = 8f;

        CanvasGroup _group;
        RectTransform _rt;
        float _t, _duration;
        float _headTopY;

        public bool Visible => gameObject.activeSelf;

        public void Build(UiKit kit)
        {
            _rt = (RectTransform)transform;
            _rt.sizeDelta = new Vector2(40, 22);
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            var red = UiKit.Hex("#E53935", Color.red);
            var bg = kit.Image("Bg", transform, kit.Rounded(8, 1.5f, Color.white, red), Color.white, sliced: true);
            UiKit.Stretch(bg.rectTransform);
            var text = kit.Text("Text", transform, "新！", 14, red, bold: true);
            UiKit.Stretch(text.rectTransform);
            gameObject.SetActive(false);
        }

        /// <summary>headTopY：头顶在父节点坐标中的 y（像素）。</summary>
        public void SetHeadTop(float headTopY) => _headTopY = headTopY;

        public void Show(float duration)
        {
            _duration = Mathf.Max(0.1f, duration);
            _t = 0f;
            _group.alpha = 1f;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float bob = Mathf.Sin(_t * Mathf.PI * 2f) * BobPx;
            _rt.anchoredPosition = new Vector2(0f, _headTopY + GapPx + _rt.sizeDelta.y / 2f + bob);
            if (_t > _duration) _group.alpha = 1f - (_t - _duration) / FadeSec;
            if (_t > _duration + FadeSec) Hide();
        }
    }
}
