using System;
using System.Collections.Generic;
using UnityEngine;

namespace Naiwa.Input
{
    /// <summary>
    /// 用 Unity 的 Input 生成与全局钩子相同结构的事件。
    /// Editor 下（C3）：只在 Game 视图有焦点时生效，模拟键鼠。
    /// 打包后：仅在鼠标钩子安装失败时作为鼠标兜底（命中角色时窗口不穿透，Unity 能收到点击）。
    /// 坐标约定：ScreenX/ScreenY 为"桌面坐标"（y 向下），Editor 下即 Game 视图坐标翻转 y。
    /// </summary>
    public sealed class EditorInputSimulator : IInputSource
    {
        static readonly KeyCode[] s_keys = BuildKeyList();

        readonly bool _simulateKeyboard;
        readonly bool _requireFocus;
        readonly Func<long> _clockMs;

        public EditorInputSimulator(bool simulateKeyboard, bool requireFocus, Func<long> clockMs)
        {
            _simulateKeyboard = simulateKeyboard;
            _requireFocus = requireFocus;
            _clockMs = clockMs;
        }

        public void Drain(List<RawInputEvent> into)
        {
            if (_requireFocus && !Application.isFocused) return;

            long now = _clockMs();
            Vector3 mouse = UnityEngine.Input.mousePosition;
            bool mouseInView = mouse.x >= 0 && mouse.y >= 0 && mouse.x <= Screen.width && mouse.y <= Screen.height;
            int sx = Mathf.RoundToInt(mouse.x);
            int sy = Mathf.RoundToInt(Screen.height - mouse.y);

            for (int b = 0; b < 3; b++)
            {
                var btn = b == 0 ? MouseBtn.Left : b == 1 ? MouseBtn.Right : MouseBtn.Middle;
                if (mouseInView && UnityEngine.Input.GetMouseButtonDown(b))
                    into.Add(new RawInputEvent { Kind = RawKind.MouseDown, Button = btn, TimestampMs = now, ScreenX = sx, ScreenY = sy });
                if (UnityEngine.Input.GetMouseButtonUp(b))
                    into.Add(new RawInputEvent { Kind = RawKind.MouseUp, Button = btn, TimestampMs = now, ScreenX = sx, ScreenY = sy });
            }

            if (!_simulateKeyboard) return;
            bool scanUps = AnyKeyUp();
            if (!UnityEngine.Input.anyKeyDown && !scanUps) return;

            foreach (var key in s_keys)
            {
                if (UnityEngine.Input.GetKeyDown(key))
                    into.Add(new RawInputEvent { Kind = RawKind.KeyDown, KeyId = (int)key, TimestampMs = now });
                else if (UnityEngine.Input.GetKeyUp(key))
                    into.Add(new RawInputEvent { Kind = RawKind.KeyUp, KeyId = (int)key, TimestampMs = now });
            }
        }

        bool _anyHeldLastFrame;

        bool AnyKeyUp()
        {
            // anyKey 从 true 变 false 或仍有键按住时都需要扫描 KeyUp
            bool held = UnityEngine.Input.anyKey;
            bool scan = held || _anyHeldLastFrame;
            _anyHeldLastFrame = held;
            return scan;
        }

        public void Dispose() { }

        static KeyCode[] BuildKeyList()
        {
            var list = new List<KeyCode>();
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if (k == KeyCode.None) continue;
                if (k >= KeyCode.Mouse0) continue; // 鼠标与手柄由别处处理
                if (!list.Contains(k)) list.Add(k);
            }
            return list.ToArray();
        }
    }
}
