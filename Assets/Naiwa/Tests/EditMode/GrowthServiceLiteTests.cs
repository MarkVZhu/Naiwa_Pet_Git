using Naiwa.Growth;
using NUnit.Framework;

namespace Naiwa.Tests
{
    /// <summary>成长值与形态解锁（v1.0 阈值 5,000 / 12,000）。</summary>
    public class GrowthServiceLiteTests
    {
        static GrowthService Create() => new GrowthService(5000, 12000);

        [Test]
        public void Growth4999_StaysEgg()
        {
            var g = Create();
            g.Add(4999);
            Assert.AreEqual(FormId.Egg, g.HighestForm);
            Assert.IsFalse(g.HasPendingUnlock);
        }

        [Test]
        public void Growth5000_PendingSmall_CommitUnlocks()
        {
            var g = Create();
            g.Add(5000);
            Assert.IsTrue(g.HasPendingUnlock);
            Assert.AreEqual(FormId.Egg, g.HighestForm, "动画换形态之前不提交");
            g.CommitUnlock();
            Assert.AreEqual(FormId.Small, g.HighestForm);
            Assert.IsFalse(g.HasPendingUnlock);
        }

        [Test]
        public void Add30000FromZero_UnlocksOneStepAtATime()
        {
            var g = Create();
            g.Add(30000);
            g.CommitUnlock();
            Assert.AreEqual(FormId.Small, g.HighestForm, "不能跳过奶蛋→小奶蛙");
            Assert.IsTrue(g.HasPendingUnlock);
            g.CommitUnlock();
            Assert.AreEqual(FormId.Big, g.HighestForm);
            Assert.IsFalse(g.HasPendingUnlock);
        }

        [Test]
        public void AfterBig_AddingNeverPending()
        {
            var g = new GrowthService(5000, 12000, 12000, FormId.Big);
            g.Add(100000);
            Assert.IsFalse(g.HasPendingUnlock);
            Assert.AreEqual(112000, g.Growth);
            Assert.IsNull(g.NextThreshold);
        }

        [Test]
        public void OldSave_HigherFormThanThresholdAllows_NeverDowngrades()
        {
            var g = new GrowthService(5000, 12000, 4000, FormId.Small);
            Assert.AreEqual(FormId.Small, g.HighestForm);
            Assert.IsFalse(g.HasPendingUnlock);
        }

        [Test]
        public void GrowthIsLong_NoOverflow()
        {
            var g = new GrowthService(5000, 12000, long.MaxValue - 1, FormId.Big);
            g.Add(10);
            Assert.AreEqual(long.MaxValue, g.Growth);
        }
    }
}
