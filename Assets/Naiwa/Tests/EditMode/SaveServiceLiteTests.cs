using System;
using System.Collections.Generic;
using System.IO;
using Naiwa.Save;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class SaveServiceLiteTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "NaiwaSaveTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch (Exception) { }
        }

        SaveServiceLite Create() => new SaveServiceLite(_dir, _ => { });

        static SaveDataLite Sample(long growth) => new SaveDataLite
        {
            growth = growth,
            form = 1,
            highestForm = 2,
            clicks = 777,
            clicksLifetimeEarned = 900,
            clicksLifetimeSpent = 123,
            unlockedEmotes = new List<string> { "egg_drink", "big_fall" },
            lotteryNextAvailableUnixMs = 1790000000000,
            lotteryDrawCount = 3,
            windowX = 1234,
            windowY = -56,
            countingPaused = true,
            hudShowClicks = false,
            hudShowGrowth = true,
        };

        [Test]
        public void WriteThenRead_FieldsMatch()
        {
            var svc = Create();
            Assert.IsTrue(svc.Save(Sample(12345)));

            var reader = Create();
            var loaded = reader.Load();
            Assert.AreEqual(SaveLoadSource.Main, reader.LastLoadSource);
            Assert.IsFalse(reader.LastLoadMigrated);
            Assert.AreEqual(2, loaded.version);
            Assert.AreEqual(12345, loaded.growth);
            Assert.AreEqual(1, loaded.form);
            Assert.AreEqual(2, loaded.highestForm);
            Assert.AreEqual(777, loaded.clicks);
            Assert.AreEqual(900, loaded.clicksLifetimeEarned);
            Assert.AreEqual(123, loaded.clicksLifetimeSpent);
            CollectionAssert.AreEqual(new[] { "egg_drink", "big_fall" }, loaded.unlockedEmotes);
            Assert.AreEqual(1790000000000, loaded.lotteryNextAvailableUnixMs);
            Assert.AreEqual(3, loaded.lotteryDrawCount);
            Assert.AreEqual(1234, loaded.windowX);
            Assert.AreEqual(-56, loaded.windowY);
            Assert.IsTrue(loaded.countingPaused);
            Assert.IsFalse(loaded.hudShowClicks);
            Assert.IsTrue(loaded.hudShowGrowth);
            Assert.IsFalse(File.Exists(svc.TempPath));
        }

        [Test]
        public void DisplayScale_RoundTrips_AndOldSaveDefaultsTo1()
        {
            var svc = Create();
            var data = Sample(5);
            data.displayScale = 1.35f;
            svc.Save(data);
            Assert.AreEqual(1.35f, Create().Load().displayScale, 1e-5f);

            // 加缩放功能之前的存档没有 displayScale 字段
            File.WriteAllText(svc.MainPath, "{ \"version\": 2, \"growth\": 10, \"windowX\": 5, \"windowY\": 6 }");
            File.Delete(svc.BackupPath);
            var old = Create().Load();
            Assert.AreEqual(10, old.growth);
            Assert.AreEqual(1f, old.displayScale);
        }

        [Test]
        public void ApplyHeadroom_ShiftScalesWithDisplayScale()
        {
            var d = new SaveDataLite { windowX = 0, windowY = 100, windowHeadroomPx = 0 };
            Assert.IsTrue(d.ApplyHeadroom(40, 1.5f));
            Assert.AreEqual(40, d.windowY, "顶部留白 +40 → 1.5 倍时窗口上移 60");
        }

        [Test]
        public void MainCorrupted_RecoversFromBackup()
        {
            var svc = Create();
            svc.Save(Sample(100));
            svc.Save(Sample(200));
            Assert.IsTrue(File.Exists(svc.BackupPath));

            File.WriteAllText(svc.MainPath, "{ broken json ###");

            var reader = Create();
            var loaded = reader.Load();
            Assert.AreEqual(SaveLoadSource.Backup, reader.LastLoadSource);
            Assert.AreEqual(100, loaded.growth);
            Assert.AreEqual(1234, loaded.windowX);
            Assert.IsNotEmpty(Directory.GetFiles(_dir, "save.corrupt.*.json"));
        }

        [Test]
        public void BothCorrupted_CreatesDefault()
        {
            var svc = Create();
            File.WriteAllText(svc.MainPath, "garbage");
            File.WriteAllText(svc.BackupPath, "");

            var loaded = svc.Load();
            Assert.AreEqual(SaveLoadSource.NewDefault, svc.LastLoadSource);
            Assert.AreEqual(0, loaded.growth);
            Assert.AreEqual(0, loaded.clicks);
            Assert.IsFalse(loaded.HasWindowPosition);
            Assert.IsTrue(loaded.hudShowClicks);
            Assert.IsTrue(loaded.hudShowGrowth);
            Assert.IsNotEmpty(Directory.GetFiles(_dir, "save.corrupt.*.json"));
        }

        [Test]
        public void NoFiles_CreatesDefaultWithConfiguredToggles()
        {
            var svc = new SaveServiceLite(_dir, _ => { }, new SaveDefaults { hudShowClicks = false, hudShowGrowth = true });
            var loaded = svc.Load();
            Assert.AreEqual(SaveLoadSource.NewDefault, svc.LastLoadSource);
            Assert.AreEqual(SaveDataLite.CurrentVersion, loaded.version);
            Assert.IsFalse(loaded.hudShowClicks);
            Assert.IsTrue(loaded.hudShowGrowth);
        }

        [Test]
        public void SaveCount_IncrementsOnEachSuccessfulWrite()
        {
            var svc = Create();
            svc.Save(Sample(1));
            svc.Save(Sample(2));
            Assert.AreEqual(2, svc.SaveCount);
        }
    }
}
