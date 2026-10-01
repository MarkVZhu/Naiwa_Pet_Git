using UnityEngine;

namespace Naiwa.Hud
{
    /// <summary>运行时生成带描边的圆角矩形 Sprite（九宫格，配合 SpriteRenderer Sliced 使用）。</summary>
    public static class RoundedRectSprite
    {
        const int RadiusTex = 32;
        const int Margin = 2;

        /// <param name="cornerRadiusPx">屏幕上的圆角半径（像素）。</param>
        /// <param name="outlinePx">屏幕上的描边宽度（像素）。</param>
        /// <param name="screenPxPerUnit">1 个世界单位对应的屏幕像素。</param>
        public static Sprite Create(float cornerRadiusPx, float outlinePx, Color fill, Color outline,
            float screenPxPerUnit, out Texture2D texture)
        {
            cornerRadiusPx = Mathf.Max(1f, cornerRadiusPx);
            int size = (RadiusTex + Margin) * 2 + 2;
            float outlineTex = Mathf.Max(0f, outlinePx) * RadiusTex / cornerRadiusPx;

            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "NaiwaRoundedRect",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color[size * size];
            float half = size * 0.5f;
            float innerHalf = half - Margin - RadiusTex;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - innerHalf;
                    float qy = Mathf.Abs(y + 0.5f - half) - innerHalf;
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - RadiusTex; // <0 在内部

                    float coverage = Mathf.Clamp01(0.5f - d);
                    float inner = outlineTex > 0f ? Mathf.Clamp01(0.5f - (d + outlineTex)) : 1f;
                    // 透明像素也保留描边色，避免双线性过滤时边缘发黑（Sprites-Default 会在 shader 里预乘）
                    Color c = Color.Lerp(outline, fill, inner);
                    c.a = coverage * Mathf.Lerp(outline.a, fill.a, inner);
                    pixels[y * size + x] = c;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            float ppu = RadiusTex / (cornerRadiusPx / screenPxPerUnit);
            float border = RadiusTex + Margin;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
