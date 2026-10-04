using Naiwa.UI;
using NUnit.Framework;
using UnityEngine;

namespace Naiwa.Tests
{
    public class HudLayoutTests
    {
        static readonly HudLayoutParams P = HudLayoutParams.Default; // 框中心 -24、高 26 → 底边 -37

        [Test]
        public void ClicksOn_GrowthOn_NoBubble()
        {
            var r = HudLayout.Compute(true, true, false);
            Assert.IsTrue(r.Clicks.HasValue);
            Assert.AreEqual(-37f, r.Clicks.Value.yMin, 1e-4f);
            Assert.AreEqual(-37f - 7f, r.Growth.Value.yMax, 1e-4f, "进度条在框下方 7px");
            Assert.AreEqual(120f, r.Growth.Value.width);
            Assert.IsFalse(r.Bubble.HasValue);
        }

        [Test]
        public void ClicksOn_GrowthOn_Bubble_LeftOfClicksBottomAligned()
        {
            var r = HudLayout.Compute(true, true, true);
            var b = r.Bubble.Value;
            Assert.AreEqual(r.Clicks.Value.xMin - 10f, b.xMax, 1e-4f);
            Assert.AreEqual(r.Clicks.Value.yMin, b.yMin, 1e-4f);
            Assert.IsTrue(r.BubbleBesideClicks);
            Assert.AreEqual(-44f, r.Growth.Value.yMax, 1e-4f);
        }

        [Test]
        public void ClicksOff_GrowthOn_NoBubble_GrowthTakesClicksCenterLine()
        {
            var r = HudLayout.Compute(false, true, false);
            Assert.IsFalse(r.Clicks.HasValue);
            Assert.AreEqual(P.clicksCenterY, r.Growth.Value.center.y, 1e-4f);
        }

        [Test]
        public void ClicksOff_GrowthOn_Bubble_BubbleAtClicksSpot_GrowthBelowIt()
        {
            var r = HudLayout.Compute(false, true, true);
            var b = r.Bubble.Value;
            Assert.AreEqual(0f, b.center.x, 1e-4f);
            Assert.AreEqual(-37f, b.yMin, 1e-4f, "底边与原框底边对齐");
            Assert.IsFalse(r.BubbleBesideClicks);
            Assert.AreEqual(b.yMin - 7f, r.Growth.Value.yMax, 1e-4f);
        }

        [Test]
        public void ClicksOn_GrowthOff_NoBubble()
        {
            var r = HudLayout.Compute(true, false, false);
            Assert.IsTrue(r.Clicks.HasValue);
            Assert.IsFalse(r.Growth.HasValue);
            Assert.IsFalse(r.Bubble.HasValue);
        }

        [Test]
        public void ClicksOn_GrowthOff_Bubble()
        {
            var r = HudLayout.Compute(true, false, true);
            Assert.IsTrue(r.Clicks.HasValue);
            Assert.IsTrue(r.Bubble.HasValue);
            Assert.IsTrue(r.BubbleBesideClicks);
            Assert.IsFalse(r.Growth.HasValue);
        }

        [Test]
        public void BothOff_NoBubble_AllEmpty()
        {
            var r = HudLayout.Compute(false, false, false);
            Assert.IsFalse(r.Clicks.HasValue);
            Assert.IsFalse(r.Growth.HasValue);
            Assert.IsFalse(r.Bubble.HasValue);
        }

        [Test]
        public void BothOff_Bubble_OnlyBubbleAtClicksSpot()
        {
            var r = HudLayout.Compute(false, false, true);
            Assert.IsFalse(r.Clicks.HasValue);
            Assert.IsFalse(r.Growth.HasValue);
            Assert.AreEqual(new Vector2(0f, -37f + 35f), r.Bubble.Value.center);
        }
    }
}
