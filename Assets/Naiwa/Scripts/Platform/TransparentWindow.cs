using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Text;
using Debug = UnityEngine.Debug;
#endif

namespace Naiwa.Platform
{
    /// <summary>
    /// 无边框、背景透明、置顶、不出现在任务栏的窗口（§3.1）。
    /// 同时提供桌面坐标 ↔ Unity 屏幕坐标换算。Editor 下全部走模拟实现（C3）。
    /// 桌面坐标：y 向下；Unity 屏幕坐标：y 向上、原点在客户区左下角。
    /// </summary>
    public sealed class TransparentWindow
    {
        public bool IsNative { get; private set; }
        public int SizePx { get; private set; }
        public int WidthPx { get; private set; }
        public int HeightPx { get; private set; }

        /// <summary>某个桌面矩形所在（或最近的）显示器的工作区。Editor 下返回一个很大的矩形。</summary>
        public RectInt GetWorkAreaFor(RectInt desktopRect)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var rect = new Win32Native.RECT { Left = desktopRect.xMin, Top = desktopRect.yMin, Right = desktopRect.xMax, Bottom = desktopRect.yMax };
            var mon = Win32Native.MonitorFromRect(ref rect, Win32Native.MONITOR_DEFAULTTONEAREST);
            var info = new Win32Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32Native.MONITORINFO)) };
            if (mon != IntPtr.Zero && Win32Native.GetMonitorInfo(mon, ref info))
                return new RectInt(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right - info.rcWork.Left, info.rcWork.Bottom - info.rcWork.Top);
#endif
            return new RectInt(-100000, -100000, 200000, 200000);
        }

        /// <summary>主显示器工作区。</summary>
        public RectInt GetPrimaryWorkArea()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var mon = Win32Native.MonitorFromPoint(new Win32Native.POINT { X = 0, Y = 0 }, Win32Native.MONITOR_DEFAULTTOPRIMARY);
            var info = new Win32Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32Native.MONITORINFO)) };
            if (mon != IntPtr.Zero && Win32Native.GetMonitorInfo(mon, ref info))
                return new RectInt(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right - info.rcWork.Left, info.rcWork.Bottom - info.rcWork.Top);
#endif
            return new RectInt(0, 0, Screen.width, Screen.height);
        }

        /// <summary>该桌面矩形是否落在任何显示器上。</summary>
        public bool IsOnAnyMonitor(RectInt desktopRect)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var rect = new Win32Native.RECT { Left = desktopRect.xMin, Top = desktopRect.yMin, Right = desktopRect.xMax, Bottom = desktopRect.yMax };
            return Win32Native.MonitorFromRect(ref rect, Win32Native.MONITOR_DEFAULTTONULL) != IntPtr.Zero;
#else
            return true;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        IntPtr _hwnd;
        long _exStyleBase;
        bool _clickThrough;

        public IntPtr Handle => _hwnd;
#endif

        /// <summary>应用窗口样式。应在第一帧之后调用（Unity 已完成窗口创建）。</summary>
        public void Initialize(int widthPx, int heightPx, bool useLayeredAlpha)
        {
            SizePx = widthPx;
            WidthPx = widthPx;
            HeightPx = heightPx;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _hwnd = FindUnityWindow();
            if (_hwnd == IntPtr.Zero)
            {
                Debug.LogError("[Naiwa] 找不到 Unity 主窗口（UnityWndClass），透明窗口未启用");
                return;
            }

            Win32Native.ShowWindow(_hwnd, Win32Native.SW_HIDE);
            Win32Native.SetWindowLongPtr(_hwnd, Win32Native.GWL_STYLE, Win32Native.WS_POPUP | Win32Native.WS_VISIBLE);
            _exStyleBase = Win32Native.WS_EX_LAYERED | Win32Native.WS_EX_TOOLWINDOW;
            Win32Native.SetWindowLongPtr(_hwnd, Win32Native.GWL_EXSTYLE, _exStyleBase);
            if (useLayeredAlpha)
                Win32Native.SetLayeredWindowAttributes(_hwnd, 0, 255, Win32Native.LWA_ALPHA);

            var margins = new Win32Native.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            int hr = Win32Native.DwmExtendFrameIntoClientArea(_hwnd, ref margins);
            if (hr != 0) Debug.LogWarning($"[Naiwa] DwmExtendFrameIntoClientArea 失败 hr=0x{hr:X8}");

            Win32Native.GetWindowRect(_hwnd, out var r);
            Win32Native.SetWindowPos(_hwnd, Win32Native.HWND_TOPMOST, r.Left, r.Top, widthPx, heightPx,
                Win32Native.SWP_FRAMECHANGED | Win32Native.SWP_NOACTIVATE);
            Win32Native.ShowWindow(_hwnd, Win32Native.SW_SHOW);
            _clickThrough = false;
            IsNative = true;
#endif
        }

        public void ReassertTopmost()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsNative) return;
            Win32Native.SetWindowPos(_hwnd, Win32Native.HWND_TOPMOST, 0, 0, 0, 0,
                Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_NOACTIVATE);
#endif
        }

        /// <summary>只在状态变化时调用 SetWindowLongPtr。</summary>
        public void SetClickThrough(bool enabled)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsNative || _clickThrough == enabled) return;
            _clickThrough = enabled;
            long ex = enabled ? _exStyleBase | Win32Native.WS_EX_TRANSPARENT : _exStyleBase;
            Win32Native.SetWindowLongPtr(_hwnd, Win32Native.GWL_EXSTYLE, ex);
#endif
        }

        public Vector2Int GetCursorDesktop()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            Win32Native.GetCursorPos(out var p);
            return new Vector2Int(p.X, p.Y);
#else
            Vector3 m = UnityEngine.Input.mousePosition;
            return new Vector2Int(Mathf.RoundToInt(m.x), Mathf.RoundToInt(Screen.height - m.y));
#endif
        }

        public Vector2 DesktopToUnityScreen(Vector2Int desktop)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsNative) return new Vector2(desktop.x, Screen.height - desktop.y);
            var p = new Win32Native.POINT { X = desktop.x, Y = desktop.y };
            Win32Native.ScreenToClient(_hwnd, ref p);
            Win32Native.GetClientRect(_hwnd, out var rc);
            return new Vector2(p.X, (rc.Bottom - rc.Top) - p.Y);
#else
            return new Vector2(desktop.x, Screen.height - desktop.y);
#endif
        }

        public Vector2Int GetPosition()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (IsNative && Win32Native.GetWindowRect(_hwnd, out var r))
                return new Vector2Int(r.Left, r.Top);
#endif
            return Vector2Int.zero;
        }

        public void MoveTo(Vector2Int pos)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsNative) return;
            Win32Native.SetWindowPos(_hwnd, IntPtr.Zero, pos.x, pos.y, 0, 0,
                Win32Native.SWP_NOSIZE | Win32Native.SWP_NOZORDER | Win32Native.SWP_NOACTIVATE);
#endif
        }

        /// <summary>同时改位置和尺寸（全局缩放）。Unity 收到 WM_SIZE 后自动重建后台缓冲区（resizableWindow = true）。</summary>
        public void SetBounds(Vector2Int pos, int widthPx, int heightPx)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsNative) return;
            SizePx = widthPx;
            WidthPx = widthPx;
            HeightPx = heightPx;
            Win32Native.SetWindowPos(_hwnd, IntPtr.Zero, pos.x, pos.y, widthPx, heightPx,
                Win32Native.SWP_NOZORDER | Win32Native.SWP_NOACTIVATE);
#endif
        }

        /// <summary>左键当前是否物理按下（用于拖动中丢失 MouseUp 的兜底）。</summary>
        public bool IsLeftButtonDown()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            bool swapped = Win32Native.GetSystemMetrics(Win32Native.SM_SWAPBUTTON) != 0;
            int vk = swapped ? Win32Native.VK_RBUTTON : Win32Native.VK_LBUTTON;
            return (Win32Native.GetAsyncKeyState(vk) & 0x8000) != 0;
#else
            return UnityEngine.Input.GetMouseButton(0);
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        static IntPtr FindUnityWindow()
        {
            uint pid = (uint)Process.GetCurrentProcess().Id;
            IntPtr found = IntPtr.Zero;
            var sb = new StringBuilder(256);
            Win32Native.EnumWindows((hWnd, _) =>
            {
                Win32Native.GetWindowThreadProcessId(hWnd, out uint wpid);
                if (wpid != pid) return true;
                sb.Length = 0;
                Win32Native.GetClassName(hWnd, sb, sb.Capacity);
                if (sb.ToString() == "UnityWndClass")
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }
#endif
    }
}
