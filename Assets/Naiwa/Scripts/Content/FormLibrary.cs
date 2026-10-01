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
        public readonly List<SpriteSequence> Emotes = new List<SpriteSequence>();

        /// <summary>点击判定形状来源：idle 第 0 帧。</summary>
        public Sprite HitShapeSprite => Idle != null && Idle.FrameCount > 0 ? Idle.Frames[0] : null;
    }

    /// <summary>
    /// 按阶段加载/卸载 Sprite 序列（§V.3.2）。同一时间最多驻留 2 个阶段（进化过程中）。
    /// </summary>
    public sealed class FormLibrary
    {
        readonly ContentManifestResult _manifest;
        readonly string _clipsRoot;
        readonly Dictionary<FormId, LoadedForm> _loaded = new Dictionary<FormId, LoadedForm>();

        public FormLibrary(ContentManifestResult manifest, string clipsResourceRoot)
        {
            _manifest = manifest;
            _clipsRoot = (clipsResourceRoot ?? string.Empty).Trim('/');
        }

        public bool IsLoaded(FormId form) => _loaded.ContainsKey(form);

        public LoadedForm Get(FormId form) => _loaded.TryGetValue(form, out var f) ? f : null;

        public IEnumerable<FormId> LoadedForms => _loaded.Keys;

        public LoadedForm Load(FormId form)
        {
            if (_loaded.TryGetValue(form, out var existing)) return existing;

            var loaded = new LoadedForm { Form = form };
            var content = _manifest.Get(form);

            loaded.Idle = LoadIdleWithFallback(form);
            if (content != null)
            {
                foreach (var clip in content.Emotes)
                {
                    var seq = LoadClip(clip);
                    if (seq != null) loaded.Emotes.Add(seq);
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
            var loaded = new LoadedForm { Form = form };
            var content = _manifest.Get(form);

            loaded.Idle = LoadIdleWithFallback(form);
            yield return null;

            if (content != null)
            {
                foreach (var clip in content.Emotes)
                {
                    if (Time.realtimeSinceStartup - start > timeoutSec)
                    {
                        Debug.LogError($"[Naiwa] 加载阶段 {form} 超时（>{timeoutSec:0.0}s），本次进化取消");
                        ReleaseTemporary(loaded);
                        onDone?.Invoke(false);
                        yield break;
                    }

                    var seq = LoadClip(clip);
                    if (seq != null) loaded.Emotes.Add(seq);
                    yield return null;
                }
            }

            if (loaded.Idle == null || Time.realtimeSinceStartup - start > timeoutSec)
            {
                Debug.LogError(loaded.Idle == null
                    ? $"[Naiwa] 阶段 {form} 没有任何可用 idle，本次进化取消"
                    : $"[Naiwa] 加载阶段 {form} 超时（>{timeoutSec:0.0}s），本次进化取消");
                ReleaseTemporary(loaded);
                onDone?.Invoke(false);
                yield break;
            }

            _loaded[form] = loaded;
            onDone?.Invoke(true);
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

        static void ReleaseTemporary(LoadedForm loaded)
        {
            loaded.Emotes.Clear();
            loaded.Idle = null;
            Resources.UnloadUnusedAssets();
        }

        /// <summary>某阶段没有 idle：打 Error，用上一阶段（再不行用下一阶段）的 idle 兜底。</summary>
        SpriteSequence LoadIdleWithFallback(FormId form)
        {
            var own = _manifest.Get(form)?.Idle;
            if (own != null)
            {
                var seq = LoadClip(own);
                if (seq != null) return seq;
            }

            Debug.LogError($"[Naiwa] 阶段 {form} 没有可用 idle，尝试用其他阶段的 idle 兜底");

            for (int f = (int)form - 1; f >= (int)FormIdExtensions.First; f--)
            {
                var seq = TryLoadIdleOf((FormId)f);
                if (seq != null) return seq;
            }
            for (int f = (int)form + 1; f <= (int)FormIdExtensions.Last; f++)
            {
                var seq = TryLoadIdleOf((FormId)f);
                if (seq != null) return seq;
            }
            return null;
        }

        SpriteSequence TryLoadIdleOf(FormId form)
        {
            var def = _manifest.Get(form)?.Idle;
            return def != null ? LoadClip(def) : null;
        }

        SpriteSequence LoadClip(ClipDef clip)
        {
            string path = string.IsNullOrEmpty(_clipsRoot) ? clip.Folder : _clipsRoot + "/" + clip.Folder;
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
            return new SpriteSequence(clip.Id, frames, clip.Fps, clip.Loop);
        }
    }
}
