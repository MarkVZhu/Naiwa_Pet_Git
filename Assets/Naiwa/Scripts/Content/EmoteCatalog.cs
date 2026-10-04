using System.Collections.Generic;
using Naiwa.Growth;

namespace Naiwa.Content
{
    /// <summary>所有已注册表情的只读目录（来自 content.json）。</summary>
    public sealed class EmoteCatalog
    {
        readonly ContentConfigResult _content;
        readonly Dictionary<string, EmoteDef> _byId = new Dictionary<string, EmoteDef>();
        readonly List<EmoteDef> _all = new List<EmoteDef>();

        public EmoteCatalog(ContentConfigResult content)
        {
            _content = content;
            foreach (var e in content.AllEmotes)
            {
                if (_byId.ContainsKey(e.Id)) continue;
                _byId.Add(e.Id, e);
                _all.Add(e);
            }
        }

        public ContentConfigResult Content => _content;
        public IReadOnlyList<EmoteDef> All => _all;
        public int Count => _all.Count;

        public EmoteDef Get(string id) => id != null && _byId.TryGetValue(id, out var e) ? e : null;

        public bool Contains(string id) => id != null && _byId.ContainsKey(id);

        public IReadOnlyList<EmoteDef> ForForm(FormId form)
        {
            var c = _content.Get(form);
            return c != null ? (IReadOnlyList<EmoteDef>)c.Emotes : new List<EmoteDef>();
        }

        public string FormDisplayName(FormId form) => _content.FormDisplayName(form);
    }
}
