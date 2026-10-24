using System;
using System.Collections.Generic;
using System.Linq;
using Naiwa.Content;
using Naiwa.Core;
using Naiwa.Economy;
using Naiwa.Growth;
using NUnit.Framework;

namespace Naiwa.Tests
{
    /// <summary>抽奖「状态偏好权重」：当前显示形态的表情，在自己的 lotteryWeight 上再加固定权重。</summary>
    public class LotteryFormPreferenceTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

        // 池子：egg_a(3) egg_b(3) | small_a(2) small_b(2) | big_a(1) big_b(1)，全是 Lottery，另各有一个 Default 保证阶段合法
        const string Json = @"{
  ""schemaVersion"": 2,
  ""forms"": [
    { ""form"": ""Egg"", ""idle"": { ""folder"": ""egg_Idle"", ""fps"": 12 }, ""emotes"": [
        { ""id"": ""egg_def"", ""folder"": ""egg_def"", ""fps"": 24, ""unlock"": ""Default"" },
        { ""id"": ""egg_a"", ""folder"": ""egg_a"", ""fps"": 24, ""unlock"": ""Lottery"", ""lotteryWeight"": 3 },
        { ""id"": ""egg_b"", ""folder"": ""egg_b"", ""fps"": 24, ""unlock"": ""Lottery"", ""lotteryWeight"": 3 } ] },
    { ""form"": ""Small"", ""idle"": { ""folder"": ""small_idle"", ""fps"": 24 }, ""emotes"": [
        { ""id"": ""small_def"", ""folder"": ""small_def"", ""fps"": 24, ""unlock"": ""Default"" },
        { ""id"": ""small_a"", ""folder"": ""small_a"", ""fps"": 24, ""unlock"": ""Lottery"", ""lotteryWeight"": 2 },
        { ""id"": ""small_b"", ""folder"": ""small_b"", ""fps"": 24, ""unlock"": ""Lottery"", ""lotteryWeight"": 2 } ] },
    { ""form"": ""Big"", ""idle"": { ""folder"": ""big_Idle"", ""fps"": 12 }, ""emotes"": [
        { ""id"": ""big_def"", ""folder"": ""big_def"", ""fps"": 24, ""unlock"": ""Default"" },
        { ""id"": ""big_a"", ""folder"": ""big_a"", ""fps"": 24, ""unlock"": ""Lottery"", ""lotteryWeight"": 1 },
        { ""id"": ""big_b"", ""folder"": ""big_b"", ""fps"": 24, ""unlock"": ""Lottery"", ""lotteryWeight"": 1 },
        { ""id"": ""big_god"", ""folder"": ""big_god"", ""fps"": 24, ""unlock"": ""Reborn"", ""lotteryWeight"": 9 } ] }
  ]
}";

        /// <summary>每次返回指定值（夹在范围内），用来精确落到权重区间的某一点。</summary>
        sealed class FixedRandom : IRandom
        {
            public int Value;
            public int Next(int max) => Math.Max(0, Math.Min(Value, max - 1));
        }

        sealed class Rig
        {
            public FixedRandom Rng = new FixedRandom();
            public LotteryService Lottery;
            public FormId Form = FormId.Egg;
        }

        static Rig Create(int preference, LotteryPoolScope scope = LotteryPoolScope.All)
        {
            var catalog = TestContent.Catalog(Json);
            var unlocks = new UnlockService(catalog, null);
            unlocks.ApplyDefaults();
            var rig = new Rig();
            rig.Lottery = new LotteryService(new ClickWallet(1000000), catalog, unlocks, rig.Rng, () => rig.Form, null,
                1, TimeSpan.Zero, scope, 0, 0, preference);
            return rig;
        }

        static EmoteDef Def(Rig rig, string id) => rig.Lottery.GetPool().First(e => e.Id == id);

        /// <summary>遍历随机数 0..总权重-1，统计每个表情被抽到的权重份数（等价于无限次抽样的精确概率）。</summary>
        static Dictionary<string, int> Distribution(Rig rig)
        {
            int total = rig.Lottery.GetPool().Sum(e => rig.Lottery.EffectiveWeight(e));
            var counts = new Dictionary<string, int>();
            for (int r = 0; r < total; r++)
            {
                var fresh = Create(rig.Lottery.FormPreferenceWeight, rig.Lottery.Scope);
                fresh.Form = rig.Form;
                fresh.Rng.Value = r;
                string id = fresh.Lottery.DebugDrawFree().EmoteId;
                counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
            }
            return counts;
        }

        [Test]
        public void CurrentForm_GetsExtraWeight_OthersUnchanged()
        {
            var rig = Create(2);
            rig.Form = FormId.Small;
            Assert.AreEqual(4, rig.Lottery.EffectiveWeight(Def(rig, "small_a")), "2 + 2");
            Assert.AreEqual(3, rig.Lottery.EffectiveWeight(Def(rig, "egg_a")), "别的阶段保持配置权重");
            Assert.AreEqual(1, rig.Lottery.EffectiveWeight(Def(rig, "big_a")));
        }

        [Test]
        public void Distribution_ExactRatios_WithPreference2()
        {
            var rig = Create(2);
            rig.Form = FormId.Small;
            // 池：egg_a 3, egg_b 3, small_a 4, small_b 4, big_a 1, big_b 1 = 总 16
            var d = Distribution(rig);
            Assert.AreEqual(3, d["egg_a"]);
            Assert.AreEqual(3, d["egg_b"]);
            Assert.AreEqual(4, d["small_a"]);
            Assert.AreEqual(4, d["small_b"]);
            Assert.AreEqual(1, d["big_a"]);
            Assert.AreEqual(1, d["big_b"]);
            Assert.AreEqual(16, d.Values.Sum());
        }

        [Test]
        public void Preference0_BehavesLikeBefore()
        {
            var rig = Create(0);
            rig.Form = FormId.Small;
            var d = Distribution(rig);
            // 3,3,2,2,1,1 = 12
            Assert.AreEqual(12, d.Values.Sum());
            Assert.AreEqual(2, d["small_a"]);
            Assert.AreEqual(3, d["egg_a"]);
        }

        [Test]
        public void ManualSwitchBackToSmall_FavorsSmall_EvenWhenBigUnlocked()
        {
            // 「当前显示形态」由委托提供，与已解锁的最高形态无关
            var rig = Create(2);
            rig.Form = FormId.Small; // 假设最高已是 Big，但手动切回了小奶蛙
            int small = Distribution(rig).Where(kv => kv.Key.StartsWith("small")).Sum(kv => kv.Value);
            rig.Form = FormId.Big;
            int smallWhenBig = Distribution(rig).Where(kv => kv.Key.StartsWith("small")).Sum(kv => kv.Value);
            Assert.AreEqual(8, small);
            Assert.AreEqual(4, smallWhenBig, "显示 Big 时 Small 不加成");
        }

        [Test]
        public void FormChange_TakesEffectOnNextDraw()
        {
            var rig = Create(2);
            rig.Form = FormId.Egg;
            Assert.AreEqual(5, rig.Lottery.EffectiveWeight(Def(rig, "egg_a")));
            rig.Form = FormId.Big;
            Assert.AreEqual(3, rig.Lottery.EffectiveWeight(Def(rig, "egg_a")));
            Assert.AreEqual(3, rig.Lottery.EffectiveWeight(Def(rig, "big_a")));
        }

        [Test]
        public void RebornEmotes_NeverInPool_EvenWithPreference()
        {
            var rig = Create(100);
            rig.Form = FormId.Big;
            Assert.IsFalse(rig.Lottery.GetPool().Any(e => e.Id == "big_god"));
            Assert.IsFalse(Distribution(rig).ContainsKey("big_god"));
        }

        [Test]
        public void CurrentFormOnly_PreferenceIrrelevant_OnlyCurrentFormDrawn()
        {
            var rig = Create(2, LotteryPoolScope.CurrentFormOnly);
            rig.Form = FormId.Small;
            Assert.IsTrue(Distribution(rig).Keys.All(k => k.StartsWith("small")));
        }

        [Test]
        public void NegativePreference_ClampedToZero()
        {
            var rig = Create(-5);
            Assert.AreEqual(0, rig.Lottery.FormPreferenceWeight);
        }

        [Test]
        public void Config_DefaultIs2_AndNegativeSanitized()
        {
            Assert.AreEqual(2, new GameConfig().lottery.formPreferenceWeight);

            var cfg = ConfigLoader.FromJson("{ \"lottery\": { \"formPreferenceWeight\": -3 } }", null);
            cfg.Sanitize(null);
            Assert.AreEqual(2, cfg.lottery.formPreferenceWeight, "负数回到默认值");

            var custom = ConfigLoader.FromJson("{ \"lottery\": { \"formPreferenceWeight\": 5 } }", null);
            custom.Sanitize(null);
            Assert.AreEqual(5, custom.lottery.formPreferenceWeight);

            var zero = ConfigLoader.FromJson("{ \"lottery\": { \"formPreferenceWeight\": 0 } }", null);
            zero.Sanitize(null);
            Assert.AreEqual(0, zero.lottery.formPreferenceWeight, "写 0 表示关闭偏好");
        }
    }
}
