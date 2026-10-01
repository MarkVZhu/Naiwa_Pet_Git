using System;
using System.Collections.Generic;
using Naiwa.Content;
using Naiwa.Growth;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class EmotePickerTests
    {
        static List<ClipDef> Clips(params string[] ids)
        {
            var list = new List<ClipDef>();
            foreach (var id in ids) list.Add(new ClipDef { Id = id, Folder = id, Fps = 24 });
            return list;
        }

        [Test]
        public void TwoCandidates_UniformWithin40To60Percent()
        {
            var picker = new EmotePicker(false, new Random(12345));
            var clips = Clips("a", "b");
            int a = 0;
            for (int i = 0; i < 1000; i++)
                if (picker.Pick(FormId.Egg, clips).Id == "a") a++;

            Assert.That(a, Is.InRange(400, 600));
            Assert.That(1000 - a, Is.InRange(400, 600));
        }

        [Test]
        public void SingleCandidate_AlwaysReturnsIt()
        {
            var picker = new EmotePicker(true, new Random(1));
            var clips = Clips("big_laugh");
            for (int i = 0; i < 20; i++)
                Assert.AreEqual("big_laugh", picker.Pick(FormId.Big, clips).Id);
        }

        [Test]
        public void NoCandidates_ReturnsNullWithoutThrowing()
        {
            var picker = new EmotePicker(false);
            ClipDef result = null;
            Assert.DoesNotThrow(() => result = picker.Pick(FormId.Small, new List<ClipDef>()));
            Assert.IsNull(result);
            Assert.DoesNotThrow(() => result = picker.Pick<ClipDef>(FormId.Small, null));
            Assert.IsNull(result);
        }

        [Test]
        public void AvoidImmediateRepeat_TwoCandidatesStrictlyAlternate()
        {
            var picker = new EmotePicker(true, new Random(7));
            var clips = Clips("a", "b");
            string last = picker.Pick(FormId.Egg, clips).Id;
            for (int i = 0; i < 100; i++)
            {
                string next = picker.Pick(FormId.Egg, clips).Id;
                Assert.AreNotEqual(last, next);
                last = next;
            }
        }
    }
}
