using System.Globalization;
using Naiwa.Content;
using Naiwa.Core;
using Naiwa.Growth;
using Naiwa.Pet;
using Naiwa.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace Naiwa.UI
{
    /// <summary>
    /// 运行时搭建并驱动全部 UI（v1.0 §4.3–4.4、§5.5–5.6、§6.3、§8.2）：
    /// 点击量框（扣费滚动 + 飘字）、成长进度条、抽奖气泡、「新！」、揭晓卡片、图鉴面板。
    /// Canvas 为 Screen Space Overlay，参考分辨率 = 窗口尺寸（打包后 1 UI 单位 = 1 像素）。
    /// 坐标：Root = 窗口（原点在窗口中心），PetAnchor = 脚底。
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        public Material uiMaterial;
        public Material silhouetteMaterial;

        GameConfig _cfg;
        UiKit _kit;
        RectTransform _root;
        RectTransform _pet;

        // 点击量框
        RectTransform _clicksRt;
        CanvasGroup _clicksGroup;
        Image _clicksBox;
        Text _clicksText;
        Text _floatText;
        RectTransform _floatRt;
        Color _clicksTextColor;
        long _clicksTarget = -1;
        double _clicksShown;
        long _rollFrom, _rollTo;
        float _rollT = -1f, _floatT = -1f;
        bool _paused;

        // 进度条
        RectTransform _growthRt;
        CanvasGroup _growthGroup;
        Image _growthFill;
        Color _fillColor, _maxColor;
        float _growthTarget, _growthShown;
        bool _growthMax;

        // 开关与布局
        bool _showClicks = true, _showGrowth = true;
        bool _bubbleWanted;
        float _clicksWidth = 56f;

        public LotteryBubble Bubble { get; private set; }
        public RevealToast Toast { get; private set; }
        public NewBadge Badge { get; private set; }
        public CollectionPanel Collection { get; private set; }
        public UiKit Kit => _kit;

        public void Initialize(GameConfig cfg, EmoteCatalog catalog, IUnlockService unlocks, IconLibrary icons,
            System.Func<FormId> displayForm)
        {
            _cfg = cfg;
            var win = cfg.window;
            var hud = cfg.hud;

            if (silhouetteMaterial != null)
            {
                silhouetteMaterial = new Material(silhouetteMaterial) { hideFlags = HideFlags.DontSave };
                silhouetteMaterial.SetColor("_SilhouetteColor", UiKit.Hex(cfg.collection.silhouetteColor, Color.black));
            }
            if (uiMaterial == null) Debug.LogWarning("[Naiwa] HUD 缺少 UIPremultiplied 材质（请重新执行 Naiwa/搭建主场景）");

            var icons2d = new ProceduralIcons(System.IO.Path.Combine(Application.streamingAssetsPath, "content", "ui"));
            _kit = new UiKit(uiMaterial, silhouetteMaterial, hud.fontNames, cfg.counter.fontNames, icons2d);

            // Canvas
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvas.pixelPerfect = false;
            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(win.WindowWidthPx, win.WindowHeightPx);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 100f;
            if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            _root = UiKit.Rect("Window", transform);
            UiKit.Place(_root, 0, 0, win.WindowWidthPx, win.WindowHeightPx);
            _pet = UiKit.Rect("PetAnchor", _root);
            var feet = win.FeetOffsetFromCenterPx;
            UiKit.Place(_pet, feet.x, feet.y, 0, 0);

            BuildClicks();
            BuildGrowth();

            Bubble = UiKit.Rect("LotteryBubble", _pet).gameObject.AddComponent<LotteryBubble>();
            Bubble.Build(_kit, cfg.lottery, hud);

            Badge = UiKit.Rect("NewBadge", _pet).gameObject.AddComponent<NewBadge>();
            Badge.Build(_kit);

            Toast = UiKit.Rect("RevealToast", _root).gameObject.AddComponent<RevealToast>();
            Toast.Build(_kit, hud.toastWidthPx, hud.toastHeightPx);

            Collection = UiKit.Rect("Collection", _root).gameObject.AddComponent<CollectionPanel>();
            Collection.Build(_kit, cfg.collection, win.sideWidthPx, win.WindowHeightPx, catalog, unlocks, icons, displayForm);

            ApplyLayout(true);
        }

        void BuildClicks()
        {
            var c = _cfg.counter;
            _clicksRt = UiKit.Rect("Clicks", _pet);
            _clicksGroup = _clicksRt.gameObject.AddComponent<CanvasGroup>();
            _clicksGroup.blocksRaycasts = false;
            _clicksBox = _kit.Image("Box", _clicksRt,
                _kit.Rounded(c.cornerRadiusPx, c.outlinePx, CounterConfig.ParseColor(c.fillColor, Color.white), CounterConfig.ParseColor(c.outlineColor, Color.gray)),
                Color.white, sliced: true);
            UiKit.Stretch(_clicksBox.rectTransform);
            _clicksTextColor = CounterConfig.ParseColor(c.textColor, new Color(0.48f, 0.32f, 0.19f));
            _clicksText = _kit.Text("Value", _clicksRt, "0", c.fontPx, _clicksTextColor, number: true);
            UiKit.Stretch(_clicksText.rectTransform);
            _clicksText.rectTransform.anchoredPosition = new Vector2(0f, 3);
            _floatText = _kit.Text("Spend", _pet, "", 14, UiKit.Hex("#E53935", Color.red), bold: true, number: true);
            _floatRt = _floatText.rectTransform;
            _floatRt.sizeDelta = new Vector2(120, 20);
            _floatText.gameObject.SetActive(false);
        }

        void BuildGrowth()
        {
            var h = _cfg.hud;
            _growthRt = UiKit.Rect("Growth", _pet);
            _growthGroup = _growthRt.gameObject.AddComponent<CanvasGroup>();
            _growthGroup.blocksRaycasts = false;
            float r = h.growthBarHeightPx / 2f;
            var bg = _kit.Image("Bg", _growthRt, _kit.Rounded(r, 0, Color.white, Color.white), UiKit.Hex(h.growthBarBgColor, Color.gray), sliced: true);
            UiKit.Stretch(bg.rectTransform);
            _fillColor = UiKit.Hex(h.growthBarFillColor, Color.yellow);
            _maxColor = UiKit.Hex(h.growthBarMaxColor, new Color(0.9f, 0.6f, 0.1f));
            _growthFill = _kit.Image("Fill", _growthRt, _kit.Rounded(r, 0, Color.white, Color.white), _fillColor, sliced: true);
            var frt = _growthFill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = Vector2.zero;
        }

        // ---------- 数据输入 ----------

        public void SetToggles(bool showClicks, bool showGrowth)
        {
            _showClicks = showClicks;
            _showGrowth = showGrowth;
            if (!showClicks) { _rollT = -1f; _floatT = -1f; _floatText.gameObject.SetActive(false); }
            ApplyLayout(false);
        }

        public void SetToggleImmediate(bool showClicks, bool showGrowth)
        {
            SetToggles(showClicks, showGrowth);
            _clicksGroup.alpha = showClicks ? 1f : 0f;
            _growthGroup.alpha = showGrowth ? 1f : 0f;
            ApplyLayout(true);
        }

        /// <summary>点击量。减少时（抽奖扣费）0.4s 滚动到新值并飘出「-n」；点击量框隐藏时不播放。</summary>
        public void SetClicks(long balance, bool paused)
        {
            _paused = paused;
            if (_clicksTarget < 0)
            {
                _clicksTarget = balance;
                _clicksShown = balance;
                UpdateClicksText();
                return;
            }
            if (balance == _clicksTarget) return;

            if (balance < _clicksTarget && _showClicks)
            {
                long spent = _clicksTarget - balance;
                _rollFrom = (long)_clicksShown;
                _rollTo = balance;
                _rollT = 0f;
                _floatT = 0f;
                _floatText.text = "-" + spent.ToString(CultureInfo.InvariantCulture);
                _floatText.gameObject.SetActive(true);
            }
            else
            {
                _rollT = -1f;
                _clicksShown = balance;
            }
            _clicksTarget = balance;
            UpdateClicksText();
        }

        /// <summary>snap=true：直接跳到目标（进化换形态那一刻进度条归零，不播放倒退动画）。</summary>
        public void SetGrowth(float ratio, bool isMax, bool snap)
        {
            _growthTarget = Mathf.Clamp01(ratio);
            _growthMax = isMax;
            if (snap) _growthShown = _growthTarget;
            ApplyGrowthFill();
        }

        public void SetBubbleWanted(bool wanted)
        {
            _bubbleWanted = wanted;
            if (wanted && !Bubble.Visible) Bubble.Show();
            else if (!wanted && Bubble.Visible && !Bubble.IsBusy) Bubble.HideImmediate();
        }

        /// <summary>头顶（相对脚底的像素高度），用于「新！」定位。</summary>
        public void SetHeadTop(float px) => Badge.SetHeadTop(px);

        public void PlaceSidePanels(PanelSide side)
        {
            float sign = side == PanelSide.Right ? 1f : -1f;
            var win = _cfg.window;
            var crt = (RectTransform)Collection.transform;
            crt.anchoredPosition = new Vector2(sign * (win.PetAreaPx / 2f + win.sideWidthPx / 2f), 0f);
            var trt = (RectTransform)Toast.transform;
            float petCenterY = _pet.anchoredPosition.y + win.sizePx / 2f;
            trt.anchoredPosition = new Vector2(sign * (win.PetAreaPx / 2f + 10f + _cfg.hud.toastWidthPx / 2f), petCenterY);
        }

        // ---------- 每帧 ----------

        void Update()
        {
            if (_cfg == null) return;
            float dt = Time.unscaledDeltaTime;
            float fade = Mathf.Max(0.01f, _cfg.hud.toggleFadeSec);
            _clicksGroup.alpha = Mathf.MoveTowards(_clicksGroup.alpha, _showClicks ? 1f : 0f, dt / fade);
            _growthGroup.alpha = Mathf.MoveTowards(_growthGroup.alpha, _showGrowth ? 1f : 0f, dt / fade);

            if (_rollT >= 0f)
            {
                _rollT += dt;
                float k = Mathf.Clamp01(_rollT / Mathf.Max(0.01f, _cfg.hud.spendRollSec));
                _clicksShown = _rollFrom + (_rollTo - _rollFrom) * (double)UiKit.EaseOutCubic(k);
                if (k >= 1f) { _rollT = -1f; _clicksShown = _clicksTarget; }
                UpdateClicksText();
            }

            if (_floatT >= 0f)
            {
                _floatT += dt;
                float k = Mathf.Clamp01(_floatT / Mathf.Max(0.01f, _cfg.hud.spendFloatSec));
                var basePos = _clicksRt.anchoredPosition + new Vector2(0f, _cfg.counter.heightPx / 2f + 10f);
                _floatRt.anchoredPosition = basePos + new Vector2(0f, _cfg.hud.spendFloatRisePx * k);
                var col = _floatText.color;
                col.a = 1f - k;
                _floatText.color = col;
                if (k >= 1f) { _floatT = -1f; _floatText.gameObject.SetActive(false); }
            }

            if (!Mathf.Approximately(_growthShown, _growthTarget))
            {
                _growthShown += (_growthTarget - _growthShown) * Mathf.Min(1f, dt * 10f);
                if (Mathf.Abs(_growthShown - _growthTarget) < 0.0005f) _growthShown = _growthTarget;
                ApplyGrowthFill();
            }

            if (!_bubbleWanted && Bubble.Visible && !Bubble.IsBusy) Bubble.HideImmediate();
            ApplyLayout(false);
        }

        void UpdateClicksText()
        {
            string s = ((long)System.Math.Round(_clicksShown)).ToString("N0", CultureInfo.InvariantCulture);
            if (_clicksText.text != s) _clicksText.text = s;
            var col = _clicksTextColor;
            if (_paused) col.a *= _cfg.counter.pausedTextAlpha;
            _clicksText.color = col;

            float w = Mathf.Max(_cfg.counter.minWidthPx, Mathf.Ceil(_clicksText.preferredWidth) + _cfg.counter.paddingXPx * 2);
            if (((int)w & 1) == 1) w += 1;
            _clicksWidth = w;
        }

        void ApplyGrowthFill()
        {
            float w = _cfg.hud.growthBarWidthPx, h = _cfg.hud.growthBarHeightPx;
            bool show = _growthShown > 0.001f;
            _growthFill.enabled = show;
            _growthFill.color = _growthMax ? _maxColor : _fillColor;
            _growthFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(h, w * _growthShown), 0f);
        }

        void ApplyLayout(bool force)
        {
            var p = new HudLayoutParams
            {
                clicksCenterY = _cfg.counter.offsetYPx,
                clicksWidth = _clicksWidth,
                clicksHeight = _cfg.counter.heightPx,
                growthWidth = _cfg.hud.growthBarWidthPx,
                growthHeight = _cfg.hud.growthBarHeightPx,
                growthGap = _cfg.hud.growthBarGapPx,
                bubbleWidth = _cfg.hud.bubbleWidthPx,
                bubbleHeight = _cfg.hud.bubbleHeightPx,
                bubbleGap = _cfg.hud.bubbleGapPx,
            };
            var r = HudLayout.Compute(_showClicks, _showGrowth, Bubble.Visible, p);

            // 被隐藏的元素原地淡出，不跟着补位；可见元素直接跳到新位置
            if (r.Clicks.HasValue) Apply(_clicksRt, r.Clicks.Value);
            else if (force) Apply(_clicksRt, new Rect(-p.clicksWidth / 2f, p.clicksCenterY - p.clicksHeight / 2f, p.clicksWidth, p.clicksHeight));
            if (r.Growth.HasValue) Apply(_growthRt, r.Growth.Value);
            else if (force) Apply(_growthRt, new Rect(-p.growthWidth / 2f, p.clicksCenterY - p.clicksHeight / 2f - p.growthGap - p.growthHeight, p.growthWidth, p.growthHeight));
            if (r.Bubble.HasValue)
            {
                var b = r.Bubble.Value;
                ((RectTransform)Bubble.transform).anchoredPosition = b.center;
                Bubble.SetTailVisible(r.BubbleBesideClicks);
            }
        }

        static void Apply(RectTransform rt, Rect r)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = r.center;
            rt.sizeDelta = r.size;
        }

        void OnDestroy()
        {
            _kit?.Dispose();
            if (silhouetteMaterial != null && (silhouetteMaterial.hideFlags & HideFlags.DontSave) != 0) Destroy(silhouetteMaterial);
        }
    }
}
