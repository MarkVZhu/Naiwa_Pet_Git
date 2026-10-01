using System;
using System.Collections;
using System.Collections.Generic;
using Naiwa.Content;
using Naiwa.Core;
using Naiwa.Fx;
using Naiwa.Growth;
using UnityEngine;

namespace Naiwa.Pet
{
    public enum PetState { Idle, Emote, Evolving }

    /// <summary>
    /// 只有 3 个状态的状态机（§V.3.4）。
    /// Idle + 点击 → Emote；Emote 播完 → 淡切回 Idle（idle 从第 0 帧开始）；
    /// 有待进化且不在拖动中 → Evolving（会直接打断表情）；Evolving 期间点击忽略，挂起的进化在本次结束后立刻开始。
    /// </summary>
    public sealed class PetStateMachineLite
    {
        readonly MonoBehaviour _host;
        readonly GameConfig _config;
        readonly FormLibrary _library;
        readonly PetAnimatorLite _animator;
        readonly PetHitTester _hitTester;
        readonly EmotePicker _picker;
        readonly GrowthService _growth;
        readonly EvolutionFx _fx;
        readonly ITimeProvider _time;

        SpriteSequence _currentEmote;
        FormId _displayedForm;
        double _retryEvolutionAt;
        readonly Queue<SpriteSequence> _debugQueue = new Queue<SpriteSequence>();
        float _idleTimeForDebugQueue;
        const float DebugQueueIdleGapSec = 1f;

        public PetState State { get; private set; }
        public bool IsDragging { get; set; }
        public FormId DisplayedForm => _displayedForm;
        public string CurrentEmoteId => _currentEmote?.Id;

        /// <summary>进化完成（参数为新形态）。用于存档。</summary>
        public event Action<FormId> EvolutionCompleted;

        public PetStateMachineLite(MonoBehaviour host, GameConfig config, FormLibrary library, PetAnimatorLite animator,
            PetHitTester hitTester, EmotePicker picker, GrowthService growth, EvolutionFx fx, ITimeProvider time)
        {
            _host = host;
            _config = config;
            _library = library;
            _animator = animator;
            _hitTester = hitTester;
            _picker = picker;
            _growth = growth;
            _fx = fx;
            _time = time;
            _animator.Finished += OnAnimatorFinished;
        }

        /// <summary>启动：显示当前形态的 idle。</summary>
        public void Begin()
        {
            ShowFormImmediate(_growth.Form);
        }

        public void OnPetClicked()
        {
            if (State != PetState.Idle) return; // Emote 中忽略、不排队；Evolving 中忽略
            var form = _library.Get(_displayedForm);
            if (form == null) return;
            var emote = _picker.Pick(_displayedForm, form.Emotes);
            if (emote == null) return;
            PlayEmote(emote);
        }

        public void Tick(float dt)
        {
            if (State != PetState.Evolving && _growth.HasPendingEvolution && !IsDragging && _time.RealtimeSeconds >= _retryEvolutionAt)
            {
                _host.StartCoroutine(EvolveRoutine());
                return;
            }

            if (State == PetState.Idle && _debugQueue.Count > 0)
            {
                _idleTimeForDebugQueue += dt;
                if (_idleTimeForDebugQueue >= DebugQueueIdleGapSec)
                    PlayEmote(_debugQueue.Dequeue());
            }
        }

        // ---------- 调试 ----------

        public void DebugPlayAllEmotes()
        {
            var form = _library.Get(_displayedForm);
            if (form == null) return;
            _debugQueue.Clear();
            foreach (var e in form.Emotes) _debugQueue.Enqueue(e);
            _idleTimeForDebugQueue = DebugQueueIdleGapSec;
        }

        /// <summary>直接换形态，不放烟雾（调试快捷键 1/2/3、重置为奶蛋）。</summary>
        public void DebugSwitchFormImmediate(FormId form)
        {
            if (State == PetState.Evolving) return;
            _growth.ForceState(form, _growth.ThresholdOf(form));
            ShowFormImmediate(form);
        }

        public void DebugResetToEgg()
        {
            if (State == PetState.Evolving) return;
            _growth.ForceState(FormId.Egg, 0);
            ShowFormImmediate(FormId.Egg);
        }

        // ---------- 内部 ----------

        void ShowFormImmediate(FormId form)
        {
            _debugQueue.Clear();
            _currentEmote = null;
            var loaded = _library.Load(form);
            _displayedForm = form;
            _hitTester.SetShapeFrom(loaded.HitShapeSprite);
            if (loaded.Idle != null) _animator.ShowImmediate(loaded.Idle, true);
            else Debug.LogError($"[Naiwa] 阶段 {form} 没有任何可显示的 idle");
            _library.UnloadAllExcept(form);
            State = PetState.Idle;
        }

        void PlayEmote(SpriteSequence emote)
        {
            _currentEmote = emote;
            _animator.Play(emote, false, _config.pet.emoteFadeSec);
            State = PetState.Emote;
        }

        void ReturnToIdle()
        {
            _currentEmote = null;
            _idleTimeForDebugQueue = 0f;
            var form = _library.Get(_displayedForm);
            if (form?.Idle != null) _animator.Play(form.Idle, true, _config.pet.emoteFadeSec);
            State = PetState.Idle;
        }

        void OnAnimatorFinished(SpriteSequence seq)
        {
            if (State == PetState.Emote && ReferenceEquals(seq, _currentEmote))
                ReturnToIdle();
        }

        IEnumerator EvolveRoutine()
        {
            State = PetState.Evolving;
            _debugQueue.Clear();
            var from = _growth.Form;
            var to = from.Next();

            bool ok = false;
            yield return _library.LoadAsync(to, _config.evolution.loadTimeoutSec, r => ok = r);
            var target = ok ? _library.Get(to) : null;
            if (target?.Idle == null)
            {
                Debug.LogError($"[Naiwa] 进化 {from}→{to} 失败：下一阶段加载失败，留在原阶段，{_config.evolution.retryCooldownSec:0}s 后重试");
                _retryEvolutionAt = _time.RealtimeSeconds + _config.evolution.retryCooldownSec;
                ReturnToIdle();
                yield break;
            }

            _currentEmote = null; // 直接打断表情，烟雾会盖住画面
            yield return _fx.Run(_animator, target.Idle, () =>
            {
                _hitTester.SetShapeFrom(target.HitShapeSprite);
                _displayedForm = to;
            });

            _growth.CommitEvolution();
            _displayedForm = _growth.Form;
            _library.Unload(from);
            State = PetState.Idle;
            _idleTimeForDebugQueue = 0f;
            EvolutionCompleted?.Invoke(_growth.Form);
        }
    }
}
