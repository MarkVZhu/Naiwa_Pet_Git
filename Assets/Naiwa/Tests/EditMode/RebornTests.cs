using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Naiwa.Content;
using Naiwa.Core;
using Naiwa.Economy;
using Naiwa.Growth;
using Naiwa.Save;
using Naiwa.UI;
using NUnit.Framework;

namespace Naiwa.Tests
{
    /// <summary>v2.0 重生：重生表情、「大奶蛙[可重生]」、重生流程。</summary>
    public class RebornTests
    {
        // 与 v2.0 content.json 同构：egg_angel(3) / small_cupid(2) / big_god(1) 为重生表情
        const string Json = @"{
  ""schemaVersion"": 2,
  ""forms"": [
    { ""form"": ""Egg"", ""idle"": { ""folder"": ""egg_Idle"", ""fps"": 12 }, ""emotes"": [
        { ""id"": ""egg_drink"", ""folder"": ""egg_emo_drink"", ""fps"": 24, ""unlock"": ""Default"" },
        { ""id"": ""egg_sleep"", ""folder"": ""egg_sleep"", ""fps"": 24, ""unlock"": ""Lottery"", ""lotteryWeight"": 3 },
        { ""id"": ""egg_angel"", ""folder"": ""egg_angel"", ""fps"": 24, ""unlock"": ""reborn"", ""lotteryWeight"": 3 } ] },
    { ""form"": ""Small"", ""idle"": { ""folder"": ""small_idle"", ""fps"": 24 }, ""emotes"": [
        { ""id"": ""small_heart"", ""folder"": ""small_heart"", ""fps"": 24, ""unlock"": ""Default"" },
        { ""id"": ""small_cupid"", ""folder"": ""small_cupid"", ""fps"": 24, ""unlock"": ""Reborn"", ""lotteryWeight"": 2 } ] },
    { ""form"": ""Big"", ""idle"": { ""folder"": ""big_Idle"", ""fps"": 12 }, ""emotes"": [
        { ""id"": ""big_laugh"", ""folder"": ""big_emo_laugh"", ""fps"": 24, ""unlock"": ""Default"" },
        { ""id"": ""big_god"", ""folder"": ""big_god"", ""fps"": 20, ""unlock"": ""Reborn"", ""lotteryWeight"": 1 } ] }
  ]
}";

        sealed class FixedRandom : IRandom
        {
            readonly int _v;
            public FixedRandom(int v) { _v = v; }
            public int Next(int max) => Math.Min(_v, max - 1);
        }

        static EmoteCatalog Catalog(string json = Json) => TestContent.Catalog(json);

        static UnlockService Unlocks(EmoteCatalog c)
        {
            var u = new UnlockService(c, null);
            u.ApplyDefaults();
            return u;
        }

        // ---------- 内容配置 ----------

        [Test]
        public void UnlockReborn_ParsedCaseInsensitive_NotInLotteryPool()
        {
            var r = TestContent.Parse(Json);
            Assert.IsFalse(r.HasErrors, string.Join("\n", r.Errors));
            var angel = r.AllEmotes.First(e => e.Id == "egg_angel");
            Assert.AreEqual(UnlockMode.Reborn, angel.Unlock, "\"reborn\" 小写也能识别");
            Assert.IsTrue(angel.IsReborn);
            Assert.IsFalse(angel.InLotteryPool);
            Assert.IsFalse(r.Warnings.Exists(w => w.Contains("egg_angel")));
        }

        [Test]
        public void RebornEmotes_NotDefaultUnlocked_AndNeverDrawnByLottery()
        {
            var c = Catalog();
            var u = Unlocks(c);
            Assert.IsFalse(u.IsUnlocked("egg_angel"));
            var lottery = new LotteryService(new ClickWallet(0), c, u, new FixedRandom(0), () => FormId.Egg, null, 1, TimeSpan.Zero, LotteryPoolScope.All);
            CollectionAssert.AreEquivalent(new[] { "egg_sleep" }, lottery.GetPool().Select(e => e.Id));
        }

        [Test]
        public void ShippedContentJson_HasThreeRebornEmotes()
        {
            string path = Path.Combine(UnityEngine.Application.streamingAssetsPath, "content", "content.json");
            var r = ContentConfig.Parse(File.ReadAllText(path), n => n);
            CollectionAssert.AreEquivalent(new[] { "big_god", "egg_angel", "small_cupid" },
                r.AllEmotes.Where(e => e.IsReborn).Select(e => e.Id));
        }

        // ---------- 重生奖励 ----------

        [Test]
        public void Reward_HighestWeightFirst_ThenNext()
        {
            var c = Catalog();
            var u = Unlocks(c);
            var order = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                var pick = RebornRewards.Pick(c, u, new FixedRandom(0));
                if (pick == null) break;
                order.Add(pick.Id);
                u.Unlock(pick.Id, UnlockSource.Reborn);
            }
            CollectionAssert.AreEqual(new[] { "egg_angel", "small_cupid", "big_god" }, order, "lotteryWeight 3 → 2 → 1");
            Assert.IsNull(RebornRewards.Pick(c, u, new FixedRandom(0)), "全部解锁后没有奖励");
        }

        [Test]
        public void Reward_SameWeight_RandomAmongTopOnly()
        {
            string json = Json.Replace(@"""unlock"": ""Reborn"", ""lotteryWeight"": 2", @"""unlock"": ""Reborn"", ""lotteryWeight"": 3");
            var c = Catalog(json);
            var u = Unlocks(c);
            var seen = new HashSet<string>();
            for (int r = 0; r < 2; r++) seen.Add(RebornRewards.Pick(c, u, new FixedRandom(r)).Id);
            CollectionAssert.AreEquivalent(new[] { "egg_angel", "small_cupid" }, seen, "同为 3 的两个都可能拿到，权重 1 的不会");
        }

        [Test]
        public void Reward_AlreadyUnlockedSkipped()
        {
            var c = Catalog();
            var u = Unlocks(c);
            u.Unlock("egg_angel", UnlockSource.Lottery); // 旧存档里抽到过
            Assert.AreEqual("small_cupid", RebornRewards.Pick(c, u, new FixedRandom(0)).Id);
        }

        // ---------- 「大奶蛙[可重生]」 ----------

        static GrowthService G(long growth, FormId highest) => new GrowthService(3000, 9000, growth, highest, 15000);

        [Test]
        public void CanReborn_OnlyBigAndGrowthReached()
        {
            Assert.IsFalse(G(14999, FormId.Big).CanReborn);
            Assert.IsTrue(G(15000, FormId.Big).CanReborn);
            Assert.IsFalse(G(20000, FormId.Small).CanReborn, "成长值够了但还没进化成大奶蛙");
            Assert.IsFalse(new GrowthService(3000, 9000, 99999, FormId.Big).CanReborn, "未配置重生阈值时不启用");
        }

        [Test]
        public void ProgressBar_BigShowsProgressToReborn_MaxWhenReady()
        {
            var g = G(12000, FormId.Big);
            Assert.AreEqual(0.5f, GrowthProgress.Ratio(g), 1e-6f, "9000 → 15000 的一半");
            Assert.IsFalse(GrowthProgress.IsMax(g));
            var ready = G(15000, FormId.Big);
            Assert.AreEqual(1f, GrowthProgress.Ratio(ready));
            Assert.IsTrue(GrowthProgress.IsMax(ready));
            Assert.AreEqual(1f, GrowthProgress.Ratio(9000, FormId.Big, 3000, 9000), "不传重生阈值保持旧行为（满格）");
        }

        [Test]
        public void Config_RebornGrowthMustExceedSmallToBig()
        {
            var cfg = ConfigLoader.FromJson("{ \"growth\": { \"eggToSmall\": 3000, \"smallToBig\": 9000, \"rebornGrowth\": 8000 } }", null);
            cfg.Sanitize(null);
            Assert.Greater(cfg.growth.rebornGrowth, cfg.growth.smallToBig);
            Assert.AreEqual(15000, new GameConfig().growth.rebornGrowth, "默认 15000");
        }

        // ---------- 重生流程 ----------

        [Test]
        public void Reborn_BackToEgg_GrowthZero_SmokeSwitchQueued()
        {
            var g = G(16000, FormId.Big);
            var f = new FormSwitchService(g, FormId.Big, true);
            Assert.IsTrue(f.Reborn());
            Assert.AreEqual(0, g.Growth);
            Assert.AreEqual(FormId.Egg, g.HighestForm);
            Assert.IsFalse(g.CanReborn);
            Assert.IsFalse(g.HasPendingUnlock, "成长值 0 不会立刻又进化");

            var t = f.TryBegin(true);
            Assert.IsTrue(t.HasValue);
            Assert.AreEqual(new FormTransition(FormId.Big, FormId.Egg, FormTransitionKind.Switch).ToString(), t.Value.ToString());
            f.ApplySwap();
            f.Complete();
            Assert.AreEqual(FormId.Egg, f.DisplayForm);
            Assert.IsFalse(f.CanSelect(FormId.Small), "重生后要重新进化");
        }

        [Test]
        public void Reborn_RejectedWhenNotReadyOrTransitioning()
        {
            var g = G(14000, FormId.Big);
            var f = new FormSwitchService(g, FormId.Big, true);
            Assert.IsFalse(f.Reborn(), "还没到可重生");

            var g2 = G(15000, FormId.Big);
            var f2 = new FormSwitchService(g2, FormId.Big, true);
            f2.RequestSwitch(FormId.Small);
            f2.TryBegin(true);
            Assert.IsTrue(f2.IsTransitioning);
            Assert.IsFalse(f2.Reborn(), "过场中不能重生");
            Assert.AreEqual(15000, g2.Growth);
        }

        [Test]
        public void Reborn_FromEggDisplay_NoTransitionNeeded()
        {
            var g = G(15000, FormId.Big);
            var f = new FormSwitchService(g, FormId.Egg, true);
            Assert.IsTrue(f.Reborn());
            Assert.IsFalse(f.TryBegin(true).HasValue);
            Assert.AreEqual(FormId.Egg, f.DisplayForm);
        }

        [Test]
        public void Wallet_ResetBalance_KeepsLifetimeStats()
        {
            var w = new ClickWallet(9000, 12000, 3000);
            long seenNew = -1;
            w.Changed += (_, n) => seenNew = n;
            w.ResetBalance();
            Assert.AreEqual(0, w.Balance);
            Assert.AreEqual(0, seenNew);
            Assert.AreEqual(12000, w.LifetimeEarned);
            Assert.AreEqual(3000, w.LifetimeSpent);
        }

        [Test]
        public void Collection_LockedRebornSlot_NameHidden_UnlockedNormal()
        {
            var c = Catalog();
            var u = Unlocks(c);
            var slot = CollectionViewModel.BuildPage(c, u, FormId.Egg, FormId.Egg).Slots.First(s => s.Id == "egg_angel");
            Assert.AreEqual(SlotState.Locked, slot.State);
            Assert.AreEqual(CollectionViewModel.LockedName, slot.Name);
            Assert.IsTrue(slot.Def.IsReborn);

            u.Unlock("egg_angel", UnlockSource.Reborn);
            slot = CollectionViewModel.BuildPage(c, u, FormId.Egg, FormId.Egg).Slots.First(s => s.Id == "egg_angel");
            Assert.AreEqual(SlotState.Playable, slot.State);
            Assert.AreEqual("egg_angel", slot.Name);
        }

        [Test]
        public void Save_RebornCount_RoundTrips_OldSaveIsZero()
        {
            string dir = Path.Combine(Path.GetTempPath(), "NaiwaRebornSave_" + Guid.NewGuid().ToString("N"));
            try
            {
                var svc = new SaveServiceLite(dir, _ => { });
                svc.Save(new SaveDataLite { growth = 1, rebornCount = 2 });
                Assert.AreEqual(2, new SaveServiceLite(dir, _ => { }).Load().rebornCount);

                File.WriteAllText(svc.MainPath, "{ \"version\": 2, \"growth\": 10 }");
                File.Delete(svc.BackupPath);
                Assert.AreEqual(0, new SaveServiceLite(dir, _ => { }).Load().rebornCount);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (Exception) { }
            }
        }
    }
}
