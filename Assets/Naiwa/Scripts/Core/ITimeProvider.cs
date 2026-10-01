using System;
using System.Diagnostics;

namespace Naiwa.Core
{
    /// <summary>所有时间逻辑都通过此接口取时间（C9），便于单元测试。</summary>
    public interface ITimeProvider
    {
        /// <summary>单调递增的秒数（不受系统时间回拨影响）。</summary>
        double RealtimeSeconds { get; }

        /// <summary>单调递增的毫秒数。</summary>
        long MonotonicMs { get; }

        DateTime LocalNow { get; }
    }

    public sealed class SystemTimeProvider : ITimeProvider
    {
        readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public double RealtimeSeconds => _stopwatch.Elapsed.TotalSeconds;
        public long MonotonicMs => _stopwatch.ElapsedMilliseconds;
        public DateTime LocalNow => DateTime.Now;
    }
}
