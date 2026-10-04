using System.Linq;
using Naiwa.Content;
using Naiwa.Growth;
using Naiwa.UI;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class CollectionViewModelTests
    {
        static (EmoteCatalog c, UnlockService u) Create(string json = null)
        {
            var c = TestContent.Catalog(json ?? TestContent.SampleJson);
            var u = new UnlockService(c, null);
            u.ApplyDefaults();
            return (c, u);
        }

        [Test]
        public void CurrentSmall_SmallPagePlayableAndLocked_BigPageViewOnly()
        {
            var (c, u) = Create();
            u.Unlock("big_fall", UnlockSource.Lottery);

            var small = CollectionViewModel.BuildPage(c, u, FormId.Small, FormId.Small);
            Assert.IsTrue(small.IsCurrent);
            Assert.AreEqual("小奶蛙·当前", small.TabTitle);
            Assert.AreEqual(SlotState.Playable, small.Slots.First(s => s.Id == "small_armcross").State);
            var heart = small.Slots.First(s => s.Id == "small_heart");
            Assert.AreEqual(SlotState.Locked, heart.State, "当前阶段未解锁也不能播放");
            Assert.AreEqual("？？？", heart.Name);

            var big = CollectionViewModel.BuildPage(c, u, FormId.Small, FormId.Big);
            Assert.AreEqual("大奶蛙", big.TabTitle);
            Assert.IsTrue(big.Slots.All(s => s.State == SlotState.ViewOnly));
            Assert.AreEqual("摔个大跟头", big.Slots.First(s => s.Id == "big_fall").Name);
        }

        [Test]
        public void AfterFormChange_StatesRecomputed()
        {
            var (c, u) = Create();
            var before = CollectionViewModel.BuildPage(c, u, FormId.Small, FormId.Small);
            var after = CollectionViewModel.BuildPage(c, u, FormId.Big, FormId.Small);
            Assert.AreEqual(SlotState.Playable, before.Slots.First(s => s.Id == "small_armcross").State);
            Assert.AreEqual(SlotState.ViewOnly, after.Slots.First(s => s.Id == "small_armcross").State);
            Assert.AreEqual("小奶蛙", after.TabTitle);
        }

        [Test]
        public void CollectedCount()
        {
            var (c, u) = Create();
            Assert.AreEqual((3, 6), CollectionViewModel.Count(c, u));
            u.Unlock("egg_bath", UnlockSource.Lottery);
            Assert.AreEqual((4, 6), CollectionViewModel.Count(c, u));
        }

        [Test]
        public void OrderRespected()
        {
            string json = TestContent.SampleJson.Replace(@"""id"": ""egg_bath"",", @"""id"": ""egg_bath"", ""order"": -1,");
            var (c, u) = Create(json);
            var page = CollectionViewModel.BuildPage(c, u, FormId.Egg, FormId.Egg);
            Assert.AreEqual("egg_bath", page.Slots[0].Id);
        }
    }
}
