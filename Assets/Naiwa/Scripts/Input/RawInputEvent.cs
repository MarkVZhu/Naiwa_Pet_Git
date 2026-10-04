using System.Collections.Generic;

namespace Naiwa.Input
{
    public enum RawKind { KeyDown, KeyUp, MouseDown, MouseUp, MouseWheel }

    public enum MouseBtn { None, Left, Right, Middle, X1, X2 }

    public struct RawInputEvent
    {
        public RawKind Kind;
        /// <summary>仅鼠标事件有效。</summary>
        public MouseBtn Button;
        /// <summary>VK 码。只用于 InputFilter 判断按住状态，禁止持久化或打日志（C6）。</summary>
        public int KeyId;
        /// <summary>键盘 LLKHF_INJECTED(0x10) / 鼠标 LLMHF_INJECTED(0x01)。</summary>
        public bool Injected;
        public long TimestampMs;
        /// <summary>仅鼠标事件有效（桌面坐标，y 向下）。</summary>
        public int ScreenX, ScreenY;
        /// <summary>仅 MouseWheel 有效：滚轮增量（一格 = ±120，向上为正）。</summary>
        public int WheelDelta;

        public bool IsPress => Kind == RawKind.KeyDown || Kind == RawKind.MouseDown;
    }

    /// <summary>输入来源：全局钩子或 Editor 模拟器。主线程每帧调用 Drain 取出事件。</summary>
    public interface IInputSource
    {
        void Drain(List<RawInputEvent> into);
        void Dispose();
    }
}
