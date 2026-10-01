using Naiwa.Pet;

namespace Naiwa.Platform
{
    /// <summary>
    /// §3.2：每帧检测鼠标是否命中角色；未命中 → 加 WS_EX_TRANSPARENT（点击穿透）。
    /// 状态变化时才调用 SetWindowLongPtr（由 TransparentWindow 保证）。拖动/按下待定/菜单期间强制不穿透。
    /// </summary>
    public sealed class ClickThroughController
    {
        readonly TransparentWindow _window;
        readonly PetHitTester _hitTester;

        public bool IsOverPet { get; private set; }

        public ClickThroughController(TransparentWindow window, PetHitTester hitTester)
        {
            _window = window;
            _hitTester = hitTester;
        }

        public void Tick(bool forceOpaque)
        {
            if (!_window.IsNative) return;
            IsOverPet = _hitTester.HitUnityScreen(_window.DesktopToUnityScreen(_window.GetCursorDesktop()));
            _window.SetClickThrough(!(IsOverPet || forceOpaque));
        }
    }
}
