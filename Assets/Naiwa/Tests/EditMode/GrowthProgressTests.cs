using Naiwa.Growth;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class GrowthProgressTests
    {
        static float R(long growth, FormId highest) => GrowthProgress.Ratio(growth, highest, 5000, 12000);

        [Test] public void Zero_IsZero() => Assert.AreEqual(0f, R(0, FormId.Egg));
        [Test] public void Egg2500_IsHalf() => Assert.AreEqual(0.5f, R(2500, FormId.Egg), 1e-6f);
        [Test] public void Small5000_IsZero() => Assert.AreEqual(0f, R(5000, FormId.Small));
        [Test] public void Small8500_IsHalf() => Assert.AreEqual(0.5f, R(8500, FormId.Small), 1e-6f);
        [Test] public void Big_AnyValue_IsOne() { Assert.AreEqual(1f, R(0, FormId.Big)); Assert.AreEqual(1f, R(999999, FormId.Big)); }
        [Test] public void OldSave_Small4000_ClampedToZero() => Assert.AreEqual(0f, R(4000, FormId.Small));
        [Test] public void Egg_OverThreshold_ClampedToOne() => Assert.AreEqual(1f, R(7000, FormId.Egg), "进化开始前保持满格");

        [Test]
        public void FollowsHighestForm_NotDisplayForm()
        {
            var g = new GrowthService(5000, 12000, 20000, FormId.Big);
            var forms = new FormSwitchService(g, FormId.Egg, true);
            Assert.AreEqual(FormId.Egg, forms.DisplayForm);
            Assert.AreEqual(1f, GrowthProgress.Ratio(g));
            Assert.IsTrue(GrowthProgress.IsMax(g.HighestForm));
        }
    }
}
