using System;
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

        static SaveDataLite Sample(int growth) => new SaveDataLite
        {
            growth = growth,
            form = 1,
            windowX = 1234,
            windowY = -56,
            countingPaused = true,
        };

        [Test]
        public void WriteThenRead_FieldsMatch()
        {
            var svc = Create();
            Assert.IsTrue(svc.Save(Sample(12345)));

            var reader = Create();
            var loaded = reader.Load();
            Assert.AreEqual(SaveLoadSource.Main, reader.LastLoadSource);
            Assert.AreEqual(1, loaded.version);
            Assert.AreEqual(12345, loaded.growth);
            Assert.AreEqual(1, loaded.form);
            Assert.AreEqual(1234, loaded.windowX);
            Assert.AreEqual(-56, loaded.windowY);
            Assert.IsTrue(loaded.countingPaused);
            Assert.IsFalse(File.Exists(svc.TempPath));
        }

        [Test]
        public void MainCorrupted_RecoversFromBackup()
        {
            var svc = Create();
            svc.Save(Sample(100));   // 主文件
            svc.Save(Sample(200));   // 主文件=200，.bak=100
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
            Assert.AreEqual(0, loaded.form);
            Assert.IsFalse(loaded.HasWindowPosition);
            Assert.IsFalse(loaded.countingPaused);
            Assert.IsNotEmpty(Directory.GetFiles(_dir, "save.corrupt.*.json"));
        }

        [Test]
        public void ShowCounter_DefaultsTrue_AndRoundTrips()
        {
            var svc = Create();
            Assert.IsTrue(svc.Load().showCounter);

            var data = Sample(1);
            data.showCounter = false;
            svc.Save(data);
            Assert.IsFalse(Create().Load().showCounter);
        }

        [Test]
        public void OldSaveWithoutShowCounter_LoadsAsTrue()
        {
            var svc = Create();
            File.WriteAllText(svc.MainPath, "{\"version\":1,\"growth\":42,\"form\":0,\"windowX\":10,\"windowY\":20,\"countingPaused\":false}");
            var loaded = svc.Load();
            Assert.AreEqual(SaveLoadSource.Main, svc.LastLoadSource);
            Assert.AreEqual(42, loaded.growth);
            Assert.IsTrue(loaded.showCounter);
        }

        [Test]
        public void NoFiles_CreatesDefault()
        {
            var svc = Create();
            var loaded = svc.Load();
            Assert.AreEqual(SaveLoadSource.NewDefault, svc.LastLoadSource);
            Assert.AreEqual(SaveDataLite.CurrentVersion, loaded.version);
        }
    }
}
