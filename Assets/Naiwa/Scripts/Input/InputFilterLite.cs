using System;
using System.Collections.Generic;

namespace Naiwa.Input
{
    /// <summary>
    /// §4.2 过滤管线的 v0.1 裁剪版，只做第 0–4 步：
    /// 0 只处理按下 → 1 暂停丢弃 → 2 注入丢弃 → 3 按住去重 → 4 每秒最多 N 次。
    /// 纯 C# 类，可单元测试。注意：KeyId 只存在于内存中的按住集合（C6）。
    /// </summary>
    public sealed class InputFilterLite
    {
        const long RateWindowMs = 1000;

        readonly HashSet<int> _heldKeys = new HashSet<int>();
        readonly Queue<long> _countedTimes = new Queue<long>();
        readonly List<int> _pruneScratch = new List<int>();

        public int MaxCountPerSecond { get; set; }
        public bool IgnoreInjected { get; set; }
        public bool Paused { get; set; }

        public int Counted { get; private set; }
        public int DroppedPaused { get; private set; }
        public int DroppedInjected { get; private set; }
        public int DroppedRepeat { get; private set; }
        public int DroppedRateLimit { get; private set; }

        public InputFilterLite(int maxCountPerSecond, bool ignoreInjected)
        {
            MaxCountPerSecond = maxCountPerSecond;
            IgnoreInjected = ignoreInjected;
        }

        /// <summary>返回 true 表示这次输入通过全部过滤，应计入成长值。</summary>
        public bool Process(in RawInputEvent e) => Process(e, out _);

        /// <param name="isPhysicalPress">
        /// 是否为一次新的物理按下（§4.2 OnAnyPress）：不受暂停、注入、限速影响，只排除长按连发。用于挤压回弹等即时反馈。
        /// </param>
        public bool Process(in RawInputEvent e, out bool isPhysicalPress)
        {
            isPhysicalPress = false;
            if (e.Kind == RawKind.KeyUp)
            {
                _heldKeys.Remove(e.KeyId);
                return false;
            }

            // 0：只处理 KeyDown / MouseDown（v0.1 不计滚轮）
            if (e.Kind != RawKind.KeyDown && e.Kind != RawKind.MouseDown)
                return false;

            // 按住集合跟踪物理状态，与是否计数无关
            bool isRepeat = e.Kind == RawKind.KeyDown && !_heldKeys.Add(e.KeyId);
            isPhysicalPress = !isRepeat;

            // 1：暂停
            if (Paused) { DroppedPaused++; return false; }

            // 2：注入
            if (e.Injected && IgnoreInjected) { DroppedInjected++; return false; }

            // 3：长按连发
            if (isRepeat) { DroppedRepeat++; return false; }

            // 4：1000ms 滑动窗口限速
            long now = e.TimestampMs;
            if (_countedTimes.Count > 0 && now < LastCountedTime - 10_000)
                _countedTimes.Clear(); // 时间戳回绕（GetTickCount 约 49 天）

            while (_countedTimes.Count > 0 && now - _countedTimes.Peek() >= RateWindowMs)
                _countedTimes.Dequeue();

            if (_countedTimes.Count >= MaxCountPerSecond) { DroppedRateLimit++; return false; }

            _countedTimes.Enqueue(now);
            _lastCounted = now;
            Counted++;
            return true;
        }

        long _lastCounted;
        long LastCountedTime => _lastCounted;

        /// <summary>
        /// 防按住集合泄漏（锁屏/UAC 时可能丢 KeyUp）：用外部查询（GetAsyncKeyState）核对，已松开的移出。
        /// </summary>
        public void PruneHeldKeys(Func<int, bool> isStillDown)
        {
            if (isStillDown == null || _heldKeys.Count == 0) return;
            _pruneScratch.Clear();
            foreach (int k in _heldKeys)
                if (!isStillDown(k)) _pruneScratch.Add(k);
            foreach (int k in _pruneScratch)
                _heldKeys.Remove(k);
        }

        public int HeldKeyCount => _heldKeys.Count;
    }
}
