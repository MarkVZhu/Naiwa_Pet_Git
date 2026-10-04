#if (UNITY_STANDALONE_WIN && !UNITY_EDITOR) || UNITY_EDITOR_WIN
#define NAIWA_HOOK_AVAILABLE
#endif
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
#if NAIWA_HOOK_AVAILABLE
using System.Runtime.InteropServices;
using Naiwa.Platform;
#endif

namespace Naiwa.Input
{
    /// <summary>
    /// 全局键鼠低级钩子（§4.1）。
    /// C4：钩子安装在独立后台线程上，由该线程自己跑 GetMessage 消息循环。
    /// C5：回调只做"构造事件 → 入队 → CallNextHookEx"。
    /// C6：KeyId 只进入内存队列，不写文件、不打日志。
    /// 同一进程同一时间只允许一个实例。
    /// </summary>
    public sealed class GlobalInputHook : IInputSource
    {
        static GlobalInputHook s_active;
        static readonly ConcurrentQueue<RawInputEvent> s_queue = new ConcurrentQueue<RawInputEvent>();

        readonly bool _installKeyboard;
        Thread _thread;
        volatile uint _nativeThreadId;
        readonly ManualResetEvent _ready = new ManualResetEvent(false);

        public bool KeyboardHooked { get; private set; }
        public bool MouseHooked { get; private set; }
        public bool IsRunning => _thread != null;

        public GlobalInputHook(bool installKeyboard)
        {
            _installKeyboard = installKeyboard;
        }

        /// <summary>启动钩子线程，最多等待 timeoutMs 让钩子装好。</summary>
        public bool Start(int timeoutMs = 1000)
        {
#if NAIWA_HOOK_AVAILABLE
            if (_thread != null) return MouseHooked;
            if (s_active != null) s_active.Dispose();
            s_active = this;
            while (s_queue.TryDequeue(out _)) { }

            _thread = new Thread(ThreadMain) { IsBackground = true, Name = "NaiwaInputHook" };
            _thread.Start();
            if (!_ready.WaitOne(timeoutMs))
                Debug.LogWarning("[Naiwa] 钩子线程启动超时");
            return MouseHooked;
#else
            Debug.LogWarning("[Naiwa] 当前平台不支持全局钩子");
            return false;
#endif
        }

        public void Drain(List<RawInputEvent> into)
        {
            while (s_queue.TryDequeue(out var e))
                into.Add(e);
        }

        public void Dispose()
        {
#if NAIWA_HOOK_AVAILABLE
            var thread = _thread;
            if (thread == null) return;
            _thread = null;

            _ready.WaitOne(500);
            uint tid = _nativeThreadId;
            if (tid != 0)
                Win32Native.PostThreadMessage(tid, Win32Native.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
            if (!thread.Join(1000))
                Debug.LogWarning("[Naiwa] 钩子线程未能在 1s 内退出");

            if (s_active == this) s_active = null;
#endif
        }

#if NAIWA_HOOK_AVAILABLE
        // 委托必须被静态字段持有，防止被 GC 回收后回调崩溃。
        static readonly Win32Native.LowLevelHookProc s_keyboardProc = KeyboardProc;
        static readonly Win32Native.LowLevelHookProc s_mouseProc = MouseProc;

        void ThreadMain()
        {
            IntPtr kbHook = IntPtr.Zero, msHook = IntPtr.Zero;
            try
            {
                _nativeThreadId = Win32Native.GetCurrentThreadId();
                // 先确保本线程有消息队列，PostThreadMessage(WM_QUIT) 才能送达。
                Win32Native.PeekMessage(out _, IntPtr.Zero, 0, 0, Win32Native.PM_NOREMOVE);

                IntPtr hMod = Win32Native.GetModuleHandle(null);
                if (_installKeyboard)
                {
                    kbHook = Win32Native.SetWindowsHookEx(Win32Native.WH_KEYBOARD_LL, s_keyboardProc, hMod, 0);
                    if (kbHook == IntPtr.Zero)
                        Debug.LogWarning($"[Naiwa] 键盘钩子安装失败 (Win32 错误 {Marshal.GetLastWin32Error()})，可能被安全软件拦截，可改用 input.source=PetClickOnly");
                }

                msHook = Win32Native.SetWindowsHookEx(Win32Native.WH_MOUSE_LL, s_mouseProc, hMod, 0);
                if (msHook == IntPtr.Zero)
                    Debug.LogWarning($"[Naiwa] 鼠标钩子安装失败 (Win32 错误 {Marshal.GetLastWin32Error()})");

                KeyboardHooked = kbHook != IntPtr.Zero;
                MouseHooked = msHook != IntPtr.Zero;
                _ready.Set();

                while (Win32Native.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
                {
                    Win32Native.TranslateMessage(ref msg);
                    Win32Native.DispatchMessage(ref msg);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Naiwa] 钩子线程异常：{e.Message}");
            }
            finally
            {
                if (kbHook != IntPtr.Zero) Win32Native.UnhookWindowsHookEx(kbHook);
                if (msHook != IntPtr.Zero) Win32Native.UnhookWindowsHookEx(msHook);
                KeyboardHooked = false;
                MouseHooked = false;
                _nativeThreadId = 0;
                _ready.Set();
            }
        }

        [AOT.MonoPInvokeCallback(typeof(Win32Native.LowLevelHookProc))]
        static IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                bool down = msg == Win32Native.WM_KEYDOWN || msg == Win32Native.WM_SYSKEYDOWN;
                bool up = msg == Win32Native.WM_KEYUP || msg == Win32Native.WM_SYSKEYUP;
                if (down || up)
                {
                    var data = (Win32Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32Native.KBDLLHOOKSTRUCT));
                    s_queue.Enqueue(new RawInputEvent
                    {
                        Kind = down ? RawKind.KeyDown : RawKind.KeyUp,
                        KeyId = (int)data.vkCode,
                        Injected = (data.flags & Win32Native.LLKHF_INJECTED) != 0,
                        TimestampMs = data.time,
                    });
                }
            }
            return Win32Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        [AOT.MonoPInvokeCallback(typeof(Win32Native.LowLevelHookProc))]
        static IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg != Win32Native.WM_MOUSEMOVE && TryMapMouse(msg, out var kind, out var button, lParam, out var data))
                {
                    s_queue.Enqueue(new RawInputEvent
                    {
                        Kind = kind,
                        Button = button,
                        Injected = (data.flags & Win32Native.LLMHF_INJECTED) != 0,
                        TimestampMs = data.time,
                        ScreenX = data.pt.X,
                        ScreenY = data.pt.Y,
                        WheelDelta = kind == RawKind.MouseWheel ? (short)((data.mouseData >> 16) & 0xFFFF) : 0,
                    });
                }
            }
            return Win32Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        static bool TryMapMouse(int msg, out RawKind kind, out MouseBtn button, IntPtr lParam, out Win32Native.MSLLHOOKSTRUCT data)
        {
            kind = RawKind.MouseDown;
            button = MouseBtn.None;
            data = default;
            switch (msg)
            {
                case Win32Native.WM_LBUTTONDOWN: kind = RawKind.MouseDown; button = MouseBtn.Left; break;
                case Win32Native.WM_LBUTTONUP: kind = RawKind.MouseUp; button = MouseBtn.Left; break;
                case Win32Native.WM_RBUTTONDOWN: kind = RawKind.MouseDown; button = MouseBtn.Right; break;
                case Win32Native.WM_RBUTTONUP: kind = RawKind.MouseUp; button = MouseBtn.Right; break;
                case Win32Native.WM_MBUTTONDOWN: kind = RawKind.MouseDown; button = MouseBtn.Middle; break;
                case Win32Native.WM_MBUTTONUP: kind = RawKind.MouseUp; button = MouseBtn.Middle; break;
                case Win32Native.WM_XBUTTONDOWN: kind = RawKind.MouseDown; break;
                case Win32Native.WM_XBUTTONUP: kind = RawKind.MouseUp; break;
                case Win32Native.WM_MOUSEWHEEL: kind = RawKind.MouseWheel; break;
                default: return false;
            }

            data = (Win32Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32Native.MSLLHOOKSTRUCT));
            if (msg == Win32Native.WM_XBUTTONDOWN || msg == Win32Native.WM_XBUTTONUP)
                button = ((data.mouseData >> 16) & 0xFFFF) == 2 ? MouseBtn.X2 : MouseBtn.X1;
            return true;
        }

#if UNITY_EDITOR
        // Editor 下重载程序集前必须卸载钩子，否则 Editor 可能卡死（§4.1）。
        [UnityEditor.InitializeOnLoadMethod]
        static void RegisterEditorCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () => s_active?.Dispose();
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) s_active?.Dispose();
            };
        }
#endif
#endif

        /// <summary>核对某个 VK 是否仍按下（用于按住集合防泄漏）。不支持的平台返回 false。</summary>
        public static bool IsKeyStillDown(int vk)
        {
#if NAIWA_HOOK_AVAILABLE
            return (Win32Native.GetAsyncKeyState(vk) & 0x8000) != 0;
#else
            return false;
#endif
        }
    }
}
