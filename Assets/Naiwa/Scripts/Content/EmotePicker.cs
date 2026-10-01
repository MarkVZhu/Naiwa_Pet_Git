using System;
using System.Collections.Generic;
using Naiwa.Growth;

namespace Naiwa.Content
{
    /// <summary>
    /// 在当前阶段的表情里均匀随机选一个。
    /// avoidImmediateRepeat=true 时排除上一次播过的（2 个候选时变成轮流）。
    /// </summary>
    public sealed class EmotePicker
    {
        readonly Random _rng;
        readonly Dictionary<FormId, object> _lastPicked = new Dictionary<FormId, object>();
        readonly List<int> _scratch = new List<int>();

        public bool AvoidImmediateRepeat { get; set; }

        public EmotePicker(bool avoidImmediateRepeat, Random rng = null)
        {
            AvoidImmediateRepeat = avoidImmediateRepeat;
            _rng = rng ?? new Random();
        }

        public T Pick<T>(FormId form, IReadOnlyList<T> candidates) where T : class
        {
            if (candidates == null || candidates.Count == 0) return null;

            T picked;
            if (candidates.Count == 1)
            {
                picked = candidates[0];
            }
            else
            {
                _scratch.Clear();
                _lastPicked.TryGetValue(form, out var last);
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i] == null) continue;
                    if (AvoidImmediateRepeat && last != null && ReferenceEquals(candidates[i], last)) continue;
                    _scratch.Add(i);
                }

                if (_scratch.Count == 0)
                {
                    for (int i = 0; i < candidates.Count; i++)
                        if (candidates[i] != null) _scratch.Add(i);
                    if (_scratch.Count == 0) return null;
                }

                picked = candidates[_scratch[_rng.Next(_scratch.Count)]];
            }

            _lastPicked[form] = picked;
            return picked;
        }

        public void Reset() => _lastPicked.Clear();
    }
}
