using System;
using System.IO;
using Naiwa.Save;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class SaveMigrationTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "NaiwaMigrationTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch (Exception) { }
        }

        const string V1Json = "{\"version\":1,\"growth\":26788,\"form\":1,\"windowX\":3460,\"windowY\":1708,\"countingPaused\":false}";

        [Test]
        public void V1ToV2_ClicksEqualGrowth_TogglesOn_HighestEqualsForm_BackupWritten()
        {
            var svc = new SaveServiceLite(_dir, _ => { }, new SaveDefaults { windowShiftX = 380 });
            File.WriteAllText(svc.MainPath, V1Json);

            var data = svc.Load();
            Assert.IsTrue(svc.LastLoadMigrated);
            Assert.AreEqual(2, data.version);
            Assert.AreEqual(26788, data.growth);
            Assert.AreEqual(26788, data.clicks);
            Assert.AreEqual(26788, data.clicksLifetimeEarned);
            Assert.AreEqual(0, data.clicksLifetimeSpent);
            Assert.AreEqual(1, data.form);
            Assert.AreEqual(1, data.highestForm);
            Assert.AreEqual(0, data.lotteryNextAvailableUnixMs);
            Assert.IsEmpty(data.unlockedEmotes);
            Assert.IsTrue(data.hudShowClicks);
            Assert.IsTrue(data.hudShowGrowth);
            Assert.AreEqual(3460 - 380, data.windowX, "窗口左侧多出侧边区，宠物在屏幕上的位置不变");
            Assert.AreEqual(1708, data.windowY);
            Assert.IsTrue(File.Exists(svc.V1BackupPath));
            StringAssert.Contains("\"version\":1", File.ReadAllText(svc.V1BackupPath));
        }

        [Test]
        public void V1WithCounterHidden_KeepsPlayersChoice()
        {
            var svc = new SaveServiceLite(_dir, _ => { });
            File.WriteAllText(svc.MainPath, "{\"version\":1,\"growth\":10,\"form\":0,\"showCounter\":false}");
            var data = svc.Load();
            Assert.IsFalse(data.hudShowClicks);
            Assert.IsTrue(data.hudShowGrowth);
            Assert.IsFalse(data.HasWindowPosition);
        }

        [Test]
        public void V2RoundTrip_NoMigration()
        {
            var svc = new SaveServiceLite(_dir, _ => { });
            File.WriteAllText(svc.MainPath, V1Json);
            var data = svc.Load();
            data.hudShowGrowth = false;
            svc.Save(data);

            var again = new SaveServiceLite(_dir, _ => { });
            var loaded = again.Load();
            Assert.IsFalse(again.LastLoadMigrated);
            Assert.AreEqual(26788, loaded.clicks);
            Assert.IsTrue(loaded.hudShowClicks);
            Assert.IsFalse(loaded.hudShowGrowth);
        }
    }
}
