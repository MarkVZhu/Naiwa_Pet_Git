using System;
using System.Globalization;
using Naiwa.Economy;

namespace Naiwa.UI
{
    /// <summary>抽奖状态文案（v1.0 §8.4），右键菜单、图鉴底部、气泡共用。纯 C#。</summary>
    public static class UiTextFormat
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Thousands(long n) => n.ToString("N0", Inv);

        /// <summary>不足 1 小时 mm:ss，否则 h:mm:ss。不足 1 秒向上取整。</summary>
        public static string Duration(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            long totalSec = (long)Math.Ceiling(t.TotalSeconds - 1e-6);
            long h = totalSec / 3600, m = totalSec / 60 % 60, s = totalSec % 60;
            return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{totalSec / 60:00}:{s:00}";
        }

        /// <summary>气泡内的消耗数字，不加千分位。</summary>
        public static string BubbleCost(int cost) => cost.ToString(Inv);

        static string CooldownMinutes(LotteryStatus s)
        {
            double min = s.CooldownTotal.TotalMinutes;
            return Math.Abs(min - Math.Round(min)) < 1e-6 ? ((long)Math.Round(min)).ToString(Inv) : min.ToString("0.#", Inv);
        }

        public static string MenuLine(LotteryStatus s)
        {
            switch (s.State)
            {
                case LotteryState.Ready: return "可以抽了！";
                case LotteryState.Cooldown: return $"冷却中，还剩 {Duration(s.CooldownLeft)}";
                case LotteryState.NotEnoughClicks: return $"还差 {Thousands(s.ClicksShort)} 点击量";
                default: return "已全部收集";
            }
        }

        public static string CollectionLine1(LotteryStatus s)
        {
            switch (s.State)
            {
                case LotteryState.Ready: return "可以抽奖了！点一下奶蛙脚下的转盘";
                case LotteryState.Cooldown: return $"抽奖冷却中，还剩 {Duration(s.CooldownLeft)}";
                case LotteryState.NotEnoughClicks: return $"还差 {Thousands(s.ClicksShort)} 点击量就能抽奖";
                default: return "所有表情都收集齐了！";
            }
        }

        public static string CollectionLine2(LotteryStatus s)
        {
            if (s.State == LotteryState.PoolEmpty) return "以后新增的表情会自动进入抽奖池";
            string line = $"每次消耗 {Thousands(s.Cost)} 点击量，两次之间至少隔 {CooldownMinutes(s)} 分钟";
            if (s.State == LotteryState.Cooldown && s.ClicksShort > 0) line += $"（还差 {Thousands(s.ClicksShort)}）";
            return line;
        }
    }
}
