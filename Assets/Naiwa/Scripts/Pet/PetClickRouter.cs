using System;
using Naiwa.Input;
using Naiwa.Platform;
using UnityEngine;

namespace Naiwa.Pet
{
    /// <summary>
    /// 点击/拖动/右键判定（§V.3.5）。统一使用全局钩子（或 Editor 模拟器）的鼠标事件，不依赖 Unity 的 Input。
    /// 左键按下且命中 → 按下待定；移动超过阈值 → 拖动（交给 WindowMover）；抬起时没超过阈值 → 点击。
    /// 右键按下且命中 → 请求右键菜单。位置统一用 GetCursorPos（与 ScreenToClient 同一 DPI 上下文）。
    /// </summary>
    public sealed class PetClickRouter
    {
        enum PressState { None, Pending, Dragging }

        readonly TransparentWindow _window;
        readonly WindowMover _mover;
        readonly PetHitTester _hitTester;
        readonly int _dragThresholdPx;

        PressState _state;
        Vector2Int _pressCursor;

        public event Action Clicked;
        public event Action DragStarted;
        public event Action<Vector2Int> DragEnded;
        public event Action ContextMenuRequested;

        public bool IsDragging => _state == PressState.Dragging;
        public bool IsPressPending => _state == PressState.Pending;

        public PetClickRouter(TransparentWindow window, WindowMover mover, PetHitTester hitTester, int dragThresholdPx)
        {
            _window = window;
            _mover = mover;
            _hitTester = hitTester;
            _dragThresholdPx = Mathf.Max(1, dragThresholdPx);
        }

        public bool IsCursorOverPet() =>
            _hitTester.HitUnityScreen(_window.DesktopToUnityScreen(_window.GetCursorDesktop()));

        /// <summary>处理一个鼠标事件。返回值：MouseDown 时是否命中角色（PetClickOnly 计数用）。</summary>
        public bool HandleEvent(in RawInputEvent e)
        {
            if (e.Kind == RawKind.MouseDown)
            {
                bool hit = IsCursorOverPet();
                if (!hit) return false;

                if (e.Button == MouseBtn.Left && _state == PressState.None)
                {
                    _state = PressState.Pending;
                    _pressCursor = _window.GetCursorDesktop();
                }
                else if (e.Button == MouseBtn.Right && _state == PressState.None)
                {
                    ContextMenuRequested?.Invoke();
                }
                return true;
            }

            if (e.Kind == RawKind.MouseUp && e.Button == MouseBtn.Left)
                Release();

            return false;
        }

        /// <summary>每帧调用：检测拖动阈值、推进拖动、兜底处理丢失的 MouseUp。</summary>
        public void Tick()
        {
            if (_state == PressState.None) return;

            var cursor = _window.GetCursorDesktop();
            if (_state == PressState.Pending)
            {
                var d = cursor - _pressCursor;
                if (d.x * d.x + d.y * d.y > _dragThresholdPx * _dragThresholdPx)
                {
                    _state = PressState.Dragging;
                    _mover.BeginDrag(_pressCursor);
                    DragStarted?.Invoke();
                }
            }

            if (_state == PressState.Dragging)
            {
                _mover.UpdateDrag(cursor);
                if (!_window.IsLeftButtonDown()) Release();
            }
        }

        public void Cancel()
        {
            if (_state == PressState.Dragging) Release();
            _state = PressState.None;
        }

        void Release()
        {
            var prev = _state;
            _state = PressState.None;
            if (prev == PressState.Pending)
            {
                Clicked?.Invoke();
            }
            else if (prev == PressState.Dragging)
            {
                var pos = _mover.EndDrag();
                DragEnded?.Invoke(pos);
            }
        }
    }
}
