using Naiwa.Core;
using Naiwa.Pet;
using NUnit.Framework;
using UnityEngine;

namespace Naiwa.Tests
{
    public class SquashStretchTests
    {
        GameObject _go;
        SquashStretch _squash;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SquashTest");
            _squash = _go.AddComponent<SquashStretch>();
            _squash.Configure(new PetConfig { squashScaleY = 0.9f, squashScaleX = 1f, squashRecoverSec = 0.1f });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void Trigger_SameFrameAtMinimum_ThenRecovers()
        {
            _squash.Trigger();
            _squash.Tick(0.033f);
            Assert.AreEqual(0.9f, _go.transform.localScale.y, 1e-4f, "按下同一帧压到最低");
            Assert.AreEqual(1f, _go.transform.localScale.x, 1e-4f);

            _squash.Tick(0.05f);
            float mid = _go.transform.localScale.y;
            Assert.That(mid, Is.GreaterThan(0.9f).And.LessThan(1f));

            _squash.Tick(0.06f);
            Assert.AreEqual(Vector3.one, _go.transform.localScale);
            Assert.IsFalse(_squash.IsActive);
        }

        [Test]
        public void RepeatedTriggers_DoNotAccumulate()
        {
            for (int i = 0; i < 5; i++)
            {
                _squash.Trigger();
                _squash.Tick(0.02f);
            }
            Assert.AreEqual(0.9f, _go.transform.localScale.y, 1e-4f);
        }

        [Test]
        public void PositionUnchanged_OnlyScaleChanges()
        {
            var pos = _go.transform.position;
            _squash.Trigger();
            _squash.Tick(0.016f);
            Assert.AreEqual(pos, _go.transform.position);
        }
    }
}
