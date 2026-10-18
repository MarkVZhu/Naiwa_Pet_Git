using System.Collections.Generic;
using Naiwa.Content;

namespace Naiwa.Economy
{
    /// <summary>
    /// v2.0 重生奖励：每次重生解锁一个还没解锁的重生表情（unlock = Reborn）。
    /// 一定先给 lotteryWeight 最大的；最大权重有多个时在它们之中等概率随机。纯 C#。
    /// </summary>
    public static class RebornRewards
    {
        /// <summary>所有还没解锁的重生表情。</summary>
        public static List<EmoteDef> Remaining(EmoteCatalog catalog, IUnlockService unlocks, List<EmoteDef> into = null)
        {
            into = into ?? new List<EmoteDef>();
            into.Clear();
            foreach (var e in catalog.All)
                if (e.IsReborn && !unlocks.IsUnlocked(e.Id)) into.Add(e);
            return into;
        }

        /// <summary>挑选下一个重生奖励；全部解锁过返回 null。</summary>
        public static EmoteDef Pick(EmoteCatalog catalog, IUnlockService unlocks, IRandom rng)
        {
            var remaining = Remaining(catalog, unlocks);
            if (remaining.Count == 0) return null;

            int best = int.MinValue;
            foreach (var e in remaining)
                if (e.LotteryWeight > best) best = e.LotteryWeight;

            var top = new List<EmoteDef>();
            foreach (var e in remaining)
                if (e.LotteryWeight == best) top.Add(e);

            int i = top.Count == 1 || rng == null ? 0 : rng.Next(top.Count);
            if (i < 0 || i >= top.Count) i = 0;
            return top[i];
        }
    }
}
