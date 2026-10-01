using System;
using System.Collections;
using Naiwa.Core;
using Naiwa.Pet;
using UnityEngine;

namespace Naiwa.Fx
{
    /// <summary>
    /// 烟雾粒子衔接进化（§V.3.7）。粒子参数与换形态时序全部来自 GameConfig.evolutionFx / evolution。
    /// 缩放以脚底为锚点（Sprite pivot 在脚底，直接缩放层 transform）。
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class EvolutionFx : MonoBehaviour
    {
        ParticleSystem _ps;
        Texture2D _texture;
        Material _materialInstance;
        EvolutionConfig _timing;

        public bool IsPlaying { get; private set; }

        public void Configure(EvolutionFxConfig fx, EvolutionConfig timing)
        {
            _timing = timing;
            _ps = GetComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _ps.main;
            main.duration = Mathf.Max(0.1f, timing.totalSec);
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(fx.lifetimeMin, fx.lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(fx.startSpeedMin, fx.startSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(fx.startSizeMin, fx.startSizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = fx.ParsedColor;
            main.gravityModifier = fx.gravityModifier;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(64, fx.burstCount * 2);

            var emission = _ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(fx.burstCount, 1, short.MaxValue)) });

            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = fx.shapeRadius;
            shape.position = new Vector3(0f, fx.centerHeight, 0f);

            var sizeOverLifetime = _ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, fx.sizeOverLifetimeStart, 1f, fx.sizeOverLifetimeEnd));

            var colorOverLifetime = _ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(fx.alphaPeak, Mathf.Clamp01(fx.alphaRiseEnd)),
                    new GradientAlphaKey(fx.alphaPeak, Mathf.Clamp01(fx.alphaHoldEnd)),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = gradient;

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = fx.sortingOrder;

            if (_texture == null) _texture = SoftCircleTexture.Create(fx.textureSize);
            if (renderer.sharedMaterial != null)
            {
                if (_materialInstance == null) _materialInstance = new Material(renderer.sharedMaterial) { hideFlags = HideFlags.DontSave };
                _materialInstance.mainTexture = _texture;
                renderer.sharedMaterial = _materialInstance;
            }
            else
            {
                Debug.LogWarning("[Naiwa] 烟雾粒子没有材质（请重新执行 Naiwa/搭建主场景）");
            }
        }

        /// <summary>
        /// 按时序播放进化：旧形态（animator.Current）鼓起+淡出，新形态在 swapSec 时放到另一层淡入+回弹。
        /// onSwap 在新形态出现的那一刻调用（用于替换点击判定形状）。
        /// </summary>
        public IEnumerator Run(PetAnimatorLite animator, SpriteSequence newIdle, Action onSwap)
        {
            var t = _timing ?? new EvolutionConfig();
            IsPlaying = true;
            animator.CompleteFade();
            var oldLayer = animator.Current;

            _ps.Clear(true);
            _ps.Play(true);

            float time = 0f;
            bool swapped = false;
            while (time < t.totalSec)
            {
                time += Time.deltaTime;

                oldLayer.Scale = Mathf.Lerp(1f, t.oldPuffScale, Ease(Inverse(0f, t.oldPuffEndSec, time)));
                oldLayer.Alpha = 1f - Inverse(t.oldFadeStartSec, t.oldFadeEndSec, time);

                if (!swapped && time >= t.swapSec)
                {
                    animator.AssignOther(newIdle, true, 0f);
                    animator.Other.Scale = t.newStartScale;
                    onSwap?.Invoke();
                    swapped = true;
                }

                if (swapped)
                {
                    var newLayer = animator.Other;
                    newLayer.Alpha = Inverse(t.swapSec, t.newFadeEndSec, time);
                    newLayer.Scale = time < t.newFadeEndSec
                        ? Mathf.Lerp(t.newStartScale, t.newOvershootScale, Ease(Inverse(t.swapSec, t.newFadeEndSec, time)))
                        : Mathf.Lerp(t.newOvershootScale, 1f, Ease(Inverse(t.newFadeEndSec, t.newSettleEndSec, time)));
                }

                yield return null;
            }

            if (!swapped)
            {
                animator.AssignOther(newIdle, true, 1f);
                onSwap?.Invoke();
            }

            animator.CommitOther();
            IsPlaying = false;
        }

        static float Inverse(float a, float b, float v) => b <= a ? (v >= b ? 1f : 0f) : Mathf.Clamp01((v - a) / (b - a));

        static float Ease(float x) => x * x * (3f - 2f * x);

        void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
            if (_materialInstance != null) Destroy(_materialInstance);
        }
    }
}
