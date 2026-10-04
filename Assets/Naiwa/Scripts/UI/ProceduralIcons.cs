using System;
using System.IO;
using UnityEngine;

namespace Naiwa.UI
{
    /// <summary>
    /// 程序生成的 UI 图标（v1.0 §7.5），无需美术素材。若 StreamingAssets/content/ui/&lt;name&gt;.png 存在则用 PNG 覆盖。
    /// 贴图为普通 alpha（透明像素保留邻近颜色防止黑边），由 UIPremultiplied shader 预乘输出。
    /// </summary>
    public sealed class ProceduralIcons
    {
        public Sprite Wheel { get; private set; }
        public Sprite WheelPointer { get; private set; }
        public Sprite Lock { get; private set; }
        public Sprite Play { get; private set; }
        public Sprite Triangle { get; private set; }
        public Sprite Circle { get; private set; }
        public Sprite Close { get; private set; }

        static readonly Color Brown = Hex("#6E4828");
        static readonly Color Red = Hex("#E53935");
        static readonly Color Yellow = Hex("#FFC107");

        public ProceduralIcons(string uiOverrideDir)
        {
            Wheel = LoadOverride(uiOverrideDir, "wheel") ?? MakeSprite(DrawWheel(128), "wheel");
            WheelPointer = MakeSprite(DrawWheelPointer(32), "wheel_pointer");
            Lock = LoadOverride(uiOverrideDir, "lock") ?? MakeSprite(DrawLock(64), "lock");
            Play = LoadOverride(uiOverrideDir, "play") ?? MakeSprite(DrawPlay(64), "play");
            Triangle = MakeSprite(DrawTriangleRight(32), "triangle");
            Circle = MakeSprite(DrawCircle(64), "circle");
            Close = MakeSprite(DrawClose(48), "close");
        }

        // ---------- 绘制 ----------

        delegate Color PixelFn(float x, float y); // x, y ∈ [0,1]，y 向上

        static Texture2D Render(int size, PixelFn fn, int supersample = 4)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color[size * size];
            float inv = 1f / (size * supersample);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                Color lastOpaque = Color.clear;
                for (int sy = 0; sy < supersample; sy++)
                for (int sx = 0; sx < supersample; sx++)
                {
                    var c = fn((x * supersample + sx + 0.5f) * inv, (y * supersample + sy + 0.5f) * inv);
                    r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                    if (c.a > 0f) lastOpaque = c;
                }
                int n = supersample * supersample;
                Color o = a > 0f ? new Color(r / a, g / a, b / a, a / n) : new Color(lastOpaque.r, lastOpaque.g, lastOpaque.b, 0f);
                px[y * size + x] = o;
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        static Texture2D DrawWheel(int size) => Render(size, (x, y) =>
        {
            float dx = x - 0.5f, dy = y - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > 0.48f) return Color.clear;
            if (d > 0.42f) return Brown;
            if (d < 0.07f) return Brown;
            float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            if (ang < 0) ang += 360f;
            int seg = Mathf.FloorToInt(ang / 60f) % 6;
            switch (seg % 3)
            {
                case 0: return Red;
                case 1: return Color.white;
                default: return Yellow;
            }
        });

        static Texture2D DrawWheelPointer(int size) => Render(size, (x, y) =>
        {
            // 朝下的三角
            float halfW = 0.42f * y;
            if (y < 0.08f || y > 0.95f) return Color.clear;
            return Mathf.Abs(x - 0.5f) <= halfW ? Brown : Color.clear;
        });

        static Texture2D DrawLock(int size) => Render(size, (x, y) =>
        {
            var body = Hex("#FFC107");
            // 锁身：圆角矩形 x[0.18,0.82] y[0.08,0.56]
            if (InRoundRect(x, y, 0.18f, 0.08f, 0.82f, 0.56f, 0.08f))
            {
                // 锁孔
                float kx = x - 0.5f, ky = y - 0.36f;
                if (kx * kx + ky * ky < 0.006f || (Mathf.Abs(kx) < 0.035f && y > 0.2f && y < 0.36f)) return Brown;
                return InRoundRect(x, y, 0.22f, 0.12f, 0.78f, 0.52f, 0.06f) ? body : Brown;
            }
            // 锁梁：U 形
            float ux = x - 0.5f, uy = y - 0.6f;
            float ring = Mathf.Sqrt(ux * ux + Mathf.Max(0f, uy) * Mathf.Max(0f, uy));
            if (y >= 0.5f && ring > 0.17f && ring < 0.26f && (uy > 0f || Mathf.Abs(ux) > 0.17f)) return Brown;
            return Color.clear;
        });

        static Texture2D DrawPlay(int size) => Render(size, (x, y) =>
        {
            float dx = x - 0.5f, dy = y - 0.5f;
            if (dx * dx + dy * dy > 0.235f) return Color.clear;
            // 白色三角（朝右）
            float tx = x - 0.38f;
            if (tx >= 0f && tx <= 0.32f && Mathf.Abs(y - 0.5f) <= (0.32f - tx) * 0.62f) return Color.white;
            return new Color(0f, 0f, 0f, 0.55f);
        });

        static Texture2D DrawTriangleRight(int size) => Render(size, (x, y) =>
            Mathf.Abs(y - 0.5f) <= (1f - x) * 0.5f ? Color.white : Color.clear);

        static Texture2D DrawCircle(int size) => Render(size, (x, y) =>
        {
            float dx = x - 0.5f, dy = y - 0.5f;
            return dx * dx + dy * dy <= 0.25f ? Color.white : Color.clear;
        });

        static Texture2D DrawClose(int size) => Render(size, (x, y) =>
        {
            float a = Mathf.Abs((x - 0.5f) - (y - 0.5f)) / 1.4142f;
            float b = Mathf.Abs((x - 0.5f) + (y - 0.5f)) / 1.4142f;
            bool inBox = Mathf.Abs(x - 0.5f) < 0.3f && Mathf.Abs(y - 0.5f) < 0.3f;
            return inBox && (a < 0.06f || b < 0.06f) ? Color.white : Color.clear;
        });

        static bool InRoundRect(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            if (x < x0 || x > x1 || y < y0 || y > y1) return false;
            float cx = Mathf.Clamp(x, x0 + r, x1 - r), cy = Mathf.Clamp(y, y0 + r, y1 - r);
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        // ---------- 工具 ----------

        static Sprite MakeSprite(Texture2D tex, string name)
        {
            tex.name = "NaiwaIcon_" + name;
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = name;
            return s;
        }

        static Sprite LoadOverride(string dir, string name)
        {
            if (string.IsNullOrEmpty(dir)) return null;
            string path = Path.Combine(dir, name + ".png");
            try
            {
                if (!File.Exists(path)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                if (!tex.LoadImage(File.ReadAllBytes(path), true)) return null;
                return MakeSprite(tex, name);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Naiwa] 读取 UI 图标 {path} 失败，改用程序生成：{e.Message}");
                return null;
            }
        }

        public static Color Hex(string html) => ColorUtility.TryParseHtmlString(html, out var c) ? c : Color.magenta;
    }
}
