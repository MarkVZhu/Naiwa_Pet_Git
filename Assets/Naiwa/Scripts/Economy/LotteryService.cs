using System;
using System.Collections.Generic;
using Naiwa.Content;
using Naiwa.Core;
using Naiwa.Growth;

namespace Naiwa.Economy
{
    public enum LotteryState { Ready, Cooldown, NotEnoughClicks, PoolEmpty }

    /// <summary>抽奖状态快照（v1.0 §5.2），HUD、图鉴底部、右键菜单共用。</summary>
    public readonly struct LotteryStatus
    {
        public readonly LotteryState State;
        public readonly int Cost;
        /// <summary>还差多少点击量（够了为 0）。Cooldown 状态下也填实际差值。</summary>
        public readonly long ClicksShort;
        public readonly TimeSpan CooldownLeft;
        public readonly TimeSpan CooldownTotal;

        public LotteryStatus(LotteryState state, int cost, long clicksShort, TimeSpan cooldownLeft, TimeSpan cooldownTotal)
        {
            State = state;
            Cost = cost;
            ClicksShort = clicksShort;
            CooldownLeft = cooldownLeft;
            CooldownTotal = cooldownTotal;
        }

        public bool IsReady => State == LotteryState.Ready;
    }

    public readonly struct DrawResult
    {
        public readonly bool Ok;
        public readonly string EmoteId;

        public DrawResult(bool ok, string emoteId)
        {
            Ok = ok;
            EmoteId = emoteId;
        }

        public static DrawResult Fail => new DrawResult(false, null);
    }

    /// <summary>
    /// 抽奖（v1.0 §5）。门槛 = 点击量 ≥ cost，冷却从抽奖那一刻起按真实时间计。只抽未解锁的 Lottery 表情，加权不放回。
    /// C14：扣点击量、写冷却、解锁、落盘在 TryDraw 内一次完成，之后才由 UI 播放转盘和揭晓。
    /// </summary>
    public sealed class LotteryService
    {
        readonly ClickWallet _wallet;
        readonly EmoteCatalog _catalog;
        readonly UnlockService _unlocks;
        readonly IRandom _rng;
        readonly Func<FormId> _currentForm;
        readonly Action _saveNow;
        readonly List<EmoteDef> _scratch = new List<EmoteDef>();

        public int Cost { get; set; }
        public TimeSpan Cooldown { get; set; }
        public LotteryPoolScope Scope { get; set; }
        /// <summary>DateTime.MinValue = 已就绪。</summary>
        public DateTime NextAvailableUtc { get; private set; }
        public int DrawCount { get; private set; }

        public event Action<string> Drawn;

        public LotteryService(ClickWallet wallet, EmoteCatalog catalog, UnlockService unlocks, IRandom rng,
            Func<FormId> currentForm, Action saveNow, int cost, TimeSpan cooldown, LotteryPoolScope scope,
            long nextAvailableUnixMs = 0, int drawCount = 0)
        {
            _wallet = wallet;
            _catalog = catalog;
            _unlocks = unlocks;
            _rng = rng;
            _currentForm = currentForm ?? (() => FormId.Egg);
            _saveNow = saveNow;
            Cost = Math.Max(1, cost);
            Cooldown = cooldown < TimeSpan.Zero ? TimeSpan.Zero : cooldown;
            Scope = scope;
            NextAvailableUtc = FromUnixMs(nextAvailableUnixMs);
            DrawCount = Math.Max(0, drawCount);
        }

        public long NextAvailableUnixMs =>
            NextAvailableUtc <= DateTime.MinValue ? 0 : (long)(NextAvailableUtc - DateTime.UnixEpoch).TotalMilliseconds;

        /// <summary>按 scope 过滤后的抽奖池（未解锁、weight &gt; 0 的 Lottery 表情）。</summary>
        public List<EmoteDef> GetPool(List<EmoteDef> into = null)
        {
            into = into ?? new List<EmoteDef>();
            into.Clear();
            var form = _currentForm();
            bool anyCurrent = false;

            foreach (var e in _catalog.All)
            {
                if (!e.InLotteryPool || _unlocks.IsUnlocked(e.Id)) continue;
                if (Scope == LotteryPoolScope.CurrentFormOnly && e.Form != form) continue;
                into.Add(e);
                if (e.Form == form) anyCurrent = true;
            }

            if (Scope == LotteryPoolScope.CurrentFormFirst && anyCurrent)
                into.RemoveAll(e => e.Form != form);
            return into;
        }

        public int PoolCount => GetPool(_scratch).Count;

        public LotteryStatus GetStatus(DateTime nowUtc)
        {
            long shortBy = Math.Max(0, Cost - _wallet.Balance);
            if (PoolCount == 0)
                return new LotteryStatus(LotteryState.PoolEmpty, Cost, shortBy, TimeSpan.Zero, Cooldown);

            var left = CooldownLeft(nowUtc);
            if (left > TimeSpan.Zero)
                return new LotteryStatus(LotteryState.Cooldown, Cost, shortBy, left, Cooldown);

            if (shortBy > 0)
                return new LotteryStatus(LotteryState.NotEnoughClicks, Cost, shortBy, TimeSpan.Zero, Cooldown);

            return new LotteryStatus(LotteryState.Ready, Cost, 0, TimeSpan.Zero, Cooldown);
        }

        public DrawResult TryDraw(DateTime nowUtc)
        {
            var status = GetStatus(nowUtc);
            if (!status.IsReady) return DrawResult.Fail;

            var picked = PickWeighted(GetPool(_scratch));
            if (picked == null) return DrawResult.Fail;
            if (!_wallet.TrySpend(Cost)) return DrawResult.Fail;

            NextAvailableUtc = nowUtc + Cooldown;
            _unlocks.Unlock(picked.Id, UnlockSource.Lottery);
            DrawCount++;
            _saveNow?.Invoke();
            Drawn?.Invoke(picked.Id);
            return new DrawResult(true, picked.Id);
        }

        /// <summary>调试：无视门槛与冷却、不扣点击量、不写冷却，直接抽一个。</summary>
        public DrawResult DebugDrawFree()
        {
            var picked = PickWeighted(GetPool(_scratch));
            if (picked == null) return DrawResult.Fail;
            _unlocks.Unlock(picked.Id, UnlockSource.Debug);
            DrawCount++;
            _saveNow?.Invoke();
            Drawn?.Invoke(picked.Id);
            return new DrawResult(true, picked.Id);
        }

        public void ClearCooldown() => NextAvailableUtc = DateTime.MinValue;

        /// <summary>剩余冷却。系统时间回拨导致剩余 &gt; 冷却总长时，修正为一个完整冷却（§5.4）。</summary>
        public TimeSpan CooldownLeft(DateTime nowUtc)
        {
            if (NextAvailableUtc <= DateTime.MinValue) return TimeSpan.Zero;
            var left = NextAvailableUtc - nowUtc;
            if (left <= TimeSpan.Zero) return TimeSpan.Zero;
            if (left > Cooldown)
            {
                NextAvailableUtc = nowUtc + Cooldown;
                left = Cooldown;
            }
            return left;
        }

        EmoteDef PickWeighted(List<EmoteDef> pool)
        {
            if (pool.Count == 0) return null;
            long total = 0;
            foreach (var e in pool) total += e.LotteryWeight;
            if (total <= 0) return null;

            int r = _rng.Next((int)Math.Min(total, int.MaxValue));
            long acc = 0;
            foreach (var e in pool)
            {
                acc += e.LotteryWeight;
                if (r < acc) return e;
            }
            return pool[pool.Count - 1];
        }

        static DateTime FromUnixMs(long ms) =>
            ms <= 0 ? DateTime.MinValue : DateTime.UnixEpoch.AddMilliseconds(ms);
    }
}
