using System;
using System.Collections.Generic;
using System.Globalization;
using Naiwa.Core;
using UnityEngine;

namespace Naiwa.Hud
{
    /// <summary>
    /// 桌宠脚下的计数框：圆角框（SpriteRenderer Sliced，Sprites-Default 预乘输出）+ 数字（自建字形网格，预乘 shader，C7）。
    /// 字形按 1 字体像素 = 1 屏幕像素排布，并对齐到整数像素，保证打包后 380×380 窗口里清晰。
    /// 不参与点击判定（框所在区域点击穿透）。
    /// </summary>
    public sealed class GrowthCounterView : MonoBehaviour
    {
        public SpriteRenderer box;
        public MeshFilter textFilter;
        public MeshRenderer textRenderer;
        /// <summary>使用 Naiwa/TextPremultiplied shader 的材质资产（由「搭建主场景」生成）。</summary>
        public Material textMaterial;

        const string PrewarmGlyphs = "0123456789,";

        CounterConfig _cfg;
        float _ppu;
        Font _font;
        Material _matInstance;
        Mesh _mesh;
        Texture2D _boxTexture;
        Color _textColor;
        bool _configured, _dirty, _visible = true;
        int _value = int.MinValue;
        bool _paused;

        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _tris = new List<int>();

        public bool Visible => _visible;

        public void Configure(CounterConfig cfg, float screenPxPerUnit)
        {
            _cfg = cfg;
            _ppu = Mathf.Max(1f, screenPxPerUnit);
            _textColor = CounterConfig.ParseColor(cfg.textColor, new Color(0.48f, 0.32f, 0.19f, 1f));

            transform.localPosition = new Vector3(0f, cfg.offsetYPx / _ppu, 0f);

            var fill = CounterConfig.ParseColor(cfg.fillColor, new Color(1f, 0.976f, 0.925f, 0.94f));
            var outline = CounterConfig.ParseColor(cfg.outlineColor, new Color(0.886f, 0.769f, 0.561f, 1f));
            if (_boxTexture != null) Destroy(_boxTexture);
            box.sprite = RoundedRectSprite.Create(cfg.cornerRadiusPx, cfg.outlinePx, fill, outline, _ppu, out _boxTexture);
            box.drawMode = SpriteDrawMode.Sliced;
            box.color = Color.white;
            box.sortingOrder = cfg.sortingOrder;

            _font = CreateFont(cfg);
            if (_font == null)
            {
                Debug.LogWarning("[Naiwa] 计数框找不到可用字体，数字不会显示");
            }
            else if (textMaterial == null)
            {
                Debug.LogWarning("[Naiwa] 计数框缺少文字材质（请重新执行 Naiwa/搭建主场景）");
            }
            else
            {
                if (_matInstance == null) _matInstance = new Material(textMaterial) { hideFlags = HideFlags.DontSave };
                textRenderer.sharedMaterial = _matInstance;
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "NaiwaCounterText", hideFlags = HideFlags.DontSave };
                _mesh.MarkDynamic();
            }
            textFilter.sharedMesh = _mesh;
            textRenderer.sortingOrder = cfg.sortingOrder + 1;

            _configured = true;
            _dirty = true;
            ApplyVisibility();
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            ApplyVisibility();
        }

        public void SetValue(int value, bool paused)
        {
            if (value == _value && paused == _paused) return;
            _value = value;
            _paused = paused;
            _dirty = true;
        }

        void OnEnable() => Font.textureRebuilt += OnFontTextureRebuilt;

        void OnDisable() => Font.textureRebuilt -= OnFontTextureRebuilt;

        void OnFontTextureRebuilt(Font font)
        {
            if (font == _font) _dirty = true;
        }

        void LateUpdate()
        {
            if (_configured && _dirty) Rebuild();
        }

        void ApplyVisibility()
        {
            if (box != null) box.enabled = _visible;
            if (textRenderer != null) textRenderer.enabled = _visible;
        }

        void Rebuild()
        {
            _dirty = false;
            string text = Math.Max(0, _value).ToString("N0", CultureInfo.InvariantCulture);
            float textWidthPx = 0f;

            _verts.Clear(); _uvs.Clear(); _colors.Clear(); _tris.Clear();

            if (_font != null && _matInstance != null)
            {
                int size = _cfg.fontPx;
                _font.RequestCharactersInTexture(PrewarmGlyphs + text, size, FontStyle.Normal);
                _matInstance.mainTexture = _font.material.mainTexture;

                float capMid = 0f;
                if (_font.GetCharacterInfo('0', out var zero, size, FontStyle.Normal))
                    capMid = (zero.minY + zero.maxY) * 0.5f;

                foreach (char ch in text)
                {
                    if (!_font.GetCharacterInfo(ch, out var ci, size, FontStyle.Normal)) continue;
                    textWidthPx += ci.advance;
                }

                var color = _textColor;
                if (_paused) color.a *= _cfg.pausedTextAlpha;

                float x = -Mathf.Round(textWidthPx * 0.5f);
                float yOff = -Mathf.Round(capMid);
                foreach (char ch in text)
                {
                    if (!_font.GetCharacterInfo(ch, out var ci, size, FontStyle.Normal)) continue;
                    int i0 = _verts.Count;
                    float x0 = (x + ci.minX) / _ppu, x1 = (x + ci.maxX) / _ppu;
                    float y0 = (yOff + ci.minY) / _ppu, y1 = (yOff + ci.maxY) / _ppu;
                    _verts.Add(new Vector3(x0, y0, 0f)); _uvs.Add(ci.uvBottomLeft);
                    _verts.Add(new Vector3(x0, y1, 0f)); _uvs.Add(ci.uvTopLeft);
                    _verts.Add(new Vector3(x1, y1, 0f)); _uvs.Add(ci.uvTopRight);
                    _verts.Add(new Vector3(x1, y0, 0f)); _uvs.Add(ci.uvBottomRight);
                    for (int k = 0; k < 4; k++) _colors.Add(color);
                    _tris.Add(i0); _tris.Add(i0 + 1); _tris.Add(i0 + 2);
                    _tris.Add(i0); _tris.Add(i0 + 2); _tris.Add(i0 + 3);
                    x += ci.advance;
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();

            int widthPx = Mathf.Max(_cfg.minWidthPx, Mathf.CeilToInt(textWidthPx) + _cfg.paddingXPx * 2);
            if ((widthPx & 1) == 1) widthPx++;
            int heightPx = _cfg.heightPx + (_cfg.heightPx & 1);
            box.size = new Vector2(widthPx / _ppu, heightPx / _ppu);
        }

        static Font CreateFont(CounterConfig cfg)
        {
            try
            {
                var os = Font.CreateDynamicFontFromOSFont(cfg.fontNames, cfg.fontPx);
                if (os != null)
                {
                    os.RequestCharactersInTexture(PrewarmGlyphs, cfg.fontPx, FontStyle.Normal);
                    if (os.GetCharacterInfo('0', out _, cfg.fontPx, FontStyle.Normal)) return os;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Naiwa] 加载系统字体失败，改用内置字体：{e.Message}");
            }

            try
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (Exception)
            {
                return null;
            }
        }

        void OnDestroy()
        {
            if (_boxTexture != null) Destroy(_boxTexture);
            if (_matInstance != null) Destroy(_matInstance);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
