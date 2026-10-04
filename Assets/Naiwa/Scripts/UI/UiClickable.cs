using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Naiwa.UI
{
    /// <summary>通用的可点击 / 可悬停元素（事件来自 HookUIInputModule）。</summary>
    public sealed class UiClickable : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Clicked;
        public Action<bool> HoverChanged;

        public bool Hovered { get; private set; }

        public void OnPointerClick(PointerEventData e) => Clicked?.Invoke();

        public void OnPointerEnter(PointerEventData e)
        {
            Hovered = true;
            HoverChanged?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            Hovered = false;
            HoverChanged?.Invoke(false);
        }

        void OnDisable()
        {
            if (!Hovered) return;
            Hovered = false;
            HoverChanged?.Invoke(false);
        }
    }
}
