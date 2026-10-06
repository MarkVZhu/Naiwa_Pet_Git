using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Naiwa.UI
{
    /// <summary>
    /// 由全局钩子驱动的 uGUI 输入模块（v1.0 §7.2 / C13）。
    /// 悬停：每帧用 GetCursorPos 换算的客户区坐标做 RaycastAll，处理 enter/exit（窗口失焦、穿透时也有效）。
    /// 按下/抬起/滚轮：由 GameBootstrap 从钩子事件队列转发进来，不读 Unity 的 Input。
    /// Process() 不做任何事，所有驱动都来自 Tick / PointerDown / PointerUp / Scroll。
    /// </summary>
    public sealed class HookUIInputModule : BaseInputModule
    {
        PointerEventData _pointer;
        readonly List<RaycastResult> _hits = new List<RaycastResult>();
        Func<Vector2> _positionProvider;
        Vector2 _lastPos;

        /// <summary>光标当前是否在可交互 UI（raycastTarget）上。</summary>
        public bool IsOverUi { get; private set; }
        public GameObject Hovered => _pointer?.pointerEnter;
        /// <summary>左键在 UI 上按下还没抬起（例如正在拖滑块）。期间窗口不应穿透。</summary>
        public bool IsPressing => _pointer != null && (_pointer.pointerPress != null || _pointer.pointerDrag != null);

        public void SetPositionProvider(Func<Vector2> provider) => _positionProvider = provider;

        public override void Process() { }

        public override bool ShouldActivateModule() => enabled && gameObject.activeInHierarchy;

        PointerEventData Pointer
        {
            get
            {
                if (_pointer == null)
                    _pointer = new PointerEventData(eventSystem) { pointerId = PointerInputModule.kMouseLeftId, button = PointerEventData.InputButton.Left };
                return _pointer;
            }
        }

        /// <summary>每帧调用：更新悬停。</summary>
        public void Tick()
        {
            if (eventSystem == null || _positionProvider == null) return;
            var p = Pointer;
            var pos = _positionProvider();
            p.delta = pos - _lastPos;
            _lastPos = pos;
            p.position = pos;
            p.Reset();

            _hits.Clear();
            eventSystem.RaycastAll(p, _hits);
            var first = FindFirstRaycast(_hits);
            p.pointerCurrentRaycast = first;
            _hits.Clear();

            HandlePointerExitAndEnter(p, first.gameObject);
            IsOverUi = first.gameObject != null;
            ProcessDrag(p);
        }

        /// <summary>按住期间的拖动（滑块等 IDragHandler）：超过阈值后 beginDrag，之后每次移动发 drag。</summary>
        void ProcessDrag(PointerEventData p)
        {
            if (p.pointerDrag == null) return;
            if (!p.dragging)
            {
                bool moved = (p.position - p.pressPosition).sqrMagnitude >= eventSystem.pixelDragThreshold * eventSystem.pixelDragThreshold;
                if (p.useDragThreshold && !moved) return;
                ExecuteEvents.Execute(p.pointerDrag, p, ExecuteEvents.beginDragHandler);
                p.dragging = true;
                p.eligibleForClick = false;
            }
            if (p.delta.sqrMagnitude > 0f)
                ExecuteEvents.Execute(p.pointerDrag, p, ExecuteEvents.dragHandler);
        }

        /// <summary>左键按下。返回 true 表示 UI 接住了这次按下（宠物不再处理）。</summary>
        public bool PointerDown()
        {
            Tick();
            var p = Pointer;
            var go = p.pointerCurrentRaycast.gameObject;
            if (go == null) return false;

            p.eligibleForClick = true;
            p.delta = Vector2.zero;
            p.dragging = false;
            p.useDragThreshold = true;
            p.pressPosition = p.position;
            p.pointerPressRaycast = p.pointerCurrentRaycast;

            var pressHandler = ExecuteEvents.ExecuteHierarchy(go, p, ExecuteEvents.pointerDownHandler);
            if (pressHandler == null) pressHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);

            p.pointerPress = pressHandler;
            p.rawPointerPress = go;
            p.clickTime = Time.unscaledTime;
            p.clickCount = 1;

            // 只把拖动交给滑块；图鉴的 ScrollRect 保持「只用滚轮滚动」，格子点击不会因为手抖被吞掉
            var drag = ExecuteEvents.GetEventHandler<IDragHandler>(go);
            p.pointerDrag = drag != null && drag.GetComponent<Slider>() != null ? drag : null;
            if (p.pointerDrag != null)
                ExecuteEvents.Execute(p.pointerDrag, p, ExecuteEvents.initializePotentialDrag);
            return true;
        }

        /// <summary>左键抬起。返回 true 表示这次是 UI 的抬起。</summary>
        public bool PointerUp()
        {
            var p = Pointer;
            if (p.pointerPress == null && p.rawPointerPress == null && p.pointerDrag == null) return false;
            Tick();

            var current = p.pointerCurrentRaycast.gameObject;
            if (p.pointerPress != null) ExecuteEvents.Execute(p.pointerPress, p, ExecuteEvents.pointerUpHandler);

            var clickHandler = current != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(current) : null;
            if (p.pointerPress != null && p.pointerPress == clickHandler && p.eligibleForClick)
                ExecuteEvents.Execute(p.pointerPress, p, ExecuteEvents.pointerClickHandler);

            if (p.pointerDrag != null && p.dragging)
                ExecuteEvents.Execute(p.pointerDrag, p, ExecuteEvents.endDragHandler);

            p.eligibleForClick = false;
            p.pointerPress = null;
            p.rawPointerPress = null;
            p.pointerDrag = null;
            p.dragging = false;
            return true;
        }

        /// <summary>滚轮（一格 = 120）。返回是否落在 UI 上。</summary>
        public bool Scroll(int wheelDelta)
        {
            Tick();
            var p = Pointer;
            var go = p.pointerCurrentRaycast.gameObject;
            if (go == null) return false;
            p.scrollDelta = new Vector2(0f, wheelDelta / 120f);
            ExecuteEvents.ExecuteHierarchy(go, p, ExecuteEvents.scrollHandler);
            p.scrollDelta = Vector2.zero;
            return true;
        }
    }
}
