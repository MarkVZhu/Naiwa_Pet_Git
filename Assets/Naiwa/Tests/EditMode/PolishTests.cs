using Naiwa.Core;
using Naiwa.Growth;
using Naiwa.Pet;
using Naiwa.Save;
using NUnit.Framework;

namespace Naiwa.Tests
{
    /// <summary>淡切不闪、阶段缩放、窗口顶部留白迁移。</summary>
    public class PolishTests
    {
        [Test]
        public void Crossfade_CombinedAlphaNeverDips()
        {
            for (int i = 0; i <= 100; i++)
            {
                float k = i / 100f;
                float top = PetAnimatorLite.CrossfadeTopAlpha(k);
                float bottom = PetAnimatorLite.CrossfadeBottomAlpha(k);
                float combined = top + bottom * (1f - top); // 新层在上的 over 合成
                Assert.AreEqual(1f, combined, 1e-4f, $"k={k}");
            }
            Assert.AreEqual(0f, PetAnimatorLite.CrossfadeTopAlpha(0f));
            Assert.AreEqual(0f, PetAnimatorLite.CrossfadeBottomAlpha(1f));
        }

        [Test]
        public void FormScale_EggSmaller_BigLarger()
        {
            var pet = new PetConfig();
            Assert.Less(pet.ScaleOf(FormId.Egg), pet.ScaleOf(FormId.Small));
            Assert.AreEqual(1f, pet.ScaleOf(FormId.Small));
            Assert.Greater(pet.ScaleOf(FormId.Big), pet.ScaleOf(FormId.Small));
        }

        [Test]
        public void FormScale_InvalidValues_Sanitized()
        {
            var cfg = new GameConfig();
            cfg.pet.eggScale = 0f;
            cfg.pet.bigScale = 9f;
            cfg.Sanitize(null);
            Assert.AreEqual(new PetConfig().eggScale, cfg.pet.eggScale);
            Assert.AreEqual(new PetConfig().bigScale, cfg.pet.bigScale);
        }

        [Test]
        public void BigForm_HeadFitsUnderWindowTop()
        {
            var cfg = new GameConfig();
            // 大奶蛙最高帧头顶约在画布 y=3（距脚底 577px，600px 画布）
            float headAboveFeetPx = 577f / PetGeometry.CanvasPx * cfg.window.sizePx * cfg.pet.bigScale;
            Assert.Less(headAboveFeetPx + 24f, cfg.window.FeetFromTopPx, "头顶上方还要放得下「新！」");
        }

        [Test]
        public void Headroom_OldSave_ShiftsWindowUp_Once()
        {
            var save = new SaveDataLite { windowX = 100, windowY = 500 };
            Assert.IsTrue(save.ApplyHeadroom(40));
            Assert.AreEqual(460, save.windowY, "窗口向上长了 40px，宠物在屏幕上的位置不变");
            Assert.AreEqual(100, save.windowX);
            Assert.IsFalse(save.ApplyHeadroom(40));
            Assert.AreEqual(460, save.windowY);
        }

        [Test]
        public void Headroom_NoPosition_NotShifted()
        {
            var save = new SaveDataLite();
            save.ApplyHeadroom(40);
            Assert.IsFalse(save.HasWindowPosition);
            Assert.AreEqual(40, save.windowHeadroomPx);
        }
    }
}
