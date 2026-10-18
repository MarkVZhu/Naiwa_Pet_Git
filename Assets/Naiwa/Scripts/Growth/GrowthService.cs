using System;

namespace Naiwa.Growth
{
    /// <summary>
    /// 成长值与已解锁最高形态（v1.0 §1.4、§4.5）。
    /// HighestForm 是"已完成解锁"的形态；成长值跨过阈值但还没播完进化时，HasPendingUnlock 为 true。
    /// 一次跨过两个阈值会依次解锁（CommitUnlock 每次只升一级）。成长值永不消耗，形态只升不降（调试与重生除外）。
    /// v2.0：大奶蛙成长值达到 rebornGrowth 进入「大奶蛙[可重生]」（CanReborn），重生后回到奶蛋、成长值归零。
    /// </summary>
    public sealed class GrowthService
    {
        readonly long _eggToSmall;
        readonly long _smallToBig;
        readonly long _reborn;

        public long Growth { get; private set; }
        public FormId HighestForm { get; private set; }

        public event Action<long> GrowthChanged;
        /// <summary>参数为新的 HighestForm。</summary>
        public event Action<FormId> HighestFormChanged;

        /// <param name="rebornGrowth">≤ smallToBig 表示不启用重生。</param>
        public GrowthService(long eggToSmall, long smallToBig, long growth = 0, FormId highestForm = FormId.Egg, long rebornGrowth = 0)
        {
            _eggToSmall = eggToSmall;
            _smallToBig = smallToBig;
            _reborn = rebornGrowth;
            Growth = Math.Max(0, growth);
            HighestForm = Enum.IsDefined(typeof(FormId), highestForm) ? highestForm : FormId.Egg;
        }

        public bool RebornEnabled => _reborn > _smallToBig;

        /// <summary>进入「大奶蛙[可重生]」所需的成长值；未启用重生为 0。</summary>
        public long RebornThreshold => RebornEnabled ? _reborn : 0;

        /// <summary>「大奶蛙[可重生]」：已解锁大奶蛙且成长值达到 rebornGrowth。</summary>
        public bool CanReborn => RebornEnabled && HighestForm == FormId.Big && Growth >= _reborn;

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

    /// <summary>
    /// 成长进度条比例（v1.0 §4.3），按 HighestForm 计算，与当前显示形态无关。
    /// v2.0：启用重生时大奶蛙阶段显示 smallToBig → rebornGrowth 的进度，到达后满格（可重生）。
    /// </summary>
    public static class GrowthProgress
    {
        /// <param name="rebornGrowth">≤ smallToBig 表示不启用重生（大奶蛙恒为满格）。</param>
        public static float Ratio(long growth, FormId highestForm, long eggToSmall, long smallToBig, long rebornGrowth = 0)
        {
            long start, end;
            if (highestForm >= FormId.Big)
            {
                if (rebornGrowth <= smallToBig) return 1f;
                start = smallToBig;
                end = rebornGrowth;
            }
            else
            {
                start = highestForm == FormId.Small ? eggToSmall : 0;
                end = highestForm == FormId.Small ? smallToBig : eggToSmall;
            }
            if (end <= start) return 1f;
            double r = (growth - start) / (double)(end - start);
            return (float)Math.Max(0.0, Math.Min(1.0, r));
        }

        public static float Ratio(GrowthService g) =>
            Ratio(g.Growth, g.HighestForm, g.ThresholdOf(FormId.Small), g.ThresholdOf(FormId.Big), g.RebornThreshold);

        /// <summary>满级（不启用重生时的大奶蛙）。</summary>
        public static bool IsMax(FormId highestForm) => highestForm >= FormId.Big;

        /// <summary>进度条是否显示满级颜色：未启用重生的大奶蛙，或已可重生。</summary>
        public static bool IsMax(GrowthService g) => g.RebornEnabled ? g.CanReborn : IsMax(g.HighestForm);
    }
}
