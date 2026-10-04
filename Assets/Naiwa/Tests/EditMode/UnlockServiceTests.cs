using System.Collections.Generic;
using Naiwa.Content;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class UnlockServiceTests
    {
        [Test]
        public void Startup_DefaultsUnlockedSilently()
        {
            var svc = new UnlockService(TestContent.Catalog(), null);
            int events = 0;
            svc.EmoteUnlocked += (_, __) => events++;
            Assert.AreEqual(3, svc.ApplyDefaults());
            Assert.IsTrue(svc.IsUnlocked("egg_drink"));
            Assert.IsTrue(svc.IsUnlocked("small_armcross"));
            Assert.IsTrue(svc.IsUnlocked("big_laugh"));
            Assert.IsFalse(svc.IsUnlocked("egg_bath"));
            Assert.AreEqual(0, events, "Default 静默解锁，不发揭晓事件");
        }

        [Test]
        public void RepeatedUnlock_ReturnsFalse_EventOnce()
        {
            var svc = new UnlockService(TestContent.Catalog(), null);
            var got = new List<string>();
            svc.EmoteUnlocked += (id, src) => got.Add(id + ":" + src);
            Assert.IsTrue(svc.Unlock("egg_bath", UnlockSource.Lottery));
            Assert.IsFalse(svc.Unlock("egg_bath", UnlockSource.Lottery));
            CollectionAssert.AreEqual(new[] { "egg_bath:Lottery" }, got);
        }

        [Test]
        public void LotteryChangedToDefault_UnlocksOnNextStartup()
        {
            string json = TestContent.SampleJson.Replace(@"""unlock"": ""Lottery"", ""lotteryWeight"": 1", @"""unlock"": ""Default""");
            var svc = new UnlockService(TestContent.Catalog(json), new[] { "egg_drink" });
            svc.ApplyDefaults();
            Assert.IsTrue(svc.IsUnlocked("egg_bath"));
        }

        [Test]
        public void UnlockedThenChangedToLottery_StaysUnlocked()
        {
            string json = TestContent.SampleJson.Replace(@"""folder"": ""egg_emo_drink"", ""fps"": 24, ""unlock"": ""Default""",
                @"""folder"": ""egg_emo_drink"", ""fps"": 24, ""unlock"": ""Lottery""");
            var svc = new UnlockService(TestContent.Catalog(json), new[] { "egg_drink" });
            svc.ApplyDefaults();
            Assert.IsTrue(svc.IsUnlocked("egg_drink"));
        }

        [Test]
        public void IdRemovedFromConfig_KeptInSave()
        {
            var svc = new UnlockService(TestContent.Catalog(), new[] { "old_removed_emote", "egg_bath" });
            svc.ApplyDefaults();
            Assert.Contains("old_removed_emote", svc.ToSaveList());
            Assert.IsTrue(svc.IsUnlocked("old_removed_emote"));

            svc.ResetToDefaults();
            Assert.Contains("old_removed_emote", svc.ToSaveList(), "重置图鉴不动配置里已删除的 id");
            Assert.IsFalse(svc.IsUnlocked("egg_bath"));
            Assert.IsTrue(svc.IsUnlocked("egg_drink"));
        }

        [Test]
        public void UnlockAll_UnlocksEveryRegisteredEmote()
        {
            var svc = new UnlockService(TestContent.Catalog(), null);
            svc.UnlockAll();
            Assert.AreEqual(6, svc.Unlocked.Count);
        }
    }
}
