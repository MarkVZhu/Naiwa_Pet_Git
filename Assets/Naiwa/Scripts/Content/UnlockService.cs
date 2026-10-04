using System;
using System.Collections.Generic;

namespace Naiwa.Content
{
    public enum UnlockSource { Default, Lottery, Debug }

    public interface IUnlockService
    {
        bool IsUnlocked(string id);
        IReadOnlyCollection<string> Unlocked { get; }
        /// <summary>新解锁返回 true 并发出 EmoteUnlocked。</summary>
        bool Unlock(string id, UnlockSource src);
        void ResetToDefaults();
        void UnlockAll();
    }

    /// <summary>
    /// 表情解锁记录（v1.0 §6.1）。解锁永久有效；配置里已删除的 id 也保留（§3.4）。
    /// 启动时 ApplyDefaults 静默解锁所有 Default 表情。
    /// </summary>
    public sealed class UnlockService : IUnlockService
    {
        readonly EmoteCatalog _catalog;
        readonly HashSet<string> _unlocked;
        readonly List<string> _ordered;

        /// <summary>(id, 来源)。Default 静默解锁时不触发。</summary>
        public event Action<string, UnlockSource> EmoteUnlocked;

        /// <summary>解锁集合发生任何变化（含静默、重置）。</summary>
        public event Action Changed;

        public UnlockService(EmoteCatalog catalog, IEnumerable<string> savedUnlocked)
        {
            _catalog = catalog;
            _unlocked = new HashSet<string>(StringComparer.Ordinal);
            _ordered = new List<string>();
            if (savedUnlocked != null)
            {
                foreach (var id in savedUnlocked)
                    if (!string.IsNullOrEmpty(id) && _unlocked.Add(id)) _ordered.Add(id);
            }
        }

        public IReadOnlyCollection<string> Unlocked => _ordered;

        /// <summary>存档用：保持解锁顺序（含配置里已删除的 id）。</summary>
        public List<string> ToSaveList() => new List<string>(_ordered);

        public bool IsUnlocked(string id) => id != null && _unlocked.Contains(id);

        /// <summary>静默解锁所有 Default 表情，返回新解锁数量。</summary>
        public int ApplyDefaults()
        {
            int n = 0;
            foreach (var e in _catalog.All)
            {
                if (e.Unlock != UnlockMode.Default) continue;
                if (Add(e.Id)) n++;
            }
            if (n > 0) Changed?.Invoke();
            return n;
        }

        public bool Unlock(string id, UnlockSource src)
        {
            if (string.IsNullOrEmpty(id) || !Add(id)) return false;
            Changed?.Invoke();
            if (src != UnlockSource.Default) EmoteUnlocked?.Invoke(id, src);
            return true;
        }

        public void ResetToDefaults()
        {
            // 只清掉配置里存在的非 Default 表情；配置已删除的 id 原样保留
            _ordered.RemoveAll(id =>
            {
                var def = _catalog.Get(id);
                if (def == null || def.Unlock == UnlockMode.Default) return false;
                _unlocked.Remove(id);
                return true;
            });
            ApplyDefaults();
            Changed?.Invoke();
        }

        public void UnlockAll()
        {
            bool any = false;
            foreach (var e in _catalog.All) any |= Add(e.Id);
            if (any) Changed?.Invoke();
        }

        public int CountUnlocked(IEnumerable<EmoteDef> emotes)
        {
            int n = 0;
            foreach (var e in emotes) if (IsUnlocked(e.Id)) n++;
            return n;
        }

        bool Add(string id)
        {
            if (!_unlocked.Add(id)) return false;
            _ordered.Add(id);
            return true;
        }
    }
}
