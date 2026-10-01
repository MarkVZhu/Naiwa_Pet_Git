using System;

namespace Naiwa.Growth
{
    /// <summary>
    /// v0.1 裁剪版（§5.2）：只保留阈值判断和形态升级。里程碑/随机掉落/周重置不做。
    /// Form 是"已完成进化"的形态；成长值达标但还没播完进化时，HasPendingEvolution 为 true。
    /// 一次跨过两个阈值会依次请求两次进化（Egg→Small，CommitEvolution 后再请求 Small→Big）。
    /// </summary>
    public sealed class GrowthService
    {
        readonly int _eggToSmall;
        readonly int _smallToBig;

        public int Growth { get; private set; }
        public FormId Form { get; private set; }

        /// <summary>参数为请求进化到的目标形态（总是 Form 的下一阶段）。</summary>
        public event Action<FormId> EvolutionRequested;
        public event Action<int> GrowthChanged;

        public GrowthService(int eggToSmall, int smallToBig, int growth = 0, FormId form = FormId.Egg)
        {
            _eggToSmall = eggToSmall;
            _smallToBig = smallToBig;
            Growth = Math.Max(0, growth);
            Form = Enum.IsDefined(typeof(FormId), form) ? form : FormId.Egg;
        }

        public FormId TargetForm => FormForGrowth(Growth);

        public bool HasPendingEvolution => TargetForm > Form;

        /// <summary>下一个进化节点；大奶蛙返回 null。</summary>
        public int? NextThreshold => Form.HasNext() ? ThresholdOf(Form.Next()) : (int?)null;

        public FormId FormForGrowth(int growth)
        {
            if (growth >= _smallToBig) return FormId.Big;
            if (growth >= _eggToSmall) return FormId.Small;
            return FormId.Egg;
        }

        public int ThresholdOf(FormId form)
        {
            switch (form)
            {
                case FormId.Small: return _eggToSmall;
                case FormId.Big: return _smallToBig;
                default: return 0;
            }
        }

        public void Add(int n)
        {
            if (n <= 0) return;
            bool hadPending = HasPendingEvolution;
            Growth = (int)Math.Min((long)Growth + n, int.MaxValue);
            GrowthChanged?.Invoke(Growth);
            if (!hadPending && HasPendingEvolution)
                EvolutionRequested?.Invoke(Form.Next());
        }

        /// <summary>进化过场播完后调用：形态 +1；如果还有待进化，立刻请求下一次。</summary>
        public void CommitEvolution()
        {
            if (!HasPendingEvolution) return;
            Form = Form.Next();
            if (HasPendingEvolution)
                EvolutionRequested?.Invoke(Form.Next());
        }

        /// <summary>调试：直接设成长值（不降级形态）。</summary>
        public void SetGrowth(int growth)
        {
            bool hadPending = HasPendingEvolution;
            Growth = Math.Max(0, growth);
            GrowthChanged?.Invoke(Growth);
            if (!hadPending && HasPendingEvolution)
                EvolutionRequested?.Invoke(Form.Next());
        }

        /// <summary>调试：强制设置形态与成长值（唯一允许降级的入口）。</summary>
        public void ForceState(FormId form, int growth)
        {
            Form = form;
            Growth = Math.Max(0, growth);
            GrowthChanged?.Invoke(Growth);
        }
    }
}
