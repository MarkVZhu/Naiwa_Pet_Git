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
        public void Clamp_OnlyConstrainsCoreArea_SidesMayLeaveScreen()
        {
            var pos = L.ClampWindowPos(new Vector2Int(1920 - 380 - 380 + 100, 500), Work, 1f);
            Assert.AreEqual(1920 - 380 - 380, pos.x, "宠物区完整留在屏幕内");
            var left = L.ClampWindowPos(new Vector2Int(-380, 0), Work, 1f);
            Assert.AreEqual(-380, left.x, "左侧区可以完全伸出屏幕");
        }
    }
}
