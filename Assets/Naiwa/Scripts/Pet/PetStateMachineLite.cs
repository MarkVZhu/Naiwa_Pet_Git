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
    /// 3 状态状态机（§V.3.4 + v1.0 §6.4）。
    /// 请求来源：点桌宠（随机已解锁表情）、图鉴点播（可打断表情）、解锁揭晓（挂起，只保留最近一个）、
    /// 形态过场（进化 / 切换，与进化同级，会打断表情）。
    /// </summary>
    public sealed class PetStateMachineLite
    {
        readonly MonoBehaviour _host;
        readonly GameConfig _config;
        readonly FormLibrary _library;
        readonly PetAnimatorLite _animator;
        readonly PetHitTester _hitTester;
        readonly EmotePicker _picker;
        readonly EmoteCatalog _catalog;
        readonly UnlockService _unlocks;
        readonly FormSwitchService _forms;
        readonly EvolutionFx _fx;
        readonly ITimeProvider _time;

        SpriteSequence _currentEmote;
        string _currentEmoteId;
        string _pendingReveal;
        double _retryTransitionAt;
        readonly Queue<EmoteDef> _debugQueue = new Queue<EmoteDef>();
        readonly List<EmoteDef> _candidates = new List<EmoteDef>();
        float _idleTimeForDebugQueue;
        const float DebugQueueIdleGapSec = 1f;

        public PetState State { get; private set; }
        public bool IsDragging { get; set; }
        public FormId DisplayedForm => _forms.DisplayForm;
        public string CurrentEmoteId => State == PetState.Emote ? _currentEmoteId : null;
        public string PendingReveal => _pendingReveal;

        /// <summary>点到奶蛙但当前阶段没有已解锁表情（§6.2）。</summary>
        public event Action EmptyPoke;
        /// <summary>揭晓表情开始播放（参数为 id），用于显示「新！」。</summary>
        public event Action<string> RevealStarted;
        /// <summary>形态过场开始 / 结束。</summary>
        public event Action<FormTransition> TransitionStarted;
        public event Action<FormTransition> TransitionCompleted;

        public PetStateMachineLite(MonoBehaviour host, GameConfig config, FormLibrary library, PetAnimatorLite animator,
            PetHitTester hitTester, EmotePicker picker, EmoteCatalog catalog, UnlockService unlocks,
            FormSwitchService forms, EvolutionFx fx, ITimeProvider time)
        {
            _host = host;
            _config = config;
            _library = library;
            _animator = animator;
            _hitTester = hitTester;
            _picker = picker;
            _catalog = catalog;
            _unlocks = unlocks;
            _forms = forms;
            _fx = fx;
            _time = time;
            _animator.Finished += OnAnimatorFinished;
        }

        /// <summary>启动：显示存档里的 displayForm 的 idle。</summary>
        public void Begin() => ShowFormImmediate(_forms.DisplayForm);

        // ---------- 请求 ----------

        public void OnPetClicked()
        {
            if (State != PetState.Idle) return; // Emote 中丢弃、不排队；Evolving 中丢弃

            _candidates.Clear();
            foreach (var e in _catalog.ForForm(DisplayedForm))
                if (_unlocks.IsUnlocked(e.Id)) _candidates.Add(e);

            var def = _picker.Pick(DisplayedForm, _candidates);
            var seq = def != null ? _library.GetOrLoadEmote(def) : null;
            if (seq == null)
            {
                EmptyPoke?.Invoke();
                return;
            }
            PlayEmote(def, seq);
        }

        /// <summary>图鉴点播（§6.4）：只接受当前阶段、已解锁的表情。返回是否开始播放。</summary>
        public bool RequestPlay(string id)
        {
            var def = _catalog.Get(id);
            if (def == null || def.Form != DisplayedForm || !_unlocks.IsUnlocked(id)) return false;
            if (State == PetState.Evolving) return false;
            if (State == PetState.Emote && !_config.collection.interruptEmoteOnPlay) return false;

            var seq = _library.GetOrLoadEmote(def);
            if (seq == null) return false;
            PlayEmote(def, seq);
            return true;
        }

        /// <summary>挂起一次解锁揭晓（只保留最近一个）。</summary>
        public void QueueReveal(string id) => _pendingReveal = id;

        public void Tick(float dt)
        {
            if (State != PetState.Evolving && _time.RealtimeSeconds >= _retryTransitionAt)
            {
                var t = _forms.TryBegin(!IsDragging);
                if (t.HasValue)
                {
                    _host.StartCoroutine(TransitionRoutine(t.Value));
                    return;
                }
            }

            if (State == PetState.Idle && !IsDragging && _pendingReveal != null)
            {
                string id = _pendingReveal;
                _pendingReveal = null;
                var def = _catalog.Get(id);
                var seq = def != null && def.Form == DisplayedForm ? _library.GetOrLoadEmote(def) : null;
                if (seq != null)
                {
                    PlayEmote(def, seq);
                    RevealStarted?.Invoke(id);
                    return;
                }
                // 不属于当前阶段：作废（卡片已经提示过）
            }

            if (State == PetState.Idle && _debugQueue.Count > 0)
            {
                _idleTimeForDebugQueue += dt;
                if (_idleTimeForDebugQueue >= DebugQueueIdleGapSec)
                {
                    var def = _debugQueue.Dequeue();
                    var seq = _library.GetOrLoadEmote(def);
                    if (seq != null) PlayEmote(def, seq);
                }
            }
        }

        // ---------- 调试 ----------

        /// <summary>依次播放当前阶段全部表情（含未解锁的，用于检查衔接）。</summary>
        public void DebugPlayAllEmotes()
        {
            _debugQueue.Clear();
            foreach (var e in _catalog.ForForm(DisplayedForm)) _debugQueue.Enqueue(e);
            _idleTimeForDebugQueue = DebugQueueIdleGapSec;
        }

        // ---------- 内部 ----------

        void ShowFormImmediate(FormId form)
        {
            _debugQueue.Clear();
            _currentEmote = null;
            _currentEmoteId = null;
            var loaded = _library.Load(form);
            _hitTester.SetShapeFrom(loaded.HitShapeSprite, loaded.DisplayScale);
            if (loaded.Idle != null) _animator.ShowImmediate(loaded.Idle, true);
            else Debug.LogError($"[Naiwa] 阶段 {form} 没有任何可显示的 idle");
            _library.UnloadAllExcept(form);
            State = PetState.Idle;
        }

        void PlayEmote(EmoteDef def, SpriteSequence seq)
        {
            _currentEmote = seq;
            _currentEmoteId = def.Id;
            _animator.Play(seq, false, _config.pet.emoteFadeSec);
            State = PetState.Emote;
        }

        void ReturnToIdle()
        {
            _currentEmote = null;
            _currentEmoteId = null;
            _idleTimeForDebugQueue = 0f;
            var form = _library.Get(DisplayedForm);
            if (form?.Idle != null) _animator.Play(form.Idle, true, _config.pet.emoteFadeSec);
            State = PetState.Idle;
        }

        void OnAnimatorFinished(SpriteSequence seq)
        {
            if (State == PetState.Emote && ReferenceEquals(seq, _currentEmote))
                ReturnToIdle();
        }

        IEnumerator TransitionRoutine(FormTransition t)
        {
            var previousState = State;
            State = PetState.Evolving;
            _debugQueue.Clear();
            TransitionStarted?.Invoke(t);

            bool ok = false;
            yield return _library.LoadAsync(t.To, _config.evolution.loadTimeoutSec, r => ok = r);
            var target = ok ? _library.Get(t.To) : null;
            if (target?.Idle == null)
            {
                Debug.LogError($"[Naiwa] 形态过场 {t} 失败：目标阶段加载失败，{_config.evolution.retryCooldownSec:0}s 后重试");
                _forms.Abort();
                _retryTransitionAt = _time.RealtimeSeconds + _config.evolution.retryCooldownSec;
                if (previousState == PetState.Emote && _currentEmote != null) State = PetState.Emote;
                else ReturnToIdle();
                yield break;
            }

            // 直接打断表情，烟雾会盖住画面
            _currentEmote = null;
            _currentEmoteId = null;
            yield return _fx.Run(_animator, target.Idle, () =>
            {
                _hitTester.SetShapeFrom(target.HitShapeSprite, target.DisplayScale);
                _forms.ApplySwap();
            });

            _forms.Complete();
            if (t.From != t.To) _library.UnloadAllExcept(t.To);
            State = PetState.Idle;
            _idleTimeForDebugQueue = 0f;
            TransitionCompleted?.Invoke(t);
        }
    }
}
