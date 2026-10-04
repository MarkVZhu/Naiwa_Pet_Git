using System;
using UnityEngine;

namespace Naiwa.Pet
{
    /// <summary>
    /// 双层淡切播放（§V.3.3）。两个 SpriteRenderer（层 A、层 B）同位置，材质用默认 Sprites-Default（预乘 alpha，C7）。
    /// Play：新序列放到隐藏层从第 0 帧开始，fadeSec 内交叉渐变，渐变期间两层都推进帧。
    /// 非循环序列播到最后一帧触发 Finished 并停在最后一帧。帧推进用累计时间，不受帧率影响。
    /// 由 GameBootstrap 每帧调用 Tick。
    /// </summary>
    public sealed class PetAnimatorLite : MonoBehaviour
    {
        public SpriteRenderer layerA;
        public SpriteRenderer layerB;

        public sealed class Layer
        {
            internal SpriteRenderer Renderer;
            public SpriteSequence Sequence { get; internal set; }
            public bool Loop { get; internal set; }
            public float Elapsed { get; internal set; }
            internal bool FinishedRaised;
            float _alpha;

            public float Alpha
            {
                get => _alpha;
                set
                {
                    _alpha = Mathf.Clamp01(value);
                    Renderer.color = new Color(1f, 1f, 1f, _alpha);
                    Renderer.enabled = _alpha > 0.001f && Sequence != null;
                }
            }

            /// <summary>相对缩放（进化特效的鼓起/回弹用）；实际缩放 = Scale × 序列所属阶段的 DisplayScale。</summary>
            public float Scale
            {
                get => _scale;
                set
                {
                    _scale = value;
                    float s = value * (Sequence != null ? Sequence.DisplayScale : 1f);
                    Renderer.transform.localScale = new Vector3(s, s, 1f);
                }
            }
            float _scale = 1f;

            public int CurrentFrame => Sequence?.FrameIndexAt(Elapsed, Loop) ?? -1;

            internal void Assign(SpriteSequence seq, bool loop)
            {
                Sequence = seq;
                Loop = loop;
                Elapsed = 0f;
                FinishedRaised = false;
                Scale = _scale;
                ApplyFrame();
            }

            internal void Clear()
            {
                Sequence = null;
                Renderer.sprite = null;
                Scale = 1f;
                Alpha = 0f;
            }

            internal void ApplyFrame()
            {
                int idx = CurrentFrame;
                Renderer.sprite = idx >= 0 ? Sequence.Frames[idx] : null;
            }
        }

        /// <summary>参数为播完的序列（只有非循环序列会触发）。</summary>
        public event Action<SpriteSequence> Finished;

        Layer _current, _other;
        bool _fading;
        float _fadeElapsed, _fadeDuration;
        SpriteSequence _pendingFinished1, _pendingFinished2;

        public Layer Current => EnsureInit()._current;
        public Layer Other => EnsureInit()._other;
        public bool IsFading => _fading;

        PetAnimatorLite EnsureInit()
        {
            if (_current != null) return this;
            _current = new Layer { Renderer = layerA };
            _other = new Layer { Renderer = layerB };
            _current.Clear();
            _other.Clear();
            return this;
        }

        /// <summary>立即显示（无淡切）。</summary>
        public void ShowImmediate(SpriteSequence seq, bool loop)
        {
            EnsureInit();
            _fading = false;
            AssignOther(seq, loop, 1f);
            CommitOther();
        }

        public void Play(SpriteSequence seq, bool loop, float fadeSec)
        {
            EnsureInit();
            CompleteFade();
            if (fadeSec <= 0f || _current.Sequence == null)
            {
                ShowImmediate(seq, loop);
                return;
            }

            AssignOther(seq, loop, 0f);
            _fading = true;
            _fadeElapsed = 0f;
            _fadeDuration = fadeSec;
        }

        /// <summary>立刻结束正在进行的淡切（新层成为当前层）。</summary>
        public void CompleteFade()
        {
            if (!_fading) return;
            _fading = false;
            _other.Alpha = 1f;
            CommitOther();
        }

        /// <summary>把序列放到"另一层"（总是排序在上方），从第 0 帧开始。供进化特效直接控制。</summary>
        public void AssignOther(SpriteSequence seq, bool loop, float alpha)
        {
            EnsureInit();
            _other.Assign(seq, loop);
            _other.Scale = 1f;
            _other.Renderer.sortingOrder = 1;
            _current.Renderer.sortingOrder = 0;
            _other.Alpha = alpha;
        }

        /// <summary>另一层成为当前层，旧的当前层清空。</summary>
        public void CommitOther()
        {
            EnsureInit();
            var old = _current;
            _current = _other;
            _other = old;
            _current.Alpha = 1f;
            _current.Scale = 1f;
            _other.Clear();
        }

        public void Tick(float dt)
        {
            EnsureInit();
            _pendingFinished1 = Advance(_current, dt);
            _pendingFinished2 = Advance(_other, dt);

            if (_fading)
            {
                _fadeElapsed += dt;
                float k = Mathf.Clamp01(_fadeElapsed / _fadeDuration);
                // 不透明交叉：前半段新层（在上）淡入、旧层保持不透明；后半段新层已不透明、旧层在下面淡出。
                // 两层重叠处合成 alpha 始终为 1，不会出现「同时半透明」的闪烁。
                _other.Alpha = CrossfadeTopAlpha(k);
                _current.Alpha = CrossfadeBottomAlpha(k);
                if (k >= 1f)
                {
                    _fading = false;
                    CommitOther();
                }
            }

            var f1 = _pendingFinished1;
            var f2 = _pendingFinished2;
            _pendingFinished1 = _pendingFinished2 = null;
            if (f1 != null) Finished?.Invoke(f1);
            if (f2 != null) Finished?.Invoke(f2);
        }

        public static float CrossfadeTopAlpha(float k) => Mathf.Clamp01(k * 2f);
        public static float CrossfadeBottomAlpha(float k) => Mathf.Clamp01(2f - k * 2f);

        static SpriteSequence Advance(Layer layer, float dt)
        {
            var seq = layer.Sequence;
            if (seq == null || seq.FrameCount == 0) return null;
            layer.Elapsed += dt;
            layer.ApplyFrame();
            if (!layer.Loop && !layer.FinishedRaised && layer.Elapsed * seq.Fps >= seq.FrameCount)
            {
                layer.FinishedRaised = true;
                return seq;
            }
            return null;
        }
    }
}
