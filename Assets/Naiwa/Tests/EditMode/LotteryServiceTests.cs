using System;
using System.Collections.Generic;
using Naiwa.Content;
using Naiwa.Core;
using Naiwa.Economy;
using Naiwa.Growth;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class LotteryServiceTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);

        sealed class SeqRandom : IRandom
        {
            readonly Random _r;
            public SeqRandom(int seed) { _r = new Random(seed); }
            public int Next(int max) => _r.Next(max);
        }

        sealed class Rig
        {
            public ClickWallet Wallet;
            public UnlockService Unlocks;
            public LotteryService Lottery;
            public int Saves;
            public FormId Form = FormId.Egg;
        }

        static Rig Create(long balance, string json = null, LotteryPoolScope scope = LotteryPoolScope.All, int seed = 1)
        {
            var catalog = TestContent.Catalog(json ?? TestContent.SampleJson);
            var rig = new Rig { Wallet = new ClickWallet(balance), Unlocks = new UnlockService(catalog, null) };
            rig.Unlocks.ApplyDefaults();
            rig.Lottery = new LotteryService(rig.Wallet, catalog, rig.Unlocks, new SeqRandom(seed), () => rig.Form,
                () => rig.Saves++, 2000, TimeSpan.FromMinutes(30), scope);
            return rig;
        }

        [Test]
        public void Balance1999_NotEnoughClicks_ShortOne()
        {
            var s = Create(1999).Lottery.GetStatus(T0);
            Assert.AreEqual(LotteryState.NotEnoughClicks, s.State);
            Assert.AreEqual(1, s.ClicksShort);
        }

        [Test]
        public void Balance2000_NoCooldown_Ready()
        {
            Assert.AreEqual(LotteryState.Ready, Create(2000).Lottery.GetStatus(T0).State);
        }

        [Test]
        public void AfterDraw_BalanceZero_Cooldown30Min()
        {
            var rig = Create(2000);
            var r = rig.Lottery.TryDraw(T0);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0, rig.Wallet.Balance);
            var s = rig.Lottery.GetStatus(T0);
            Assert.AreEqual(LotteryState.Cooldown, s.State);
            Assert.AreEqual(TimeSpan.FromMinutes(30), s.CooldownLeft);
            Assert.AreEqual(2000, s.ClicksShort, "冷却中也填实际差值");
        }

        [Test]
        public void CooldownBoundary_2959StillCooldown_3000DependsOnBalance()
        {
            var rig = Create(4000);
            rig.Lottery.TryDraw(T0);
            Assert.AreEqual(LotteryState.Cooldown, rig.Lottery.GetStatus(T0.AddMinutes(29).AddSeconds(59)).State);
            Assert.AreEqual(LotteryState.Ready, rig.Lottery.GetStatus(T0.AddMinutes(30)).State);

            var poor = Create(2000);
            poor.Lottery.TryDraw(T0);
            Assert.AreEqual(LotteryState.NotEnoughClicks, poor.Lottery.GetStatus(T0.AddMinutes(30)).State);
        }

        [Test]
        public void PoolEmpty_TakesPriorityOverEverything()
        {
            var rig = Create(0);
            rig.Unlocks.UnlockAll();
            var s = rig.Lottery.GetStatus(T0);
            Assert.AreEqual(LotteryState.PoolEmpty, s.State);
        }

        [Test]
        public void DrawUntilEmpty_EachLotteryEmoteExactlyOnce()
        {
            var rig = Create(100000);
            var got = new List<string>();
            var now = T0;
            while (true)
            {
                var s = rig.Lottery.GetStatus(now);
                if (s.State == LotteryState.PoolEmpty) break;
                Assert.AreEqual(LotteryState.Ready, s.State);
                var r = rig.Lottery.TryDraw(now);
                Assert.IsTrue(r.Ok);
                Assert.IsFalse(got.Contains(r.EmoteId), "不会抽到已解锁的");
                got.Add(r.EmoteId);
                now = now.AddMinutes(30);
            }
            CollectionAssert.AreEquivalent(new[] { "egg_bath", "small_heart", "big_fall" }, got);
        }

        [Test]
        public void Weights3To1_Ratio70To80Percent()
        {
            string json = TestContent.SampleJson
                .Replace(@"""unlock"": ""Lottery"", ""lotteryWeight"": 1", @"""unlock"": ""Lottery"", ""lotteryWeight"": 3")
                .Replace(@"""folder"": ""big_emo_fall"",  ""fps"": 24, ""unlock"": ""Lottery""", @"""folder"": ""big_emo_fall"",  ""fps"": 24, ""unlock"": ""Default""");
            // 池子：egg_bath(3) + small_heart(1)
            int bath = 0;
            for (int i = 0; i < 4000; i++)
            {
                var rig = Create(2000, json, seed: i);
                if (rig.Lottery.TryDraw(T0).EmoteId == "egg_bath") bath++;
            }
            Assert.That(bath / 4000.0, Is.InRange(0.70, 0.80));
        }

        [Test]
        public void CurrentFormOnly_NeverDrawsOtherForms()
        {
            var rig = Create(100000, scope: LotteryPoolScope.CurrentFormOnly);
            rig.Form = FormId.Small;
            var r = rig.Lottery.TryDraw(T0);
            Assert.AreEqual("small_heart", r.EmoteId);
            Assert.AreEqual(LotteryState.PoolEmpty, rig.Lottery.GetStatus(T0.AddHours(1)).State);
        }

        [Test]
        public void CurrentFormFirst_PrefersCurrentThenFallsBack()
        {
            var rig = Create(100000, scope: LotteryPoolScope.CurrentFormFirst);
            rig.Form = FormId.Big;
            Assert.AreEqual("big_fall", rig.Lottery.TryDraw(T0).EmoteId);
            var next = rig.Lottery.TryDraw(T0.AddMinutes(30));
            Assert.IsTrue(next.Ok);
            Assert.AreNotEqual("big_fall", next.EmoteId);
        }

        [Test]
        public void ClockRolledBack_RemainingNeverExceedsCooldown()
        {
            var rig = Create(2000);
            rig.Lottery.TryDraw(T0);
            var left = rig.Lottery.GetStatus(T0.AddDays(-3)).CooldownLeft;
            Assert.LessOrEqual(left, TimeSpan.FromMinutes(30));
            Assert.Greater(left, TimeSpan.Zero);
        }

        [Test]
        public void TryDraw_SavesBeforeReturning()
        {
            var rig = Create(2000);
            int savesWhenDrawn = -1;
            rig.Lottery.Drawn += _ => savesWhenDrawn = rig.Saves;
            var r = rig.Lottery.TryDraw(T0);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(1, rig.Saves);
            Assert.AreEqual(1, savesWhenDrawn, "Drawn 事件（动画）之前已落盘");
            Assert.IsTrue(rig.Unlocks.IsUnlocked(r.EmoteId));
            Assert.AreEqual(1, rig.Lottery.DrawCount);
        }

        [Test]
        public void TryDraw_NotReady_FailsWithoutSpending()
        {
            var rig = Create(1500);
            Assert.IsFalse(rig.Lottery.TryDraw(T0).Ok);
            Assert.AreEqual(1500, rig.Wallet.Balance);
            Assert.AreEqual(0, rig.Saves);
        }

        [Test]
        public void SavedCooldown_RestoredFromUnixMs()
        {
            var catalog = TestContent.Catalog();
            var unlocks = new UnlockService(catalog, null);
            long next = (long)(T0.AddMinutes(10) - DateTime.UnixEpoch).TotalMilliseconds;
            var lottery = new LotteryService(new ClickWallet(5000), catalog, unlocks, new SeqRandom(1), () => FormId.Egg, null,
                2000, TimeSpan.FromMinutes(30), LotteryPoolScope.All, next, 2);
            var s = lottery.GetStatus(T0);
            Assert.AreEqual(LotteryState.Cooldown, s.State);
            Assert.AreEqual(TimeSpan.FromMinutes(10), s.CooldownLeft);
            Assert.AreEqual(next, lottery.NextAvailableUnixMs);
        }
    }
}
