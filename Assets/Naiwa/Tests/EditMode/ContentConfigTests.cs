using System.Linq;
using Naiwa.Content;
using Naiwa.Growth;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class ContentConfigTests
    {
        [Test]
        public void ParsesSample_ThreeFormsSixEmotes()
        {
            var r = TestContent.Parse();
            Assert.IsFalse(r.HasErrors, string.Join("\n", r.Errors));
            Assert.AreEqual(3, r.Forms.Count);
            Assert.AreEqual(6, r.EmoteCount);

            var bath = r.AllEmotes.First(e => e.Id == "egg_bath");
            Assert.AreEqual(UnlockMode.Lottery, bath.Unlock);
            Assert.AreEqual(1, bath.LotteryWeight);
            Assert.AreEqual("icons/egg_bath.png", bath.Icon);
            Assert.AreEqual("泡个澡", bath.DisplayName);
            Assert.AreEqual(FormId.Egg, bath.Form);
            Assert.AreEqual(-1, bath.IconFrame);
            Assert.AreEqual("小奶蛙", r.FormDisplayName(FormId.Small));
        }

        [Test]
        public void MissingUnlock_IsDefault_MissingWeight_IsOne()
        {
            string json = TestContent.SampleJson.Replace(@", ""unlock"": ""Default"" }", " }");
            var r = ContentConfig.Parse(json, n => n);
            Assert.AreEqual(UnlockMode.Default, r.AllEmotes.First(e => e.Id == "egg_drink").Unlock);
            Assert.AreEqual(1, r.AllEmotes.First(e => e.Id == "small_heart").LotteryWeight);
        }

        [Test]
        public void InvalidUnlock_FallsBackToDefault_WithWarning()
        {
            string json = TestContent.SampleJson.Replace(@"""unlock"": ""Lottery"", ""lotteryWeight"": 1", @"""unlock"": ""Gacha""");
            var r = ContentConfig.Parse(json, n => n);
            Assert.AreEqual(UnlockMode.Default, r.AllEmotes.First(e => e.Id == "egg_bath").Unlock);
            Assert.IsTrue(r.Warnings.Exists(w => w.Contains("egg_bath") && w.Contains("Gacha")));
        }

        [Test]
        public void DuplicateId_SecondSkipped_WithWarning()
        {
            string json = TestContent.SampleJson.Replace(@"""id"": ""big_fall""", @"""id"": ""big_laugh""");
            var r = ContentConfig.Parse(json, n => n);
            Assert.AreEqual(1, r.Get(FormId.Big).Emotes.Count);
            Assert.AreEqual("big_emo_laugh", r.Get(FormId.Big).Emotes[0].Folder);
            Assert.IsTrue(r.Warnings.Exists(w => w.Contains("big_laugh") && w.Contains("重复")));
        }

        [Test]
        public void FormWithoutDefault_WarnsButNoError()
        {
            string json = TestContent.SampleJson.Replace(@"""folder"": ""big_emo_laugh"", ""fps"": 24, ""unlock"": ""Default""",
                @"""folder"": ""big_emo_laugh"", ""fps"": 24, ""unlock"": ""Lottery""");
            var r = ContentConfig.Parse(json, n => n);
            Assert.IsFalse(r.HasErrors);
            Assert.IsTrue(r.Warnings.Exists(w => w.Contains("Big") && w.Contains("Default")));
        }

        [Test]
        public void MissingFolder_SkipsEntryWithWarning_NoThrow()
        {
            ContentConfigResult r = null;
            Assert.DoesNotThrow(() => r = ContentConfig.Parse(TestContent.SampleJson, n => n == "egg_emo_bath" ? null : n));
            Assert.AreEqual(1, r.Get(FormId.Egg).Emotes.Count);
            Assert.IsTrue(r.Warnings.Exists(w => w.Contains("egg_emo_bath")));
        }

        [Test]
        public void IllegalId_Skipped()
        {
            string json = TestContent.SampleJson.Replace(@"""id"": ""small_heart""", @"""id"": ""Small-Heart""");
            var r = ContentConfig.Parse(json, n => n);
            Assert.AreEqual(1, r.Get(FormId.Small).Emotes.Count);
            Assert.IsTrue(r.Warnings.Exists(w => w.Contains("Small-Heart")));
        }

        [Test]
        public void FormWithoutIdle_ReturnsError()
        {
            string json = TestContent.SampleJson.Replace(@"""idle"": { ""folder"": ""small_idle"", ""fps"": 12 },", "");
            var r = ContentConfig.Parse(json, n => n);
            Assert.IsTrue(r.HasErrors);
            Assert.IsNull(r.Get(FormId.Small).Idle);
        }

        [Test]
        public void Schema1Fallback_AllDefault()
        {
            const string v1 = @"{ ""schemaVersion"": 1, ""forms"": [
                { ""form"": ""Egg"", ""idle"": { ""folder"": ""egg_Idle"", ""fps"": 12 }, ""emotes"": [ { ""id"": ""egg_drink"", ""folder"": ""egg_emo_drink"", ""fps"": 24 } ] },
                { ""form"": ""Small"", ""idle"": { ""folder"": ""small_idle"", ""fps"": 12 }, ""emotes"": [ { ""id"": ""small_heart"", ""folder"": ""small_heart"", ""fps"": 24 } ] },
                { ""form"": ""Big"", ""idle"": { ""folder"": ""big_Idle"", ""fps"": 12 }, ""emotes"": [ { ""id"": ""big_laugh"", ""folder"": ""big_emo_laugh"", ""fps"": 24, ""unlock"": ""Lottery"" } ] } ] }";
            var r = ContentConfig.Parse(v1, n => n);
            Assert.IsFalse(r.HasErrors);
            Assert.AreEqual(3, r.EmoteCount);
            Assert.IsTrue(r.AllEmotes.All(e => e.Unlock == UnlockMode.Default));
            Assert.IsTrue(r.Warnings.Exists(w => w.Contains("schema 1")));
        }

        [Test]
        public void Order_SortsEmotesWithinForm()
        {
            string json = TestContent.SampleJson.Replace(@"""id"": ""egg_bath"",", @"""id"": ""egg_bath"", ""order"": -5,");
            var r = ContentConfig.Parse(json, n => n);
            Assert.AreEqual("egg_bath", r.Get(FormId.Egg).Emotes[0].Id);
        }

        [Test]
        public void BrokenJson_ReturnsErrorWithoutThrowing()
        {
            ContentConfigResult r = null;
            Assert.DoesNotThrow(() => r = ContentConfig.Parse("{ not json", n => n));
            Assert.IsTrue(r.HasErrors);
        }
    }
}
