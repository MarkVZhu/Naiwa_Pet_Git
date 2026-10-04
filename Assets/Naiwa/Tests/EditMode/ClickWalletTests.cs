using System.Collections.Generic;
using Naiwa.Economy;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class ClickWalletTests
    {
        [Test]
        public void EarnThreeTimes_BalanceThree()
        {
            var w = new ClickWallet();
            w.Earn(1); w.Earn(1); w.Earn(1);
            Assert.AreEqual(3, w.Balance);
            Assert.AreEqual(3, w.LifetimeEarned);
        }

        [Test]
        public void SpendMoreThanBalance_FailsAndBalanceUnchanged()
        {
            var w = new ClickWallet(10);
            Assert.IsFalse(w.TrySpend(11));
            Assert.AreEqual(10, w.Balance);
            Assert.AreEqual(0, w.LifetimeSpent);
        }

        [Test]
        public void SpendExactBalance_LeavesZero()
        {
            var w = new ClickWallet(2000);
            Assert.IsTrue(w.TrySpend(2000));
            Assert.AreEqual(0, w.Balance);
            Assert.AreEqual(2000, w.LifetimeSpent);
        }

        [Test]
        public void EarnZeroOrNegative_Ignored()
        {
            var w = new ClickWallet(5);
            int events = 0;
            w.Changed += (_, __) => events++;
            w.Earn(0);
            w.Earn(-5);
            Assert.AreEqual(5, w.Balance);
            Assert.AreEqual(0, events);
        }

        [Test]
        public void ChangedEvent_ReportsOldAndNew()
        {
            var w = new ClickWallet(100);
            var log = new List<(long, long)>();
            w.Changed += (o, n) => log.Add((o, n));
            w.Earn(5);
            w.TrySpend(50);
            CollectionAssert.AreEqual(new[] { (100L, 105L), (105L, 55L) }, log);
        }
    }
}
