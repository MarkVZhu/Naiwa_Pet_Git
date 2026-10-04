using System;

namespace Naiwa.Growth
{
    /// <summary>
    /// 成长值与已解锁最高形态（v1.0 §1.4、§4.5）。
    /// HighestForm 是"已完成解锁"的形态；成长值跨过阈值但还没播完进化时，HasPendingUnlock 为 true。
    /// 一次跨过两个阈值会依次解锁（CommitUnlock 每次只升一级）。成长值永不消耗，形态只升不降（调试除外）。
    /// </summary>
    public sealed class GrowthService
    {
        readonly long _eggToSmall;
        readonly long _smallToBig;

        public long Growth { get; private set; }
        public FormId HighestForm { get; private set; }

        public event Action<long> GrowthChanged;
        /// <summary>参数为新的 HighestForm。</summary>
        public event Action<FormId> HighestFormChanged;

        public GrowthService(long eggToSmall, long smallToBig, long growth = 0, FormId highestForm = FormId.Egg)
        {
            _eggToSmall = eggToSmall;
            _smallToBig = smallToBig;
            Growth = Math.Max(0, growth);
            HighestForm = Enum.IsDefined(typeof(FormId), highestForm) ? highestForm : FormId.Egg;
        }

        /// <summary>按成长值应达到的形态（不低于 HighestForm）。</summary>
        public FormId TargetForm
        {
            get
            {
                var f = FormForGrowth(Growth);
                return f > HighestForm ? f : HighestForm;
            }
        }

        public bool HasPendingUnlock => FormForGrowth(Growth) > HighestForm;

        /// <summary>下一个进化节点；大奶蛙返回 null。按 HighestForm 计算。</summary>
        public long? NextThreshold => HighestForm.HasNext() ? ThresholdOf(HighestForm.Next()) : (long?)null;

        public FormId FormForGrowth(long growth)
        {
            if (growth >= _smallToBig) return FormId.Big;
            if (growth >= _eggToSmall) return FormId.Small;
            return FormId.Egg;
        }

        /// <summary>解锁该形态所需的成长值（奶蛋为 0）。</summary>
        public long ThresholdOf(FormId form)
        {
            switch (form)
            {
                case FormId.Small: return _eggToSmall;
                case FormId.Big: return _smallToBig;
                default: return 0;
            }
        }

        public void Add(long n)
        {
            if (n <= 0) return;
            Growth = Growth > long.MaxValue - n ? long.MaxValue : Growth + n;
            GrowthChanged?.Invoke(Growth);
        }

        /// <summary>进化过场到换形态那一刻调用：HighestForm +1。</summary>
        public void CommitUnlock()
        {
            if (!HasPendingUnlock) return;
            HighestForm = HighestForm.Next();
            HighestFormChanged?.Invoke(HighestForm);
        }

        /// <summary>调试：强制设置最高形态与成长值（唯一允许降级的入口）。</summary>
        public void ForceState(FormId highest, long growth)
        {
            Growth = Math.Max(0, growth);
            bool changed = HighestForm != highest;
            HighestForm = highest;
            GrowthChanged?.Invoke(Growth);
            if (changed) HighestFormChanged?.Invoke(HighestForm);
        }
    }

    /// <summary>成长进度条比例（v1.0 §4.3），按 HighestForm 计算，与当前显示形态无关。</summary>
    public static class GrowthProgress
    {
        public static float Ratio(long growth, FormId highestForm, long eggToSmall, long smallToBig)
        {
            if (highestForm >= FormId.Big) return 1f;
            long start = highestForm == FormId.Small ? eggToSmall : 0;
            long end = highestForm == FormId.Small ? smallToBig : eggToSmall;
            if (end <= start) return 1f;
            double r = (growth - start) / (double)(end - start);
            return (float)Math.Max(0.0, Math.Min(1.0, r));
        }

        public static float Ratio(GrowthService g) =>
            Ratio(g.Growth, g.HighestForm, g.ThresholdOf(FormId.Small), g.ThresholdOf(FormId.Big));

        public static bool IsMax(FormId highestForm) => highestForm >= FormId.Big;
    }
}
