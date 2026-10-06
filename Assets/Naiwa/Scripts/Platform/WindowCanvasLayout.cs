using Naiwa.Core;
using UnityEngine;

namespace Naiwa.Platform
{
    public enum PanelSide { Right, Left }

    /// <summary>
    /// 固定大画布（v1.0 §7.3）：左侧区 | 宠物区 | 右侧区，宠物区下方是 HUD 区。
    /// 所有矩形均为桌面像素坐标（y 向下），相对窗口左上角。纯函数，可测试。
    /// scale = 全局缩放：窗口整体按比例放大/缩小，相机与 Canvas 的参考尺寸不变，所以宠物、HUD、图鉴一起缩放。
    /// </summary>
    public sealed class WindowCanvasLayout
    {
        public readonly float Scale;
        public readonly int Width;
        public readonly int Height;
        public readonly int SideWidth;
        public readonly int PetArea;
        public readonly int HudHeight;
        /// <summary>脚底相对窗口左上角的位置（桌面像素）。缩放前后保持脚底在桌面上不动。</summary>
        public readonly Vector2 Feet;

        public WindowCanvasLayout(WindowConfig cfg, float scale = 1f)
        {
            Scale = scale > 0f ? scale : 1f;
            Width = Mathf.Max(1, Mathf.RoundToInt(cfg.WindowWidthPx * Scale));
            Height = Mathf.Max(1, Mathf.RoundToInt(cfg.WindowHeightPx * Scale));
            SideWidth = Mathf.RoundToInt(cfg.sideWidthPx * Scale);
            PetArea = Width - SideWidth * 2;
            HudHeight = Mathf.RoundToInt(cfg.hudHeightPx * Scale);
            // 实际渲染比例由窗口高度决定（相机正交尺寸、Canvas 按高度匹配）
            float k = Height / (float)cfg.WindowHeightPx;
            Feet = new Vector2(Width / 2f, cfg.FeetFromTopPx * k);
        }

        /// <summary>换缩放后的窗口位置：脚底在桌面上的位置不变。</summary>
        public Vector2Int RescaledPosition(Vector2Int windowPos, WindowCanvasLayout from)
        {
            float feetX = windowPos.x + from.Feet.x;
            float feetY = windowPos.y + from.Feet.y;
            return new Vector2Int(Mathf.RoundToInt(feetX - Feet.x), Mathf.RoundToInt(feetY - Feet.y));
        }

        /// <summary>宠物区 + HUD 区（窗口中间一列），受工作区约束。</summary>
        public RectInt CoreRect => new RectInt(SideWidth, 0, PetArea, Height);
        public RectInt LeftSideRect => new RectInt(0, 0, SideWidth, Height);
        public RectInt RightSideRect => new RectInt(SideWidth + PetArea, 0, SideWidth, Height);

        public RectInt ToDesktop(RectInt local, Vector2Int windowPos) =>
            new RectInt(local.x + windowPos.x, local.y + windowPos.y, local.width, local.height);

        /// <summary>
        /// 选边：优先右侧；右侧可见面积 &lt; 100% 而左侧 = 100% 时放左侧；两边都不完整时选可见面积大的一侧。
        /// </summary>
        public PanelSide ChooseSide(Vector2Int windowPos, RectInt workArea)
        {
            float right = VisibleFraction(ToDesktop(RightSideRect, windowPos), workArea);
            float left = VisibleFraction(ToDesktop(LeftSideRect, windowPos), workArea);
            if (right >= 0.999f) return PanelSide.Right;
            if (left >= 0.999f) return PanelSide.Left;
            return left > right ? PanelSide.Left : PanelSide.Right;
        }

        /// <summary>首次启动：宠物区 + HUD 区放在工作区右下角。</summary>
        public Vector2Int DefaultWindowPos(RectInt workArea) =>
            new Vector2Int(workArea.xMax - SideWidth - PetArea, workArea.yMax - Height);

        /// <summary>松手后限制：宠物区 + HUD 区至少 minVisible 比例留在工作区内（每个方向）；侧区允许伸出屏幕。</summary>
        public Vector2Int ClampWindowPos(Vector2Int windowPos, RectInt workArea, float minVisible)
        {
            var core = CoreRect;
            int keepX = Mathf.RoundToInt(core.width * Mathf.Clamp01(minVisible));
            int keepY = Mathf.RoundToInt(core.height * Mathf.Clamp01(minVisible));
            int coreX = windowPos.x + core.x;
            int coreY = windowPos.y + core.y;
            coreX = Mathf.Clamp(coreX, workArea.xMin - (core.width - keepX), workArea.xMax - keepX);
            coreY = Mathf.Clamp(coreY, workArea.yMin - (core.height - keepY), workArea.yMax - keepY);
            return new Vector2Int(coreX - core.x, coreY - core.y);
        }

        public static float VisibleFraction(RectInt r, RectInt area)
        {
            if (r.width <= 0 || r.height <= 0) return 0f;
            int x0 = Mathf.Max(r.xMin, area.xMin), x1 = Mathf.Min(r.xMax, area.xMax);
            int y0 = Mathf.Max(r.yMin, area.yMin), y1 = Mathf.Min(r.yMax, area.yMax);
            if (x1 <= x0 || y1 <= y0) return 0f;
            return (x1 - x0) * (float)(y1 - y0) / (r.width * (float)r.height);
        }
    }
}
