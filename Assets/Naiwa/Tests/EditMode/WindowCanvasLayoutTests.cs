using Naiwa.Core;
using Naiwa.Platform;
using NUnit.Framework;
using UnityEngine;

namespace Naiwa.Tests
{
    public class WindowCanvasLayoutTests
    {
        static readonly WindowCanvasLayout L = new WindowCanvasLayout(new WindowConfig());
        static readonly RectInt Work = new RectInt(0, 0, 1920, 1040);

        [Test]
        public void Size_1140x510_PetAreaSameAsV01_PlusHeadroom()
        {
            Assert.AreEqual(1140, L.Width);
            Assert.AreEqual(510, L.Height);
            Assert.AreEqual(new RectInt(380, 0, 380, 510), L.CoreRect);
            var cfg = new WindowConfig();
            Assert.AreEqual(370f, cfg.FeetFromTopPx, 1e-4f, "脚底比 v1.0 首版下移 headroomPx");
            Assert.AreEqual(2.3f, cfg.CameraY, 1e-4f);
            Assert.AreEqual(5.1f, cfg.OrthographicSize, 1e-4f);
        }

        [Test]
        public void NoHeadroom_MatchesPreviousLayout()
        {
            var cfg = new WindowConfig { headroomPx = 0 };
            Assert.AreEqual(470, cfg.WindowHeightPx);
            Assert.AreEqual(1.9f, cfg.CameraY, 1e-4f);
        }

        [Test]
        public void ChooseSide_RightPreferred()
        {
            Assert.AreEqual(PanelSide.Right, L.ChooseSide(new Vector2Int(200, 300), Work));
        }

        [Test]
        public void ChooseSide_NearRightEdge_Left()
        {
            var pos = L.DefaultWindowPos(Work); // 宠物区贴右下角，右侧区伸出屏幕
            Assert.AreEqual(PanelSide.Left, L.ChooseSide(pos, Work));
        }

        [Test]
        public void ChooseSide_BothClipped_PicksLargerVisible()
        {
            var tight = new RectInt(0, 0, 1000, 1040);
            // 窗口 x=-200：左侧区可见 180/380，右侧区 x=560..940 全可见
            Assert.AreEqual(PanelSide.Right, L.ChooseSide(new Vector2Int(-200, 0), tight));
            // 窗口 x=-100：左侧区可见 280/380，右侧区 660..1040 可见 340/380
            Assert.AreEqual(PanelSide.Right, L.ChooseSide(new Vector2Int(-100, 0), tight));
            // 窗口 x=-300：左侧区 80/380；右侧区 460..840 全可见
            Assert.AreEqual(PanelSide.Right, L.ChooseSide(new Vector2Int(-300, 0), tight));
            var narrow = new RectInt(0, 0, 900, 1040);
            // 窗口 x=-150：左 230/380，右 610..990 可见 290/380 → 右
            Assert.AreEqual(PanelSide.Right, L.ChooseSide(new Vector2Int(-150, 0), narrow));
            // 窗口 x=-50：左 330/380，右 710..1090 可见 190/380 → 左
            Assert.AreEqual(PanelSide.Left, L.ChooseSide(new Vector2Int(-50, 0), narrow));
        }

        [Test]
        public void Scale1_IdenticalToUnscaled()
        {
            var s = new WindowCanvasLayout(new WindowConfig(), 1f);
            Assert.AreEqual(L.Width, s.Width);
            Assert.AreEqual(L.Height, s.Height);
            Assert.AreEqual(L.CoreRect, s.CoreRect);
            Assert.AreEqual(new Vector2(570f, 370f), s.Feet);
        }

        [TestCase(0.5f, 570, 255, 190)]
        [TestCase(1.5f, 1710, 765, 570)]
        public void Scaled_WindowAndAreasScaleTogether(float scale, int w, int h, int side)
        {
            var s = new WindowCanvasLayout(new WindowConfig(), scale);
            Assert.AreEqual(w, s.Width);
            Assert.AreEqual(h, s.Height);
            Assert.AreEqual(side, s.SideWidth);
            Assert.AreEqual(w - side * 2, s.PetArea);
            Assert.AreEqual(370f * scale, s.Feet.y, 0.5f);
        }

        [Test]
        public void Rescale_KeepsFeetOnDesktop()
        {
            var big = new WindowCanvasLayout(new WindowConfig(), 1.5f);
            var small = new WindowCanvasLayout(new WindowConfig(), 0.5f);
            var pos = new Vector2Int(400, 300);
            Vector2 feet = pos + L.Feet;

            var p1 = big.RescaledPosition(pos, L);
            Assert.AreEqual(feet.x, p1.x + big.Feet.x, 0.5f);
            Assert.AreEqual(feet.y, p1.y + big.Feet.y, 0.5f);

            var p2 = small.RescaledPosition(p1, big);
            Assert.AreEqual(feet.x, p2.x + small.Feet.x, 1f);
            Assert.AreEqual(feet.y, p2.y + small.Feet.y, 1f);

            Assert.AreEqual(pos, L.RescaledPosition(p2, small), "缩放回 100% 回到原位");
        }

        [TestCase(1f, 1f)]
        [TestCase(0.2f, 0.5f)]
        [TestCase(9f, 1.5f)]
        [TestCase(1.234f, 1.25f)]
        [TestCase(0.74f, 0.75f)]
        [TestCase(0f, 1f)]
        [TestCase(float.NaN, 1f)]
        public void ClampScale_RangeAndStep(float input, float expected)
        {
            Assert.AreEqual(expected, new WindowConfig().ClampScale(input), 1e-4f);
        }

        [Test]
        public void Clamp_OnlyConstrainsCoreArea_SidesMayLeaveScreen()
        {
            var pos = L.ClampWindowPos(new Vector2Int(1920 - 380 - 380 + 100, 500), Work, 1f);
            Assert.AreEqual(1920 - 380 - 380, pos.x, "宠物区完整留在屏幕内");
            var left = L.ClampWindowPos(new Vector2Int(-380, 0), Work, 1f);
            Assert.AreEqual(-380, left.x, "左侧区可以完全伸出屏幕");
        }
    }
}
