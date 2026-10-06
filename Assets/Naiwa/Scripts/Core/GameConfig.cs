using System;
using UnityEngine;

namespace Naiwa.Core
{
    /// <summary>
    /// 编译期默认值。打包后由 StreamingAssets/config/game_config.json 逐字段覆盖（见 ConfigLoader）。
    /// 标注：[R] 有依据 / [P] 占位待实测 / [A] 设计假设。
    /// </summary>
    [Serializable]
    public sealed class GameConfig
    {
        public GrowthConfig growth = new GrowthConfig();
        public InputConfig input = new InputConfig();
        public PetConfig pet = new PetConfig();
        public CounterConfig counter = new CounterConfig();
        public HudConfig hud = new HudConfig();
        public LotteryConfig lottery = new LotteryConfig();
        public CollectionConfig collection = new CollectionConfig();
        public FormConfig form = new FormConfig();
        public EmoteConfig emote = new EmoteConfig();
        public WindowConfig window = new WindowConfig();
        public EvolutionFxConfig evolutionFx = new EvolutionFxConfig();
        public EvolutionConfig evolution = new EvolutionConfig();
        public ContentPathsConfig content = new ContentPathsConfig();
        public AppConfig app = new AppConfig();
        public DebugConfig debug = new DebugConfig();

        public InputSourceMode InputSource =>
            Enum.TryParse(input.source, true, out InputSourceMode mode) ? mode : InputSourceMode.GlobalHook;

        /// <summary>把明显非法的值拉回默认值，避免配置写错导致卡死（C10）。</summary>
        public void Sanitize(Action<string> warn)
        {
            var d = new GameConfig();
            if (growth == null) growth = new GrowthConfig();
            if (pet == null) pet = new PetConfig();
            if (counter == null) counter = new CounterConfig();
            if (hud == null) hud = new HudConfig();
            if (lottery == null) lottery = new LotteryConfig();
            if (collection == null) collection = new CollectionConfig();
            if (form == null) form = new FormConfig();
            if (window == null) window = new WindowConfig();

            if (growth.eggToSmall <= 0) { warn?.Invoke("growth.eggToSmall 非法，使用默认值"); growth.eggToSmall = d.growth.eggToSmall; }
            if (growth.smallToBig <= growth.eggToSmall) { warn?.Invoke("growth.smallToBig 必须大于 eggToSmall，使用默认值"); growth.smallToBig = Math.Max(d.growth.smallToBig, growth.eggToSmall + 1); }
            if (input.maxCountPerSecond <= 0) input.maxCountPerSecond = d.input.maxCountPerSecond;
            if (pet.emoteFadeSec < 0f) pet.emoteFadeSec = d.pet.emoteFadeSec;
            if (pet.dragThresholdPx < 1) pet.dragThresholdPx = d.pet.dragThresholdPx;
            if (pet.squashScaleY < 0.5f || pet.squashScaleY > 1f) pet.squashScaleY = d.pet.squashScaleY;
            if (pet.squashScaleX < 0.5f || pet.squashScaleX > 1.5f) pet.squashScaleX = d.pet.squashScaleX;
            if (pet.squashRecoverSec < 0.01f) pet.squashRecoverSec = d.pet.squashRecoverSec;
            if (pet.emptyPokeScaleY < 0.5f || pet.emptyPokeScaleY > 1f) pet.emptyPokeScaleY = d.pet.emptyPokeScaleY;
            if (pet.emptyPokeSec < 0.01f) pet.emptyPokeSec = d.pet.emptyPokeSec;
            if (pet.newBadgeSec < 0.1f) pet.newBadgeSec = d.pet.newBadgeSec;
            if (pet.eggScale < 0.3f || pet.eggScale > 2f) pet.eggScale = d.pet.eggScale;
            if (pet.smallScale < 0.3f || pet.smallScale > 2f) pet.smallScale = d.pet.smallScale;
            if (pet.bigScale < 0.3f || pet.bigScale > 2f) pet.bigScale = d.pet.bigScale;
            if (window.headroomPx < 0) window.headroomPx = d.window.headroomPx;
            if (counter.heightPx < 8) counter.heightPx = d.counter.heightPx;
            if (counter.fontPx < 6) counter.fontPx = d.counter.fontPx;
            if (counter.cornerRadiusPx < 1) counter.cornerRadiusPx = 1;
            if (counter.cornerRadiusPx * 2 > counter.heightPx) counter.cornerRadiusPx = counter.heightPx / 2;
            if (counter.fontNames == null || counter.fontNames.Length == 0) counter.fontNames = d.counter.fontNames;
            if (hud.fontNames == null || hud.fontNames.Length == 0) hud.fontNames = d.hud.fontNames;
            if (hud.growthBarWidthPx < 8) hud.growthBarWidthPx = d.hud.growthBarWidthPx;
            if (hud.growthBarHeightPx < 2) hud.growthBarHeightPx = d.hud.growthBarHeightPx;
            if (lottery.cost < 1) { warn?.Invoke("lottery.cost 必须 ≥ 1，使用默认值"); lottery.cost = d.lottery.cost; }
            if (lottery.cooldownMinutes < 0f) lottery.cooldownMinutes = d.lottery.cooldownMinutes;
            if (lottery.spinSec < 0.1f) lottery.spinSec = d.lottery.spinSec;
            if (lottery.spinTurns < 0) lottery.spinTurns = d.lottery.spinTurns;
            if (lottery.revealToastSec < 0.5f) lottery.revealToastSec = d.lottery.revealToastSec;
            if (!Enum.TryParse(lottery.poolScope, true, out LotteryPoolScope _))
            {
                warn?.Invoke($"lottery.poolScope 未知值 '{lottery.poolScope}'，回退为 All");
                lottery.poolScope = nameof(LotteryPoolScope.All);
            }
            if (collection.columns < 1) collection.columns = d.collection.columns;
            if (collection.slotPx < 32) collection.slotPx = d.collection.slotPx;
            if (window.sizePx < 32) window.sizePx = d.window.sizePx;
            if (window.marginPx < 0) window.marginPx = d.window.marginPx;
            if (window.sideWidthPx < 240) window.sideWidthPx = d.window.sideWidthPx;
            if (window.hudHeightPx < 40) window.hudHeightPx = d.window.hudHeightPx;
            if (!(window.scaleMin >= 0.1f && window.scaleMin <= 1f)) window.scaleMin = d.window.scaleMin;
            if (!(window.scaleMax >= 1f && window.scaleMax <= 3f)) window.scaleMax = d.window.scaleMax;
            if (!(window.scaleStep > 0f && window.scaleStep <= 0.5f)) window.scaleStep = d.window.scaleStep;
            if (app.targetFps < 5) app.targetFps = d.app.targetFps;
            if (app.saveIntervalSec < 1) app.saveIntervalSec = d.app.saveIntervalSec;
            if (evolution.totalSec < evolution.newSettleEndSec) evolution.totalSec = Math.Max(d.evolution.totalSec, evolution.newSettleEndSec);
            if (string.IsNullOrEmpty(content.clipsResourcePath)) content.clipsResourcePath = d.content.clipsResourcePath;
            if (string.IsNullOrEmpty(content.manifestResourcePath)) content.manifestResourcePath = d.content.manifestResourcePath;
            if (string.IsNullOrEmpty(content.contentJsonPath)) content.contentJsonPath = d.content.contentJsonPath;
            if (!Enum.TryParse(input.source, true, out InputSourceMode _))
            {
                warn?.Invoke($"input.source 未知值 '{input.source}'，回退为 GlobalHook");
                input.source = nameof(InputSourceMode.GlobalHook);
            }
        }
    }

    public enum InputSourceMode
    {
        /// <summary>全局键鼠按下都计数（默认）。</summary>
        GlobalHook,
        /// <summary>降级模式：只有点击桌宠计数，不安装键盘钩子。</summary>
        PetClickOnly,
    }

    public enum LotteryPoolScope
    {
        /// <summary>所有阶段中未解锁的 Lottery 表情。</summary>
        All,
        /// <summary>当前显示阶段有未解锁的就先抽当前阶段。</summary>
        CurrentFormFirst,
        /// <summary>只抽当前显示阶段。</summary>
        CurrentFormOnly,
    }

    [Serializable]
    public sealed class GrowthConfig
    {
        public int eggToSmall = 5000;   // [R] v1.0 §4.5 用户定稿
        public int smallToBig = 12000;  // [R] v1.0 §4.5 用户定稿
    }

    [Serializable]
    public sealed class InputConfig
    {
        public string source = nameof(InputSourceMode.GlobalHook); // [A]
        public int maxCountPerSecond = 15;  // [R] §2.2
        public bool ignoreInjected = true;  // [R] §2.2
    }

    [Serializable]
    public sealed class PetConfig
    {
        public float emoteFadeSec = 0.15f;  // [P]
        public int dragThresholdPx = 6;     // [P]
        /// <summary>[P] 每次按下时竖直方向压到的比例（以脚底为锚点）。</summary>
        public float squashScaleY = 0.93f;
        /// <summary>[P] 压缩时的水平比例；1 = 只压竖直方向。</summary>
        public float squashScaleX = 1.0f;
        /// <summary>[P] 打字每秒 8 下时间隔约 0.125s，回弹需在下一下前完成（§2.2）。</summary>
        public float squashRecoverSec = 0.12f;
        /// <summary>[P] v1.0 §6.3「新！」标记显示时长。</summary>
        public float newBadgeSec = 3.0f;
        /// <summary>[P] v1.0 §6.2 当前阶段没有已解锁表情时，点它的下压回弹。</summary>
        public float emptyPokeSec = 0.15f;
        public float emptyPokeScaleY = 0.94f;

        /// <summary>
        /// [P] 各阶段的显示缩放（以脚底为锚点，作用于该阶段的 idle 和全部表情，点击判定同步缩放）。
        /// 小奶蛙 = 1 为基准；奶蛋小一点、大奶蛙大一点，体现成长。
        /// </summary>
        public float eggScale = 0.85f;
        public float smallScale = 1.0f;
        public float bigScale = 1.12f;

        public float ScaleOf(Naiwa.Growth.FormId form)
        {
            switch (form)
            {
                case Naiwa.Growth.FormId.Egg: return eggScale;
                case Naiwa.Growth.FormId.Big: return bigScale;
                default: return smallScale;
            }
        }
    }

    /// <summary>桌宠下方的点击量框（像素单位均为屏幕像素）。</summary>
    [Serializable]
    public sealed class CounterConfig
    {
        /// <summary>框中心相对脚底的竖直偏移（负数 = 在脚下方）。</summary>
        public int offsetYPx = -24;
        public int heightPx = 26;
        public int paddingXPx = 12;
        public int minWidthPx = 56;
        public int fontPx = 16;
        public int cornerRadiusPx = 9;
        public float outlinePx = 1.5f;
        public string fillColor = "#FFF9ECF0";
        public string outlineColor = "#E2C48FFF";
        public string textColor = "#7A5230FF";
        /// <summary>暂停计数时数字的透明度。</summary>
        public float pausedTextAlpha = 0.45f;
        /// <summary>数字用的系统字体，按顺序尝试。</summary>
        public string[] fontNames = { "Segoe UI Semibold", "Segoe UI", "Arial" };

        public static Color ParseColor(string html, Color fallback) =>
            ColorUtility.TryParseHtmlString(html, out var c) ? c : fallback;
    }

    /// <summary>v1.0 §4.3–§4.4、附录 B。</summary>
    [Serializable]
    public sealed class HudConfig
    {
        public int growthBarWidthPx = 120;   // [P]
        public int growthBarHeightPx = 8;    // [P]
        public int growthBarGapPx = 7;       // [P] 点击量框下方间距
        public string growthBarBgColor = "#ECDEBE";
        public string growthBarFillColor = "#F2B92B";
        public string growthBarMaxColor = "#E89A1C";
        public float spendRollSec = 0.4f;    // [P]
        public float spendFloatSec = 0.8f;   // [P]
        public int spendFloatRisePx = 20;    // [P]
        public float toggleFadeSec = 0.15f;  // [P]
        public bool showClicksDefault = true;  // [A] 新存档时的初值
        public bool showGrowthDefault = true;  // [A]
        public int bubbleWidthPx = 58;       // [P]
        public int bubbleHeightPx = 70;      // [P]
        public int bubbleGapPx = 10;         // [P]
        public int toastWidthPx = 220;       // [P]
        public int toastHeightPx = 84;       // [P]
        /// <summary>中文界面字体，按顺序尝试。</summary>
        public string[] fontNames = { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" };
    }

    /// <summary>v1.0 §5、附录 B。</summary>
    [Serializable]
    public sealed class LotteryConfig
    {
        public int cost = 2000;                    // [P]
        public float cooldownMinutes = 30f;        // [P]
        public string poolScope = nameof(LotteryPoolScope.All); // [A]
        public float spinSec = 1.2f;               // [P]
        public int spinTurns = 3;                  // [P]
        public float bubbleWobbleIntervalSec = 2.5f; // [P]
        public float revealToastSec = 3.5f;        // [P]

        public LotteryPoolScope Scope =>
            Enum.TryParse(poolScope, true, out LotteryPoolScope s) ? s : LotteryPoolScope.All;

        public TimeSpan Cooldown => TimeSpan.FromMinutes(Math.Max(0f, cooldownMinutes));
    }

    /// <summary>v1.0 §8.2–§8.3。</summary>
    [Serializable]
    public sealed class CollectionConfig
    {
        public int columns = 3;          // [P]
        public int slotPx = 104;         // [P]
        public int slotSpacingPx = 14;   // [P]
        public string silhouetteColor = "#000000"; // [R]
        /// <summary>[A] §6.4 图鉴点播时打断正在播放的表情。</summary>
        public bool interruptEmoteOnPlay = true;
    }

    [Serializable]
    public sealed class FormConfig
    {
        /// <summary>[A] §6.5 解锁新形态时直接变身。</summary>
        public bool autoEvolveOnUnlock = true;
    }

    [Serializable]
    public sealed class EmoteConfig
    {
        public bool avoidImmediateRepeat = false; // [A]
    }

    /// <summary>
    /// 窗口为固定大画布（v1.0 §7.3）：左侧区 | 宠物区 | 右侧区，宠物区下方是 HUD 区。
    /// 宠物区 = v0.1 的整个窗口（sizePx + 2×marginPx）。
    /// </summary>
    [Serializable]
    public sealed class WindowConfig
    {
        public int sizePx = 300;              // [R] 角色显示高度 300px 对应 600px 画布
        public int marginPx = 40;             // [P] 宠物区四周留白（顶部用于「新！」）
        public int sideWidthPx = 380;         // [P]
        public int hudHeightPx = 90;          // [P]
        /// <summary>[P] 宠物区上方额外留白：大奶蛙放大后头顶和「新！」不被窗口裁掉。</summary>
        public int headroomPx = 40;
        public float topmostReassertSec = 5f; // [P]
        public float minVisibleFraction = 0.3f; // [P] §3.3，只约束宠物区 + HUD 区
        /// <summary>[A] 加 WS_EX_LAYERED 后调用 SetLayeredWindowAttributes(alpha=255) 使窗口可见。</summary>
        public bool useLayeredAlpha = true;
        /// <summary>[R] 右键菜单「调整大小」的全局缩放范围（整个窗口：宠物、HUD、图鉴一起缩放）。</summary>
        public float scaleMin = 0.5f;
        public float scaleMax = 1.5f;
        /// <summary>[P] 滑块步长。</summary>
        public float scaleStep = 0.05f;

        /// <summary>限制到 [scaleMin, scaleMax] 并对齐到步长；非法值回到 1。</summary>
        public float ClampScale(float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) scale = 1f;
            scale = Mathf.Clamp(scale, scaleMin, scaleMax);
            if (scaleStep > 0f) scale = Mathf.Round(scale / scaleStep) * scaleStep;
            scale = Mathf.Clamp(scale, scaleMin, scaleMax);
            return (float)Math.Round(scale, 4);
        }

        /// <summary>画布 600px 对应 6 个世界单位，1 单位 = sizePx/6 屏幕像素。</summary>
        public float PixelsPerUnit => sizePx / 6f;
        public int PetAreaPx => sizePx + marginPx * 2;
        public int WindowWidthPx => PetAreaPx + sideWidthPx * 2;
        public int WindowHeightPx => headroomPx + PetAreaPx + hudHeightPx;
        public float OrthographicSize => WindowHeightPx / PixelsPerUnit / 2f;

        /// <summary>脚底到窗口顶边的距离（像素）。</summary>
        public float FeetFromTopPx => headroomPx + PetAreaPx - marginPx - PetGeometry.FeetFromBottomPx / (float)PetGeometry.CanvasPx * sizePx;

        /// <summary>相机中心（窗口中心）相对脚底的世界坐标高度。</summary>
        public float CameraY => (FeetFromTopPx - WindowHeightPx / 2f) / PixelsPerUnit;

        /// <summary>脚底相对窗口中心的像素偏移（UI 坐标，y 向上）。</summary>
        public Vector2 FeetOffsetFromCenterPx => new Vector2(0f, WindowHeightPx / 2f - FeetFromTopPx);
    }

    [Serializable]
    public sealed class EvolutionFxConfig
    {
        public int burstCount = 32;
        public float shapeRadius = 1.4f;
        public float centerHeight = 2.8f;
        public float startSizeMin = 1.8f;
        public float startSizeMax = 2.8f;
        public float sizeOverLifetimeStart = 0.6f;
        public float sizeOverLifetimeEnd = 1.5f;
        public float startSpeedMin = 0.8f;
        public float startSpeedMax = 1.6f;
        public float lifetimeMin = 0.9f;
        public float lifetimeMax = 1.2f;
        public float alphaPeak = 0.95f;
        public float alphaRiseEnd = 0.20f;
        public float alphaHoldEnd = 0.45f;
        public string color = "#FFF6E0";
        public float gravityModifier = -0.08f;
        public int textureSize = 128;
        public int sortingOrder = 100;

        public Color ParsedColor =>
            ColorUtility.TryParseHtmlString(color, out var c) ? c : new Color(1f, 0.965f, 0.878f, 1f);
    }

    [Serializable]
    public sealed class EvolutionConfig
    {
        public float oldPuffEndSec = 0.25f;
        public float oldPuffScale = 1.06f;
        public float oldFadeStartSec = 0.20f;
        public float oldFadeEndSec = 0.40f;
        public float swapSec = 0.30f;
        public float newStartScale = 0.90f;
        public float newFadeEndSec = 0.50f;
        public float newOvershootScale = 1.05f;
        public float newSettleEndSec = 0.70f;
        public float totalSec = 1.40f;
        public float loadTimeoutSec = 3f;
        /// <summary>[P] 加载失败后多久再重试，避免每帧重试。</summary>
        public float retryCooldownSec = 30f;
    }

    [Serializable]
    public sealed class ContentPathsConfig
    {
        /// <summary>仅编辑器导入用。</summary>
        public string sourceDir = @"D:\Workbuddy_Workplace\桌宠奶蛙\GameResource";
        /// <summary>序列帧所在的 Resources 相对路径（对应 Assets/Resources/AnimationImages）。</summary>
        public string clipsResourcePath = "AnimationImages";
        /// <summary>v1.0 内容配置，相对 StreamingAssets。</summary>
        public string contentJsonPath = "content/content.json";
        /// <summary>v0.1 映射表（找不到 content.json 时回退），Resources 相对路径，不带扩展名。</summary>
        public string manifestResourcePath = "Naiwa/mvp_content";
        /// <summary>序列帧文件名索引（编辑器生成），用于运行时只加载单帧做图鉴图标。</summary>
        public string clipIndexResourcePath = "Naiwa/clip_index";
    }

    [Serializable]
    public sealed class AppConfig
    {
        public int targetFps = 30;      // [R]
        public int saveIntervalSec = 30; // [R]
    }

    [Serializable]
    public sealed class DebugConfig
    {
        public bool enabled = true;     // 发给朋友前改 false
        /// <summary>§4.1：Editor 下默认不装全局钩子，用 Unity Input 模拟。</summary>
        public bool editorUseGlobalHook = false;
    }
}
