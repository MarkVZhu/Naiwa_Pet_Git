using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
#endif

namespace Naiwa.Platform
{
    /// <summary>
    /// 拖动移动窗口（§3.3）：保持按下时鼠标相对窗口的偏移；松手后限制在工作区内（至少 minVisibleFraction 可见）。
    /// Editor 下不能移动 Game 视图，只记录拖动状态。
    /// </summary>
    public sealed class WindowMover
    {
        readonly TransparentWindow _window;
        readonly float _minVisibleFraction;
        Vector2Int _grabOffset;

        public bool IsDragging { get; private set; }
        public float MinVisibleFraction => _minVisibleFraction;

        public WindowMover(TransparentWindow window, float minVisibleFraction)
        {
            _window = window;
            _minVisibleFraction = Mathf.Clamp01(minVisibleFraction);
        }

        public void BeginDrag(Vector2Int cursorDesktopAtPress)
        {
            IsDragging = true;
            _grabOffset = cursorDesktopAtPress - _window.GetPosition();
        }

        public void UpdateDrag(Vector2Int cursorDesktop)
        {
            if (!IsDragging) return;
            _window.MoveTo(cursorDesktop - _grabOffset);
        }

        /// <summary>松手：限制在工作区内，返回最终窗口位置。</summary>
        public Vector2Int EndDrag()
        {
            IsDragging = false;
            var clamped = ClampToWorkArea(_window.GetPosition());
            _window.MoveTo(clamped);
            return clamped;
        }

        /// <summary>启动时恢复位置；位置无效（显示器被拔掉等）时放主显示器工作区右下角。</summary>
        public Vector2Int RestoreOrDefault(bool hasSaved, int x, int y)
        {
            Vector2Int pos = hasSaved && IsOnAnyMonitor(new Vector2Int(x, y))
                ? ClampToWorkArea(new Vector2Int(x, y))
                : DefaultPosition();
            _window.MoveTo(pos);
            return pos;
        }

        Vector2Int DefaultPosition()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var mon = Win32Native.MonitorFromPoint(new Win32Native.POINT { X = 0, Y = 0 }, Win32Native.MONITOR_DEFAULTTOPRIMARY);
            var info = new Win32Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32Native.MONITORINFO)) };
            if (mon != IntPtr.Zero && Win32Native.GetMonitorInfo(mon, ref info))
                return new Vector2Int(info.rcWork.Right - _window.SizePx, info.rcWork.Bottom - _window.SizePx);
#endif
            return Vector2Int.zero;
        }

        bool IsOnAnyMonitor(Vector2Int pos)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var rect = new Win32Native.RECT { Left = pos.x, Top = pos.y, Right = pos.x + _window.SizePx, Bottom = pos.y + _window.SizePx };
            return Win32Native.MonitorFromRect(ref rect, Win32Native.MONITOR_DEFAULTTONULL) != IntPtr.Zero;
#else
            return true;
#endif
        }

        Vector2Int ClampToWorkArea(Vector2Int pos)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            int size = _window.SizePx;
            var rect = new Win32Native.RECT { Left = pos.x, Top = pos.y, Right = pos.x + size, Bottom = pos.y + size };
            var mon = Win32Native.MonitorFromRect(ref rect, Win32Native.MONITOR_DEFAULTTONEAREST);
            var info = new Win32Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32Native.MONITORINFO)) };
            if (mon == IntPtr.Zero || !Win32Native.GetMonitorInfo(mon, ref info)) return pos;

            int keep = Mathf.RoundToInt(size * _minVisibleFraction);
            var w = info.rcWork;
            int x = Mathf.Clamp(pos.x, w.Left - (size - keep), w.Right - keep);
            int y = Mathf.Clamp(pos.y, w.Top - (size - keep), w.Bottom - keep);
            return new Vector2Int(x, y);
#else
            return pos;
#endif
        }
    }
}
