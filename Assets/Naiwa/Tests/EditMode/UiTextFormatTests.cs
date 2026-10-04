using System;
using Naiwa.Economy;
using Naiwa.UI;
using NUnit.Framework;

namespace Naiwa.Tests
{
    public class UiTextFormatTests
    {
        static readonly TimeSpan Cd = TimeSpan.FromMinutes(30);

        static LotteryStatus S(LotteryState st, long shortBy = 0, TimeSpan? left = null) =>
            new LotteryStatus(st, 2000, shortBy, left ?? TimeSpan.Zero, Cd);

        [Test]
        public void Ready()
        {
            var s = S(LotteryState.Ready);
            Assert.AreEqual("可以抽了！", UiTextFormat.MenuLine(s));
            Assert.AreEqual("可以抽奖了！点一下奶蛙脚下的转盘", UiTextFormat.CollectionLine1(s));
            Assert.AreEqual("每次消耗 2,000 点击量，两次之间至少隔 30 分钟", UiTextFormat.CollectionLine2(s));
        }

        [Test]
        public void Cooldown_WithAndWithoutShortfall()
        {
            var s = S(LotteryState.Cooldown, 0, TimeSpan.FromSeconds(12 * 60 + 5));
            Assert.AreEqual("冷却中，还剩 12:05", UiTextFormat.MenuLine(s));
            Assert.AreEqual("抽奖冷却中，还剩 12:05", UiTextFormat.CollectionLine1(s));
            Assert.AreEqual("每次消耗 2,000 点击量，两次之间至少隔 30 分钟", UiTextFormat.CollectionLine2(s));

            var poor = S(LotteryState.Cooldown, 1234, TimeSpan.FromMinutes(5));
            Assert.AreEqual("每次消耗 2,000 点击量，两次之间至少隔 30 分钟（还差 1,234）", UiTextFormat.CollectionLine2(poor));
        }

        [Test]
        public void NotEnoughClicks()
        {
            var s = S(LotteryState.NotEnoughClicks, 1500);
            Assert.AreEqual("还差 1,500 点击量", UiTextFormat.MenuLine(s));
            Assert.AreEqual("还差 1,500 点击量就能抽奖", UiTextFormat.CollectionLine1(s));
            Assert.AreEqual("每次消耗 2,000 点击量，两次之间至少隔 30 分钟", UiTextFormat.CollectionLine2(s));
        }

        [Test]
        public void PoolEmpty()
        {
            var s = S(LotteryState.PoolEmpty);
            Assert.AreEqual("已全部收集", UiTextFormat.MenuLine(s));
            Assert.AreEqual("所有表情都收集齐了！", UiTextFormat.CollectionLine1(s));
            Assert.AreEqual("以后新增的表情会自动进入抽奖池", UiTextFormat.CollectionLine2(s));
        }

        [Test]
        public void DurationBoundaries()
        {
            Assert.AreEqual("59:59", UiTextFormat.Duration(TimeSpan.FromSeconds(3599)));
            Assert.AreEqual("1:00:00", UiTextFormat.Duration(TimeSpan.FromHours(1)));
            Assert.AreEqual("00:01", UiTextFormat.Duration(TimeSpan.FromMilliseconds(300)), "不足 1 秒向上取整");
            Assert.AreEqual("30:00", UiTextFormat.Duration(Cd));
        }

        [Test]
        public void CostFormatting()
        {
            Assert.AreEqual("2,000", UiTextFormat.Thousands(2000));
            Assert.AreEqual("2000", UiTextFormat.BubbleCost(2000));
        }
    }
}
