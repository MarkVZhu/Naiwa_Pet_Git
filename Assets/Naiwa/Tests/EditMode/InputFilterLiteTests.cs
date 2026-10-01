using Naiwa.Input;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class InputFilterLiteTests
    {
        static RawInputEvent KeyDown(int key, long t, bool injected = false) =>
            new RawInputEvent { Kind = RawKind.KeyDown, KeyId = key, TimestampMs = t, Injected = injected };

        static RawInputEvent KeyUp(int key, long t) =>
            new RawInputEvent { Kind = RawKind.KeyUp, KeyId = key, TimestampMs = t };

        static RawInputEvent MouseDown(long t) =>
            new RawInputEvent { Kind = RawKind.MouseDown, Button = MouseBtn.Left, TimestampMs = t };

        [Test]
        public void SameKeyPressedTenTimesWithoutRelease_CountsOnce()
        {
            var f = new InputFilterLite(15, true);
            int counted = 0;
            for (int i = 0; i < 10; i++)
                if (f.Process(KeyDown(65, i * 200))) counted++;
            Assert.AreEqual(1, counted);
            Assert.AreEqual(9, f.DroppedRepeat);
        }

        [Test]
        public void KeyReleasedBetweenPresses_CountsEach()
        {
            var f = new InputFilterLite(15, true);
            int counted = 0;
            for (int i = 0; i < 5; i++)
            {
                if (f.Process(KeyDown(65, i * 200))) counted++;
                f.Process(KeyUp(65, i * 200 + 50));
            }
            Assert.AreEqual(5, counted);
        }

        [Test]
        public void ThirtyDifferentKeysWithinOneSecond_CountsFifteen()
        {
            var f = new InputFilterLite(15, true);
            int counted = 0;
            for (int i = 0; i < 30; i++)
                if (f.Process(KeyDown(100 + i, i * 30))) counted++;
            Assert.AreEqual(15, counted);
            Assert.AreEqual(15, f.DroppedRateLimit);
        }

        [Test]
        public void RateWindowSlides_AfterOneSecondCountsAgain()
        {
            var f = new InputFilterLite(15, true);
            for (int i = 0; i < 15; i++) Assert.IsTrue(f.Process(MouseDown(i)));
            Assert.IsFalse(f.Process(MouseDown(500)));
            Assert.IsTrue(f.Process(MouseDown(1000)));
        }

        [Test]
        public void InjectedEvents_CountZero()
        {
            var f = new InputFilterLite(15, true);
            int counted = 0;
            for (int i = 0; i < 10; i++)
                if (f.Process(KeyDown(100 + i, i * 200, injected: true))) counted++;
            Assert.AreEqual(0, counted);
            Assert.AreEqual(10, f.DroppedInjected);
        }

        [Test]
        public void Paused_CountZero()
        {
            var f = new InputFilterLite(15, true) { Paused = true };
            int counted = 0;
            for (int i = 0; i < 10; i++)
            {
                if (f.Process(KeyDown(100 + i, i * 200))) counted++;
                if (f.Process(MouseDown(i * 200 + 100))) counted++;
            }
            Assert.AreEqual(0, counted);
            Assert.AreEqual(20, f.DroppedPaused);
        }

        [Test]
        public void KeyUpAndWheel_NeverCount()
        {
            var f = new InputFilterLite(15, true);
            Assert.IsFalse(f.Process(KeyUp(65, 0)));
            Assert.IsFalse(f.Process(new RawInputEvent { Kind = RawKind.MouseWheel, TimestampMs = 10 }));
            Assert.IsFalse(f.Process(new RawInputEvent { Kind = RawKind.MouseUp, Button = MouseBtn.Left, TimestampMs = 20 }));
            Assert.AreEqual(0, f.Counted);
        }

        [Test]
        public void PhysicalPress_IgnoresPauseInjectAndRateLimit_ButNotRepeat()
        {
            var f = new InputFilterLite(1, true) { Paused = true };
            Assert.IsFalse(f.Process(KeyDown(65, 0), out bool p1));
            Assert.IsTrue(p1, "暂停中仍然是物理按下");
            Assert.IsFalse(f.Process(KeyDown(65, 100), out bool p2));
            Assert.IsFalse(p2, "长按连发不算新的物理按下");

            f.Paused = false;
            Assert.IsFalse(f.Process(KeyDown(66, 200, injected: true), out bool p3));
            Assert.IsTrue(p3, "注入输入也触发反馈 [A]");

            Assert.IsTrue(f.Process(MouseDown(300), out bool p4));
            Assert.IsTrue(p4);
            Assert.IsFalse(f.Process(MouseDown(400), out bool p5));
            Assert.IsTrue(p5, "被限速拦下的按下仍然触发反馈");

            Assert.IsFalse(f.Process(KeyUp(65, 500), out bool p6));
            Assert.IsFalse(p6);
        }

        [Test]
        public void PruneHeldKeys_RecoversLostKeyUp()
        {
            var f = new InputFilterLite(15, true);
            Assert.IsTrue(f.Process(KeyDown(76, 0)));
            Assert.IsFalse(f.Process(KeyDown(76, 2000))); // KeyUp 丢失，被当成长按
            f.PruneHeldKeys(_ => false);
            Assert.IsTrue(f.Process(KeyDown(76, 4000)));
        }
    }
}
