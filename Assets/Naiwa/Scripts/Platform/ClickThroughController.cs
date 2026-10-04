using System;
using Naiwa.Pet;

namespace Naiwa.Platform
{
    /// <summary>
    /// §3.2 + v1.0 §7.4：不穿透 = 光标落在宠物轮廓 ∪ 可交互 UI（气泡、图鉴、揭晓卡片）上。
    /// 其余区域加 WS_EX_TRANSPARENT（点击穿透）。状态变化时才调用 SetWindowLongPtr（由 TransparentWindow 保证）。
    /// 拖动 / 按下待定 / 菜单期间强制不穿透。
    /// </summary>
    public sealed class ClickThroughController
    {
        readonly TransparentWindow _window;
        readonly PetHitTester _hitTester;
        readonly Func<bool> _isOverInteractiveUi;

        public bool IsOverPet { get; private set; }
        public bool IsOverUi { get; private set; }

        public ClickThroughController(TransparentWindow window, PetHitTester hitTester, Func<bool> isOverInteractiveUi)
        {
            _window = window;
            _hitTester = hitTester;
            _isOverInteractiveUi = isOverInteractiveUi ?? (() => false);
        }

        public void Tick(bool forceOpaque)
        {
            if (!_window.IsNative) return;
            IsOverPet = _hitTester.HitUnityScreen(_window.DesktopToUnityScreen(_window.GetCursorDesktop()));
            IsOverUi = _isOverInteractiveUi();
            _window.SetClickThrough(!(IsOverPet || IsOverUi || forceOpaque));
        }
    }
}
