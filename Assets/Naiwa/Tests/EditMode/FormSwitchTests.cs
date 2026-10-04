using System.Collections.Generic;
using Naiwa.Growth;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class FormSwitchTests
    {
        static (GrowthService g, FormSwitchService f) Create(long growth, FormId highest, FormId display, bool auto = true)
        {
            var g = new GrowthService(5000, 12000, growth, highest);
            return (g, new FormSwitchService(g, display, auto));
        }

        /// <summary>跑完一个过场（模拟烟雾时序：换形态那一刻 ApplySwap，结束 Complete）。</summary>
        static FormTransition Run(FormSwitchService f)
        {
            var t = f.TryBegin(true);
            Assert.IsTrue(t.HasValue, "应该有过场");
            f.ApplySwap();
            f.Complete();
            return t.Value;
        }

        [Test]
        public void HighestBig_AllSelectable()
        {
            var (_, f) = Create(12000, FormId.Big, FormId.Big);
            Assert.IsTrue(f.CanSelect(FormId.Egg));
            Assert.IsTrue(f.CanSelect(FormId.Small));
            Assert.IsTrue(f.CanSelect(FormId.Big));
        }

        [Test]
        public void HighestSmall_SwitchToBigRejected()
        {
            var (_, f) = Create(6000, FormId.Small, FormId.Small);
            Assert.IsFalse(f.CanSelect(FormId.Big));
            Assert.IsFalse(f.RequestSwitch(FormId.Big));
            Assert.IsFalse(f.TryBegin(true).HasValue);
        }

        [Test]
        public void SwitchToCurrent_NoOp()
        {
            var (_, f) = Create(12000, FormId.Big, FormId.Big);
            Assert.IsFalse(f.RequestSwitch(FormId.Big));
            Assert.IsFalse(f.TryBegin(true).HasValue);
        }

        [Test]
        public void Switch_ChangesDisplayOnly()
        {
            var (g, f) = Create(15000, FormId.Big, FormId.Big);
            Assert.IsTrue(f.RequestSwitch(FormId.Egg));
            var t = Run(f);
            Assert.AreEqual(FormTransitionKind.Switch, t.Kind);
            Assert.AreEqual(FormId.Big, t.From);
            Assert.AreEqual(FormId.Egg, t.To);
            Assert.AreEqual(FormId.Egg, f.DisplayForm);
            Assert.AreEqual(15000, g.Growth);
            Assert.AreEqual(FormId.Big, g.HighestForm);
        }

        [Test]
        public void AutoEvolve_DisplayEggHighestSmall_ReachBig_OneEggToBig()
        {
            var (g, f) = Create(6000, FormId.Small, FormId.Egg);
            g.Add(6000);
            var t = Run(f);
            Assert.AreEqual(FormId.Egg, t.From);
            Assert.AreEqual(FormId.Big, t.To);
            Assert.AreEqual(FormTransitionKind.Unlock, t.Kind);
            Assert.AreEqual(FormId.Big, f.DisplayForm);
            Assert.AreEqual(FormId.Big, g.HighestForm);
            Assert.IsFalse(f.TryBegin(true).HasValue, "只播一次");
        }

        [Test]
        public void AutoEvolve_FromEgg_Plus12000_SmallThenBig()
        {
            var (g, f) = Create(0, FormId.Egg, FormId.Egg);
            g.Add(12000);
            var t1 = Run(f);
            Assert.AreEqual((FormId.Egg, FormId.Small), (t1.From, t1.To));
            var t2 = Run(f);
            Assert.AreEqual((FormId.Small, FormId.Big), (t2.From, t2.To));
            Assert.AreEqual(FormId.Big, f.DisplayForm);
            Assert.IsFalse(f.TryBegin(true).HasValue);
        }

        [Test]
        public void HighestCommittedAtSwap_NotBefore()
        {
            var (g, f) = Create(4999, FormId.Egg, FormId.Egg);
            g.Add(1);
            var t = f.TryBegin(true);
            Assert.IsTrue(t.HasValue);
            Assert.AreEqual(FormId.Egg, g.HighestForm, "烟雾换形态之前进度条仍按奶蛋算（满格）");
            f.ApplySwap();
            Assert.AreEqual(FormId.Small, g.HighestForm);
            f.Complete();
        }

        [Test]
        public void AutoEvolveOff_DisplayUnchanged_NoticeEmitted()
        {
            var (g, f) = Create(0, FormId.Egg, FormId.Egg, auto: false);
            var notices = new List<FormId>();
            f.UnlockNotice += n => notices.Add(n);
            g.Add(12000);
            Assert.IsFalse(f.TryBegin(true).HasValue);
            Assert.AreEqual(FormId.Egg, f.DisplayForm);
            Assert.AreEqual(FormId.Big, g.HighestForm);
            CollectionAssert.AreEqual(new[] { FormId.Small, FormId.Big }, notices);
        }

        [Test]
        public void OldSave_Small4000_StaysSmall()
        {
            var (g, f) = Create(4000, FormId.Small, FormId.Small);
            Assert.AreEqual(FormId.Small, g.HighestForm);
            Assert.IsFalse(f.TryBegin(true).HasValue);
        }

        [Test]
        public void DebugSetSmall_Growth5000_BothSmall()
        {
            var (g, f) = Create(0, FormId.Egg, FormId.Egg);
            Assert.IsTrue(f.DebugSetForm(FormId.Small));
            Assert.AreEqual(5000, g.Growth);
            Run(f);
            Assert.AreEqual(FormId.Small, g.HighestForm);
            Assert.AreEqual(FormId.Small, f.DisplayForm);
            Assert.AreEqual(0f, GrowthProgress.Ratio(g));
        }

        [Test]
        public void DebugFromBigToEgg_DowngradeAllowed_BigNoLongerSelectable()
        {
            var (g, f) = Create(20000, FormId.Big, FormId.Big);
            f.DebugSetForm(FormId.Egg);
            var t = Run(f);
            Assert.AreEqual(FormId.Egg, t.To);
            Assert.AreEqual(0, g.Growth);
            Assert.AreEqual(FormId.Egg, g.HighestForm);
            Assert.AreEqual(FormId.Egg, f.DisplayForm);
            Assert.IsFalse(f.CanSelect(FormId.Big));
            Assert.IsFalse(f.CanSelect(FormId.Small));
        }

        [Test]
        public void Dragging_DefersTransition()
        {
            var (g, f) = Create(0, FormId.Egg, FormId.Egg);
            g.Add(5000);
            Assert.IsFalse(f.TryBegin(false).HasValue);
            Assert.IsTrue(f.TryBegin(true).HasValue);
        }

        [Test]
        public void Abort_KeepsUnlockPending()
        {
            var (g, f) = Create(0, FormId.Egg, FormId.Egg);
            g.Add(5000);
            f.TryBegin(true);
            f.Abort();
            Assert.AreEqual(FormId.Egg, g.HighestForm);
            Assert.IsTrue(f.TryBegin(true).HasValue);
        }
    }
}
