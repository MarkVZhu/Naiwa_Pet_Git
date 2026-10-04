using UnityEngine;

namespace Naiwa.Platform
{
    /// <summary>
    /// 拖动移动窗口（§3.3 + v1.0 §7.3）：保持按下时鼠标相对窗口的偏移；松手后只约束宠物区 + HUD 区留在工作区内
    /// （至少 minVisibleFraction 可见），左右侧区允许伸出屏幕。Editor 下不能移动 Game 视图，只记录拖动状态。
    /// </summary>
    public sealed class WindowMover
    {
        readonly TransparentWindow _window;
        readonly WindowCanvasLayout _layout;
        readonly float _minVisibleFraction;
        Vector2Int _grabOffset;

        public bool IsDragging { get; private set; }
        public float MinVisibleFraction => _minVisibleFraction;

        public WindowMover(TransparentWindow window, WindowCanvasLayout layout, float minVisibleFraction)
        {
            _window = window;
            _layout = layout;
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
            var clamped = Clamp(_window.GetPosition());
            _window.MoveTo(clamped);
            return clamped;
        }

        /// <summary>启动时恢复位置；位置无效（显示器被拔掉等）时宠物区放主显示器工作区右下角。</summary>
        public Vector2Int RestoreOrDefault(bool hasSaved, int x, int y)
        {
            var saved = new Vector2Int(x, y);
            Vector2Int pos = hasSaved && _window.IsOnAnyMonitor(_layout.ToDesktop(_layout.CoreRect, saved))
                ? Clamp(saved)
                : _layout.DefaultWindowPos(_window.GetPrimaryWorkArea());
            _window.MoveTo(pos);
            return pos;
        }

        /// <summary>当前窗口位置下，图鉴 / 卡片应该放在哪一侧。</summary>
        public PanelSide ChooseSide()
        {
            var pos = _window.GetPosition();
            var work = _window.GetWorkAreaFor(_layout.ToDesktop(_layout.CoreRect, pos));
            return _layout.ChooseSide(pos, work);
        }

        Vector2Int Clamp(Vector2Int pos)
        {
            if (!_window.IsNative) return pos;
            var work = _window.GetWorkAreaFor(_layout.ToDesktop(_layout.CoreRect, pos));
            return _layout.ClampWindowPos(pos, work, _minVisibleFraction);
        }
    }
}
