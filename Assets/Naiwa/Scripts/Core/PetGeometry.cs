using UnityEngine;

namespace Naiwa.Core
{
    /// <summary>素材几何约定（§V.1）：600×600 画布，脚底中心约在 (300, 580)，PPU=100。</summary>
    public static class PetGeometry
    {
        public const int CanvasPx = 600;
        public const int FeetFromBottomPx = 20;
        public const float PixelsPerUnit = 100f;

        public static readonly Vector2 Pivot = new Vector2(0.5f, FeetFromBottomPx / (float)CanvasPx);

        public static float CanvasUnits => CanvasPx / PixelsPerUnit;

        /// <summary>画布中心相对脚底的高度（世界单位），相机中心对准这里。</summary>
        public static float CanvasCenterY => (0.5f - Pivot.y) * CanvasUnits;
    }
}
