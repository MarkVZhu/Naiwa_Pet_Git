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
        public EmoteConfig emote = new EmoteConfig();
        public WindowConfig window = new WindowConfig();
        public EvolutionFxConfig evolutionFx = new EvolutionFxConfig();
        public EvolutionConfig evolution = new EvolutionConfig();
        public ContentConfig content = new ContentConfig();
        public AppConfig app = new AppConfig();
        public DebugConfig debug = new DebugConfig();

        public InputSourceMode InputSource =>
            Enum.TryParse(input.source, true, out InputSourceMode mode) ? mode : InputSourceMode.GlobalHook;

        /// <summary>把明显非法的值拉回默认值，避免配置写错导致卡死（C10）。</summary>
        public void Sanitize(Action<string> warn)
        {
            var d = new GameConfig();
            if (growth.eggToSmall <= 0) { warn?.Invoke("growth.eggToSmall 非法，使用默认值"); growth.eggToSmall = d.growth.eggToSmall; }
            if (growth.smallToBig <= growth.eggToSmall) { warn?.Invoke("growth.smallToBig 必须大于 eggToSmall，使用默认值"); growth.smallToBig = Math.Max(d.growth.smallToBig, growth.eggToSmall + 1); }
            if (input.maxCountPerSecond <= 0) input.maxCountPerSecond = d.input.maxCountPerSecond;
            if (pet.emoteFadeSec < 0f) pet.emoteFadeSec = d.pet.emoteFadeSec;
            if (pet.dragThresholdPx < 1) pet.dragThresholdPx = d.pet.dragThresholdPx;
            if (pet.squashScaleY < 0.5f || pet.squashScaleY > 1f) pet.squashScaleY = d.pet.squashScaleY;
            if (pet.squashScaleX < 0.5f || pet.squashScaleX > 1.5f) pet.squashScaleX = d.pet.squashScaleX;
            if (pet.squashRecoverSec < 0.01f) pet.squashRecoverSec = d.pet.squashRecoverSec;
            if (counter == null) counter = new CounterConfig();
            if (counter.heightPx < 8) counter.heightPx = d.counter.heightPx;
            if (counter.fontPx < 6) counter.fontPx = d.counter.fontPx;
            if (counter.cornerRadiusPx < 1) counter.cornerRadiusPx = 1;
            if (counter.cornerRadiusPx * 2 > counter.heightPx) counter.cornerRadiusPx = counter.heightPx / 2;
            if (counter.fontNames == null || counter.fontNames.Length == 0) counter.fontNames = d.counter.fontNames;
            if (window.sizePx < 32) window.sizePx = d.window.sizePx;
            if (window.marginPx < 0) window.marginPx = d.window.marginPx;
            if (app.targetFps < 5) app.targetFps = d.app.targetFps;
            if (app.saveIntervalSec < 1) app.saveIntervalSec = d.app.saveIntervalSec;
            if (evolution.totalSec < evolution.newSettleEndSec) evolution.totalSec = Math.Max(d.evolution.totalSec, evolution.newSettleEndSec);
            if (string.IsNullOrEmpty(content.clipsResourcePath)) content.clipsResourcePath = d.content.clipsResourcePath;
            if (string.IsNullOrEmpty(content.manifestResourcePath)) content.manifestResourcePath = d.content.manifestResourcePath;
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

    [Serializable]
    public sealed class GrowthConfig
    {
        public int eggToSmall = 3000;   // [P] §2.2
        public int smallToBig = 25000;  // [P] §2.2
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
    }

    /// <summary>桌宠下方的计数框（像素单位均为屏幕像素）。</summary>
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
        /// <summary>按顺序尝试的系统字体；都没有时用 Unity 内置字体。</summary>
        public string[] fontNames = { "Segoe UI Semibold", "Segoe UI", "Arial" };
        public int sortingOrder = 50;

        public static Color ParseColor(string html, Color fallback) =>
            ColorUtility.TryParseHtmlString(html, out var c) ? c : fallback;
    }

    [Serializable]
    public sealed class EmoteConfig
    {
        public bool avoidImmediateRepeat = false; // [A]
    }

    [Serializable]
    public sealed class WindowConfig
    {
        public int sizePx = 300;              // [R] 角色显示高度 300px 对应 600px 画布
        public int marginPx = 40;             // [P] 给烟雾扩散留空间
        public float topmostReassertSec = 5f; // [P]
        public float minVisibleFraction = 0.3f; // [P] §3.3
        /// <summary>[A] 加 WS_EX_LAYERED 后调用 SetLayeredWindowAttributes(alpha=255) 使窗口可见。打包后黑底/不可见时可尝试关闭。</summary>
        public bool useLayeredAlpha = true;

        /// <summary>画布 600px 对应 6 个世界单位，1 单位 = sizePx/6 屏幕像素。</summary>
        public float PixelsPerUnit => sizePx / 6f;
        public int WindowSizePx => sizePx + marginPx * 2;
        public float OrthographicSize => WindowSizePx / PixelsPerUnit / 2f;
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
    public sealed class ContentConfig
    {
        /// <summary>仅编辑器导入用。</summary>
        public string sourceDir = @"D:\Workbuddy_Workplace\桌宠奶蛙\GameResource";
        /// <summary>序列帧所在的 Resources 相对路径（对应 Assets/Resources/AnimationImages）。</summary>
        public string clipsResourcePath = "AnimationImages";
        /// <summary>映射表的 Resources 相对路径（不带扩展名）。</summary>
        public string manifestResourcePath = "Naiwa/mvp_content";
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
