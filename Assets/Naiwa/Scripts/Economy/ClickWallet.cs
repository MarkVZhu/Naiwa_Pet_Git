using System;

namespace Naiwa.Economy
{
    /// <summary>点击量（可消耗货币，v1.0 §4.1）。余额永不为负。</summary>
    public sealed class ClickWallet
    {
        public long Balance { get; private set; }
        public long LifetimeEarned { get; private set; }
        public long LifetimeSpent { get; private set; }

        /// <summary>(旧值, 新值)</summary>
        public event Action<long, long> Changed;

        public ClickWallet(long balance = 0, long lifetimeEarned = 0, long lifetimeSpent = 0)
        {
            Balance = Math.Max(0, balance);
            LifetimeEarned = Math.Max(0, lifetimeEarned);
            LifetimeSpent = Math.Max(0, lifetimeSpent);
        }

        public void Earn(long n)
        {
            if (n <= 0) return;
            long old = Balance;
            Balance = SafeAdd(Balance, n);
            LifetimeEarned = SafeAdd(LifetimeEarned, n);
            Changed?.Invoke(old, Balance);
        }

        public bool TrySpend(long n)
        {
            if (n <= 0 || n > Balance) return false;
            long old = Balance;
            Balance -= n;
            LifetimeSpent = SafeAdd(LifetimeSpent, n);
            Changed?.Invoke(old, Balance);
            return true;
        }

        static long SafeAdd(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
    }
}
