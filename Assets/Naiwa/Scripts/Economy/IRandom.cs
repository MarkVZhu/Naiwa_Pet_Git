using System;

namespace Naiwa.Economy
{
    /// <summary>随机数抽象（C15），便于单元测试。</summary>
    public interface IRandom
    {
        /// <summary>返回 [0, maxExclusive) 的整数。</summary>
        int Next(int maxExclusive);
    }

    public sealed class SystemRandom : IRandom
    {
        readonly Random _rng;

        public SystemRandom(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public int Next(int maxExclusive) => maxExclusive <= 0 ? 0 : _rng.Next(maxExclusive);
    }
}
