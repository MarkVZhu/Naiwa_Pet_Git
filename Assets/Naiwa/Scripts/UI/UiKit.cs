using System;
using System.Collections.Generic;
using Naiwa.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace Naiwa.UI
{
    /// <summary>运行时搭建 uGUI 的小工具：统一使用预乘 alpha 材质（C12）、系统字体、程序生成的圆角 Sprite。</summary>
    public sealed class UiKit
    {
        public readonly Material Material;
        public readonly Material SilhouetteMaterial;
        public readonly Font TextFont;
        public readonly Font NumberFont;
        public readonly ProceduralIcons Icons;

        readonly Dictionary<string, Sprite> _rounded = new Dictionary<string, Sprite>();
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();

        public UiKit(Material material, Material silhouette, string[] textFonts, string[] numberFonts, ProceduralIcons icons)
        {
            Material = material;
            SilhouetteMaterial = silhouette;
            TextFont = CreateFont(textFonts);
            NumberFont = CreateFont(numberFonts) ?? TextFont;
            Icons = icons;
        }

        static Font CreateFont(string[] names)
        {
            try
            {
                if (names != null && names.Length > 0)
                {
                    var f = Font.CreateDynamicFontFromOSFont(names, 16);
                    if (f != null) return f;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Naiwa] 加载系统字体失败，改用内置字体：{e.Message}");
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>九宫格圆角矩形（canvas 参考 PPU = 100，所以 Sprite PPU 按圆角像素换算）。</summary>
        public Sprite Rounded(float radiusPx, float outlinePx, Color fill, Color outline)
        {
            string key = $"{radiusPx:0.##}|{outlinePx:0.##}|{ColorUtility.ToHtmlStringRGBA(fill)}|{ColorUtility.ToHtmlStringRGBA(outline)}";
            if (_rounded.TryGetValue(key, out var s)) return s;
            s = RoundedRectSprite.Create(radiusPx, outlinePx, fill, outline, 100f, out var tex);
            _owned.Add(tex);
            _rounded[key] = s;
            return s;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        /// <summary>以父节点中心为原点放置（px，y 向上）。</summary>
        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public Image Image(string name, Transform parent, Sprite sprite, Color color, bool raycast = false, bool sliced = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.material = Material;
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.preserveAspect = !sliced && sprite != null;
            return img;
        }

        public Text Text(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter,
            bool bold = false, bool number = false)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.material = Material;
            t.font = number ? NumberFont : TextFont;
            t.fontSize = size;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = false;
            t.text = text;
            return t;
        }

        public void Dispose()
        {
            foreach (var o in _owned) if (o != null) UnityEngine.Object.Destroy(o);
            _owned.Clear();
            _rounded.Clear();
        }

        public static Color Hex(string html, Color fallback) => ColorUtility.TryParseHtmlString(html, out var c) ? c : fallback;

        public static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        public static float EaseOutBack(float t) { t = Mathf.Clamp01(t); const float c1 = 1.70158f, c3 = c1 + 1f; float u = t - 1f; return 1f + c3 * u * u * u + c1 * u * u; }
    }
}
