using System;
using System.Collections.Generic;
using UnityEngine;

namespace Naiwa.Platform
{
    public struct ContextMenuItem
    {
        public int Id;
        public string Text;
        public bool Enabled;
        public bool Checked;
        public bool IsSeparator;

        public static ContextMenuItem Separator() => new ContextMenuItem { IsSeparator = true };
        public static ContextMenuItem Label(string text) => new ContextMenuItem { Text = text, Enabled = false };
        public static ContextMenuItem Command(int id, string text, bool isChecked = false) =>
            new ContextMenuItem { Id = id, Text = text, Enabled = true, Checked = isChecked };
    }

    /// <summary>
    /// 右键菜单（§3.4 子集）。打包后用 Win32 原生弹出菜单（TrackPopupMenuEx + TPM_RETURNCMD，阻塞主线程直到关闭，
    /// 钩子在独立线程不受影响）。Editor 下用 IMGUI 简易面板模拟（需在 OnGUI 中调用 DrawEditorFallback）。
    /// </summary>
    public sealed class NativeContextMenu
    {
        readonly TransparentWindow _window;

        List<ContextMenuItem> _editorItems;
        Action<int> _editorCallback;
        Vector2 _editorGuiPos;

        public bool IsOpen => _editorItems != null;
        public TransparentWindow Window => _window;

        public NativeContextMenu(TransparentWindow window)
        {
            _window = window;
        }

        /// <param name="onChosen">选中某项时回调其 Id；取消时不回调。</param>
        public void Show(List<ContextMenuItem> items, Action<int> onChosen)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_window.IsNative)
            {
                int cmd = ShowNative(items);
                if (cmd > 0) onChosen?.Invoke(cmd);
                return;
            }
#endif
            _editorItems = items;
            _editorCallback = onChosen;
            Vector3 m = UnityEngine.Input.mousePosition;
            _editorGuiPos = new Vector2(m.x, Screen.height - m.y);
        }

        public void Close()
        {
            _editorItems = null;
            _editorCallback = null;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        int ShowNative(List<ContextMenuItem> items)
        {
            IntPtr menu = Win32Native.CreatePopupMenu();
            if (menu == IntPtr.Zero) return 0;
            try
            {
                foreach (var item in items)
                {
                    if (item.IsSeparator)
                    {
                        Win32Native.AppendMenu(menu, Win32Native.MF_SEPARATOR, UIntPtr.Zero, null);
                        continue;
                    }
                    uint flags = Win32Native.MF_STRING;
                    if (!item.Enabled) flags |= Win32Native.MF_GRAYED;
                    if (item.Checked) flags |= Win32Native.MF_CHECKED;
                    Win32Native.AppendMenu(menu, flags, new UIntPtr((uint)Math.Max(0, item.Id)), item.Text ?? string.Empty);
                }

                var cursor = _window.GetCursorDesktop();
                IntPtr hwnd = _window.Handle;
                // 不先 SetForegroundWindow，点击菜单外部时菜单不会消失（MS 文档 TrackPopupMenu Remarks）。
                Win32Native.SetForegroundWindow(hwnd);
                int cmd = Win32Native.TrackPopupMenuEx(menu,
                    Win32Native.TPM_RETURNCMD | Win32Native.TPM_RIGHTBUTTON | Win32Native.TPM_NONOTIFY,
                    cursor.x, cursor.y, hwnd, IntPtr.Zero);
                Win32Native.PostMessage(hwnd, Win32Native.WM_NULL, IntPtr.Zero, IntPtr.Zero);
                return cmd;
            }
            finally
            {
                Win32Native.DestroyMenu(menu);
            }
        }
#endif

        /// <summary>Editor 兜底：在 OnGUI 中绘制简易菜单。</summary>
        public void DrawEditorFallback()
        {
            if (_editorItems == null) return;

            const float width = 340f, rowH = 22f, sepH = 8f, pad = 4f;
            float height = pad * 2;
            foreach (var it in _editorItems) height += it.IsSeparator ? sepH : rowH;

            var rect = new Rect(_editorGuiPos.x, _editorGuiPos.y, width, height);
            rect.x = Mathf.Clamp(rect.x, 0, Mathf.Max(0, Screen.width - width));
            rect.y = Mathf.Clamp(rect.y, 0, Mathf.Max(0, Screen.height - height));

            var e = Event.current;
            if (e.type == EventType.MouseDown && !rect.Contains(e.mousePosition))
            {
                Close();
                e.Use();
                return;
            }

            GUI.Box(rect, GUIContent.none);
            float y = rect.y + pad;
            foreach (var it in _editorItems)
            {
                if (it.IsSeparator)
                {
                    GUI.Box(new Rect(rect.x + 6, y + sepH / 2f - 1, width - 12, 1), GUIContent.none);
                    y += sepH;
                    continue;
                }

                var row = new Rect(rect.x + pad, y, width - pad * 2, rowH - 2);
                string text = (it.Checked ? "✓ " : "   ") + it.Text;
                bool prev = GUI.enabled;
                GUI.enabled = it.Enabled;
                if (GUI.Button(row, text) && it.Enabled)
                {
                    var cb = _editorCallback;
                    int id = it.Id;
                    Close();
                    cb?.Invoke(id);
                    GUI.enabled = prev;
                    return;
                }
                GUI.enabled = prev;
                y += rowH;
            }
        }
    }
}
