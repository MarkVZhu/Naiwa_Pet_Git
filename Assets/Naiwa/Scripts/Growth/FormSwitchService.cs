using System;

namespace Naiwa.Growth
{
    public enum FormTransitionKind
    {
        /// <summary>成长值解锁了新形态（autoEvolveOnUnlock）。</summary>
        Unlock,
        /// <summary>玩家在右键菜单里切换形态，或调试「设为某阶段」。</summary>
        Switch,
    }

    public readonly struct FormTransition
    {
        public readonly FormId From;
        public readonly FormId To;
        public readonly FormTransitionKind Kind;

        public FormTransition(FormId from, FormId to, FormTransitionKind kind)
        {
            From = from;
            To = to;
            Kind = kind;
        }

        public override string ToString() => $"{Kind}:{From}->{To}";
    }

    /// <summary>
    /// 当前显示形态 displayForm 与形态切换（v1.0 §6.5）。纯 C#（C15）。
    /// 每次只进行一个过场：TryBegin 取下一个过场 → 播烟雾 → ApplySwap（换形态那一刻）→ Complete。
    /// 规则：每新解锁一个形态播一次烟雾，起点是当前显示的形态；切换只能选 ≤ HighestForm 的形态。
    /// </summary>
    public sealed class FormSwitchService
    {
        readonly GrowthService _growth;
        FormId? _pendingSwitch;
        FormTransition? _active;
        bool _swapped;

        public bool AutoEvolveOnUnlock { get; set; }
        public FormId DisplayForm { get; private set; }
        public bool IsTransitioning => _active.HasValue;
        public FormTransition? Active => _active;
        public FormId? PendingSwitch => _pendingSwitch;

        /// <summary>autoEvolveOnUnlock=false 时解锁新形态的提示（参数为新形态）。</summary>
        public event Action<FormId> UnlockNotice;
        public event Action<FormId> DisplayFormChanged;

        public FormSwitchService(GrowthService growth, FormId displayForm, bool autoEvolveOnUnlock)
        {
            _growth = growth;
            AutoEvolveOnUnlock = autoEvolveOnUnlock;
            if (!Enum.IsDefined(typeof(FormId), displayForm)) displayForm = FormId.Egg;
            DisplayForm = displayForm > growth.HighestForm ? growth.HighestForm : displayForm;
        }

        public GrowthService Growth => _growth;

        public bool CanSelect(FormId form) => form <= _growth.HighestForm;

        /// <summary>请求切换到某形态。返回 false：形态未解锁、正在过场，或就是当前形态（无操作）。</summary>
        public bool RequestSwitch(FormId form)
        {
            if (IsTransitioning || !CanSelect(form)) return false;
            if (form == DisplayForm)
            {
                _pendingSwitch = null;
                return false;
            }
            _pendingSwitch = form;
            return true;
        }

        /// <summary>有没有等待开始的过场（解锁或切换）。不改变状态。</summary>
        public bool HasPendingWork =>
            !IsTransitioning && ((_growth.HasPendingUnlock && AutoEvolveOnUnlock) || (_pendingSwitch.HasValue && _pendingSwitch.Value != DisplayForm));

        /// <summary>
        /// 取下一个要播放的过场。canStart=false（例如正在拖动）时只处理不需要动画的事（autoEvolve=false 的解锁提示）。
        /// </summary>
        public FormTransition? TryBegin(bool canStart)
        {
            if (IsTransitioning) return null;

            if (!AutoEvolveOnUnlock)
            {
                while (_growth.HasPendingUnlock)
                {
                    _growth.CommitUnlock();
                    UnlockNotice?.Invoke(_growth.HighestForm);
                }
            }

            if (!canStart) return null;

            if (_growth.HasPendingUnlock)
            {
                var target = _growth.HighestForm.Next();
                _pendingSwitch = null;
                return Start(new FormTransition(DisplayForm, target, FormTransitionKind.Unlock));
            }

            if (_pendingSwitch.HasValue)
            {
                var to = _pendingSwitch.Value;
                _pendingSwitch = null;
                if (to == DisplayForm || !CanSelect(to)) return null;
                return Start(new FormTransition(DisplayForm, to, FormTransitionKind.Switch));
            }

            return null;
        }

        /// <summary>烟雾中换形态的那一刻：提交解锁（进度条在此刻归零到新阶段）并更新 displayForm。</summary>
        public void ApplySwap()
        {
            if (!_active.HasValue || _swapped) return;
            var t = _active.Value;
            if (t.Kind == FormTransitionKind.Unlock && _growth.HighestForm < t.To) _growth.CommitUnlock();
            _swapped = true;
            if (DisplayForm != t.To)
            {
                DisplayForm = t.To;
                DisplayFormChanged?.Invoke(DisplayForm);
            }
        }

        public void Complete()
        {
            if (!_active.HasValue) return;
            ApplySwap();
            _active = null;
            _swapped = false;
        }

        /// <summary>过场失败（下一阶段加载失败）：什么都不改，解锁仍然挂起，稍后重试。</summary>
        public void Abort()
        {
            _active = null;
            _swapped = false;
        }

        /// <summary>
        /// 调试「设为某阶段」（v1.0 §6.6）：growth = 该形态阈值，HighestForm = 该形态（允许降级），
        /// 显示形态通过一次切换过场变过去。正在过场时拒绝。
        /// </summary>
        public bool DebugSetForm(FormId form)
        {
            if (IsTransitioning) return false;
            _growth.ForceState(form, _growth.ThresholdOf(form));
            _pendingSwitch = form != DisplayForm ? form : (FormId?)null;
            return true;
        }

        FormTransition? Start(FormTransition t)
        {
            if (t.To == t.From) return null;
            _active = t;
            _swapped = false;
            return t;
        }
    }
}
