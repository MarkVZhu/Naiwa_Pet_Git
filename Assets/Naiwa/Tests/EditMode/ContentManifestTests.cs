using System;
using Naiwa.Content;
using Naiwa.Growth;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class ContentManifestTests
    {
        // §V.3.1 的示例映射表
        const string SampleJson = @"{
  ""schemaVersion"": 1,
  ""forms"": [
    { ""form"": ""Egg"",   ""idle"": { ""folder"": ""egg_Idle"",   ""fps"": 12 },
      ""emotes"": [ { ""id"": ""egg_drink"", ""folder"": ""egg_emo_drink"", ""fps"": 24 },
                  { ""id"": ""egg_bath"",  ""folder"": ""egg_emo_bath"",  ""fps"": 24 } ] },
    { ""form"": ""Small"", ""idle"": { ""folder"": ""small_idle"", ""fps"": 12 },
      ""emotes"": [ { ""id"": ""small_heart"",    ""folder"": ""small_heart"",    ""fps"": 24 },
                  { ""id"": ""small_armcross"", ""folder"": ""small_armcross"", ""fps"": 24 } ] },
    { ""form"": ""Big"",   ""idle"": { ""folder"": ""big_Idle"",   ""fps"": 12 },
      ""emotes"": [ { ""id"": ""big_laugh"", ""folder"": ""big_emo_laugh"", ""fps"": 24 } ] }
  ]
}";

        static Func<string, string> AllExist => name => name;

        [Test]
        public void ParsesSample_ThreeFormsFiveEmotes()
        {
            var r = ContentManifest.Parse(SampleJson, AllExist);

            Assert.IsFalse(r.HasErrors, string.Join("\n", r.Errors));
            Assert.AreEqual(3, r.Forms.Count);
            Assert.AreEqual(5, r.EmoteCount);
            Assert.AreEqual("egg_Idle", r.Get(FormId.Egg).Idle.Folder);
            Assert.AreEqual(12f, r.Get(FormId.Egg).Idle.Fps);
            Assert.IsTrue(r.Get(FormId.Egg).Idle.Loop);
            Assert.IsFalse(r.Get(FormId.Big).Emotes[0].Loop);
            Assert.AreEqual(1, r.Get(FormId.Big).Emotes.Count);
        }

        [Test]
        public void MissingFolder_SkipsEntryWithWarning_NoThrow()
        {
            ContentManifestResult r = null;
            Assert.DoesNotThrow(() =>
                r = ContentManifest.Parse(SampleJson, name => name == "egg_emo_bath" ? null : name));

            Assert.AreEqual(1, r.Get(FormId.Egg).Emotes.Count);
            Assert.AreEqual("egg_drink", r.Get(FormId.Egg).Emotes[0].Id);
            Assert.AreEqual(4, r.EmoteCount);
            Assert.IsTrue(r.Warnings.Exists(w => w.Contains("egg_emo_bath")));
            Assert.IsFalse(r.HasErrors);
        }

        [Test]
        public void FormWithoutIdle_ReturnsError()
        {
            string json = SampleJson.Replace(@"""idle"": { ""folder"": ""small_idle"", ""fps"": 12 },", "");
            var r = ContentManifest.Parse(json, AllExist);

            Assert.IsTrue(r.HasErrors);
            Assert.IsNull(r.Get(FormId.Small).Idle);
            Assert.IsTrue(r.Errors.Exists(e => e.Contains("Small")));
        }

        [Test]
        public void IdleFolderMissing_ReturnsError()
        {
            var r = ContentManifest.Parse(SampleJson, name => name == "big_Idle" ? null : name);
            Assert.IsTrue(r.HasErrors);
            Assert.IsNull(r.Get(FormId.Big).Idle);
        }

        [Test]
        public void FolderResolvedCaseInsensitively_UsesRealName()
        {
            var r = ContentManifest.Parse(SampleJson, name => name.Equals("egg_idle", StringComparison.OrdinalIgnoreCase) ? "EGG_IDLE" : name);
            Assert.AreEqual("EGG_IDLE", r.Get(FormId.Egg).Idle.Folder);
        }

        [Test]
        public void BrokenJson_ReturnsErrorWithoutThrowing()
        {
            ContentManifestResult r = null;
            Assert.DoesNotThrow(() => r = ContentManifest.Parse("{ not json", AllExist));
            Assert.IsTrue(r.HasErrors);
        }
    }
}
