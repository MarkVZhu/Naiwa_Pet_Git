using UnityEngine;

namespace Naiwa.Fx
{
    /// <summary>运行时生成烟雾贴图：中心不透明、边缘按 smoothstep 衰减到 0 的白色圆形（非预乘，shader 里预乘）。</summary>
    public static class SoftCircleTexture
    {
        public static Texture2D Create(int size)
        {
            size = Mathf.Clamp(size, 8, 1024);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                name = "NaiwaSoftCircle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp01(1f - r);
                    float a = t * t * (3f - 2f * t); // smoothstep
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
