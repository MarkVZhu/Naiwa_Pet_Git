using System;
using System.Collections;
using System.Collections.Generic;
using Naiwa.Growth;
using Naiwa.Pet;
using UnityEngine;

namespace Naiwa.Content
{
    /// <summary>已加载到内存的一个阶段。</summary>
    public sealed class LoadedForm
    {
        public FormId Form;
        public SpriteSequence Idle;
        /// <summary>已加载的表情（只加载已解锁的，未解锁的按需加载）。</summary>
        public readonly Dictionary<string, SpriteSequence> Emotes = new Dictionary<string, SpriteSequence>();

        /// <summary>点击判定形状来源：idle 第 0 帧。</summary>
        /// <summary>该阶段的显示缩放（idle 的 DisplayScale）。</summary>
        public float DisplayScale => Idle != null ? Idle.DisplayScale : 1f;

        public Sprite HitShapeSprite => Idle != null && Idle.FrameCount > 0 ? Idle.Frames[0] : null;
    }

    /// <summary>
    /// 按阶段加载/卸载 Sprite 序列（§V.3.2）。同一时间最多驻留 2 个阶段（进化/切换过程中）。
    /// v1.0：只加载已解锁的表情，抽中或解锁后按需加载（显存，§8.3）。
    /// </summary>
    public sealed class FormLibrary
    {
        readonly ContentConfigResult _content;
        readonly string _clipsRoot;
        readonly Func<string, bool> _shouldPreload;
        readonly Func<FormId, float> _scaleOf;
        readonly Dictionary<FormId, LoadedForm> _loaded = new Dictionary<FormId, LoadedForm>();

        /// <param name="shouldPreload">某个表情 id 是否在加载阶段时一并加载（通常 = 已解锁）。</param>
        public FormLibrary(ContentConfigResult content, string clipsResourceRoot, Func<string, bool> shouldPreload, Func<FormId, float> scaleOf = null)
        {
            _content = content;
            _clipsRoot = (clipsResourceRoot ?? string.Empty).Trim('/');
            _shouldPreload = shouldPreload ?? (_ => true);
            _scaleOf = scaleOf ?? (_ => 1f);
        }

        public bool IsLoaded(FormId form) => _loaded.ContainsKey(form);

        public LoadedForm Get(FormId form) => _loaded.TryGetValue(form, out var f) ? f : null;

        public IEnumerable<FormId> LoadedForms => _loaded.Keys;

        public string ClipResourcePath(string folder) =>
            string.IsNullOrEmpty(_clipsRoot) ? folder : _clipsRoot + "/" + folder;

        public LoadedForm Load(FormId form)
        {
            if (_loaded.TryGetValue(form, out var existing)) return existing;

            var loaded = new LoadedForm { Form = form, Idle = LoadIdleWithFallback(form) };
            var content = _content.Get(form);
            if (content != null)
            {
                foreach (var e in content.Emotes)
                {
                    if (!_shouldPreload(e.Id)) continue;
                    var seq = LoadClip(e.Clip, form);
                    if (seq != null) loaded.Emotes[e.Id] = seq;
                }
            }

            _loaded[form] = loaded;
            return loaded;
        }

        /// <summary>协程分帧加载（每个文件夹一帧），超时视为失败。</summary>
        public IEnumerator LoadAsync(FormId form, float timeoutSec, Action<bool> onDone)
        {
            if (_loaded.ContainsKey(form))
            {
                onDone?.Invoke(true);
                yield break;
            }

            float start = Time.realtimeSinceStartup;
            var loaded = new LoadedForm { Form = form, Idle = LoadIdleWithFallback(form) };
            yield return null;

            var content = _content.Get(form);
            if (content != null)
            {
                foreach (var e in content.Emotes)
                {
                    if (!_shouldPreload(e.Id)) continue;
                    if (Time.realtimeSinceStartup - start > timeoutSec) break;
                    var seq = LoadClip(e.Clip, form);
                    if (seq != null) loaded.Emotes[e.Id] = seq;
                    yield return null;
                }
            }

            if (loaded.Idle == null || Time.realtimeSinceStartup - start > timeoutSec)
            {
                Debug.LogError(loaded.Idle == null
                    ? $"[Naiwa] 阶段 {form} 没有任何可用 idle，本次切换取消"
                    : $"[Naiwa] 加载阶段 {form} 超时（>{timeoutSec:0.0}s），本次切换取消");
                loaded.Emotes.Clear();
                loaded.Idle = null;
                Resources.UnloadUnusedAssets();
                onDone?.Invoke(false);
                yield break;
            }

            _loaded[form] = loaded;
            onDone?.Invoke(true);
        }

        /// <summary>取某个表情的序列；阶段已加载但表情还没加载时同步加载它（抽中、解锁、调试）。</summary>
        public SpriteSequence GetOrLoadEmote(EmoteDef def)
        {
            if (def == null) return null;
            var form = Get(def.Form);
            if (form == null) return null;
            if (form.Emotes.TryGetValue(def.Id, out var seq)) return seq;
            seq = LoadClip(def.Clip, def.Form);
            if (seq != null) form.Emotes[def.Id] = seq;
            return seq;
        }

        public void Unload(FormId form)
        {
            if (!_loaded.Remove(form)) return;
            Resources.UnloadUnusedAssets();
        }

        public void UnloadAllExcept(FormId keep)
        {
            var toRemove = new List<FormId>();
            foreach (var f in _loaded.Keys)
                if (f != keep) toRemove.Add(f);
            if (toRemove.Count == 0) return;
            foreach (var f in toRemove) _loaded.Remove(f);
            Resources.UnloadUnusedAssets();
        }

        /// <summary>某阶段没有 idle：打 Error，用上一阶段（再不行用下一阶段）的 idle 兜底。</summary>
        SpriteSequence LoadIdleWithFallback(FormId form)
        {
            var own = _content.Get(form)?.Idle;
            if (own != null)
            {
                var seq = LoadClip(own, form);
                if (seq != null) return seq;
            }

            Debug.LogError($"[Naiwa] 阶段 {form} 没有可用 idle，尝试用其他阶段的 idle 兜底");

            for (int f = (int)form - 1; f >= (int)FormIdExtensions.First; f--)
            {
                var seq = TryLoadIdleOf((FormId)f, form);
                if (seq != null) return seq;
            }
            for (int f = (int)form + 1; f <= (int)FormIdExtensions.Last; f++)
            {
                var seq = TryLoadIdleOf((FormId)f, form);
                if (seq != null) return seq;
            }
            return null;
        }

        SpriteSequence TryLoadIdleOf(FormId form, FormId displayAs)
        {
            var def = _content.Get(form)?.Idle;
            return def != null ? LoadClip(def, displayAs) : null;
        }

        SpriteSequence LoadClip(ClipDef clip, FormId displayAs)
        {
            string path = ClipResourcePath(clip.Folder);
            Sprite[] frames;
            try
            {
                frames = Resources.LoadAll<Sprite>(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Naiwa] 加载 '{path}' 失败，已跳过 {clip.Id}：{e.Message}");
                return null;
            }

            if (frames == null || frames.Length == 0)
            {
                Debug.LogWarning($"[Naiwa] Resources/{path} 下没有 Sprite，已跳过 {clip.Id}");
                return null;
            }

            // LoadAll 不保证顺序，按文件名排序。
            Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            return new SpriteSequence(clip.Id, frames, clip.Fps, clip.Loop) { DisplayScale = _scaleOf(displayAs) };
        }
    }
}
