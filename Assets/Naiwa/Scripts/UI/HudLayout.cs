using UnityEngine;

namespace Naiwa.UI
{
    /// <summary>HUD 布局参数（像素，坐标原点在脚底，y 向上）。</summary>
    public struct HudLayoutParams
    {
        /// <summary>点击量框中心相对脚底的 y（负数在脚下方）。</summary>
        public float clicksCenterY;
        public float clicksWidth;
        public float clicksHeight;
        public float growthWidth;
        public float growthHeight;
        public float growthGap;
        public float bubbleWidth;
        public float bubbleHeight;
        public float bubbleGap;

        public static HudLayoutParams Default => new HudLayoutParams
        {
            clicksCenterY = -24, clicksWidth = 56, clicksHeight = 26,
            growthWidth = 120, growthHeight = 8, growthGap = 7,
            bubbleWidth = 58, bubbleHeight = 70, bubbleGap = 10,
        };
    }

    public struct HudLayoutResult
    {
        public Rect? Clicks;
        public Rect? Growth;
        public Rect? Bubble;
        /// <summary>气泡在点击量框左侧时为 true（显示指向框的尖角）。</summary>
        public bool BubbleBesideClicks;
    }

    /// <summary>
    /// HUD 布局（v1.0 §4.4），纯函数：
    /// 从上到下「点击量框 → 进度条」，隐藏的元素不占位；气泡在点击量框左侧 10px、底边对齐；
    /// 点击量框隐藏时气泡放到原框位置（底边对齐原框底边），进度条照常在其下方。
    /// </summary>
    public static class HudLayout
    {
        public static HudLayoutResult Compute(bool showClicks, bool showGrowth, bool bubbleVisible) =>
            Compute(showClicks, showGrowth, bubbleVisible, HudLayoutParams.Default);

        public static HudLayoutResult Compute(bool showClicks, bool showGrowth, bool bubbleVisible, HudLayoutParams p)
        {
            var r = new HudLayoutResult();
            var clicksRect = new Rect(-p.clicksWidth / 2f, p.clicksCenterY - p.clicksHeight / 2f, p.clicksWidth, p.clicksHeight);
            float clicksBottom = clicksRect.yMin;

            // 「上方元素」的底边：进度条放在它下方 growthGap
            float? upperBottom = null;

            if (showClicks)
            {
                r.Clicks = clicksRect;
                upperBottom = clicksBottom;
            }

            if (bubbleVisible)
            {
                if (showClicks)
                {
                    r.Bubble = new Rect(clicksRect.xMin - p.bubbleGap - p.bubbleWidth, clicksBottom, p.bubbleWidth, p.bubbleHeight);
                    r.BubbleBesideClicks = true;
                }
                else
                {
                    r.Bubble = new Rect(-p.bubbleWidth / 2f, clicksBottom, p.bubbleWidth, p.bubbleHeight);
                    upperBottom = clicksBottom;
                }
            }

            if (showGrowth)
            {
                float top;
                if (upperBottom.HasValue) top = upperBottom.Value - p.growthGap;
                else top = p.clicksCenterY + p.growthHeight / 2f; // 补位：垂直居中对齐原框中线
                r.Growth = new Rect(-p.growthWidth / 2f, top - p.growthHeight, p.growthWidth, p.growthHeight);
            }

            return r;
        }
    }
}
