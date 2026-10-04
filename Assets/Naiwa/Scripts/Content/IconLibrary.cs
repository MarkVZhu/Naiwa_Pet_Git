using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Naiwa.Content
{
    /// <summary>序列帧文件名索引（编辑器生成到 Resources/Naiwa/clip_index.json）。</summary>
    [Serializable]
    public sealed class ClipIndex
    {
        [Serializable]
        public sealed class Entry
        {
            public string folder;
            public string[] frames;
        }

        public Entry[] folders;

        public string[] FramesOf(string folder)
        {
            if (folders == null || string.IsNullOrEmpty(folder)) return null;
            foreach (var e in folders)
                if (e != null && string.Equals(e.folder, folder, StringComparison.OrdinalIgnoreCase)) return e.frames;
            return null;
        }
    }

    /// <summary>
    /// 图鉴图标（v1.0 §8.3）：优先 content.json 的 icon 文件（运行时 LoadImage）；
    /// 否则只加载该表情的单帧（iconFrame，默认中间帧），按 Physics Shape 包围盒裁成正方形。
    /// 禁止为取图标加载整段序列帧。结果缓存。
    /// </summary>
    public sealed class IconLibrary
    {
        readonly string _contentDir;
        readonly string _clipsRoot;
        readonly ClipIndex _index;
        readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        readonly List<Vector2> _points = new List<Vector2>();

        public IconLibrary(string contentDir, string clipsResourceRoot, ClipIndex index)
        {
            _contentDir = contentDir;
            _clipsRoot = (clipsResourceRoot ?? string.Empty).Trim('/');
            _index = index;
        }

        public Sprite Get(EmoteDef def)
        {
            if (def == null) return null;
            if (_cache.TryGetValue(def.Id, out var cached)) return cached;
            var s = LoadFromFile(def) ?? LoadFromFrame(def);
            _cache[def.Id] = s;
            return s;
        }

        Sprite LoadFromFile(EmoteDef def)
        {
            if (string.IsNullOrEmpty(def.Icon) || string.IsNullOrEmpty(_contentDir)) return null;
            string path = Path.Combine(_contentDir, def.Icon);
            try
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[Naiwa] 图鉴图片 {def.Icon} 不存在，改用序列帧兜底（{def.Id}）");
                    return null;
                }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave };
                if (!tex.LoadImage(File.ReadAllBytes(path), true))
                {
                    Debug.LogWarning($"[Naiwa] 图鉴图片 {def.Icon} 解码失败，改用序列帧兜底（{def.Id}）");
                    return null;
                }
                tex.name = "NaiwaIcon_" + def.Id;
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Naiwa] 读取图鉴图片 {path} 失败：{e.Message}");
                return null;
            }
        }

        Sprite LoadFromFrame(EmoteDef def)
        {
            var frames = _index?.FramesOf(def.Folder);
            if (frames == null || frames.Length == 0)
            {
                Debug.LogWarning($"[Naiwa] 序列帧索引里没有 {def.Folder}，图鉴无法显示 {def.Id} 的图标（请执行 Naiwa/校验内容 重新生成索引）");
                return null;
            }

            int i = def.IconFrame >= 0 && def.IconFrame < frames.Length ? def.IconFrame : frames.Length / 2;
            string path = string.IsNullOrEmpty(_clipsRoot) ? $"{def.Folder}/{frames[i]}" : $"{_clipsRoot}/{def.Folder}/{frames[i]}";
            Sprite frame;
            try
            {
                frame = Resources.Load<Sprite>(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Naiwa] 加载图标帧 {path} 失败：{e.Message}");
                return null;
            }
            if (frame == null)
            {
                Debug.LogWarning($"[Naiwa] 找不到图标帧 Resources/{path}");
                return null;
            }
            return CropToContent(frame);
        }

        /// <summary>按 Physics Shape 包围盒裁成正方形，四周留 8% 边距（贴图不可读，不能按像素 alpha 算）。</summary>
        Sprite CropToContent(Sprite frame)
        {
            var tex = frame.texture;
            var full = frame.rect;
            int count = frame.GetPhysicsShapeCount();
            if (count == 0) return frame;

            float ppu = frame.pixelsPerUnit;
            Vector2 pivotPx = frame.pivot;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int s = 0; s < count; s++)
            {
                _points.Clear();
                frame.GetPhysicsShape(s, _points);
                foreach (var p in _points)
                {
                    float px = p.x * ppu + pivotPx.x, py = p.y * ppu + pivotPx.y;
                    minX = Mathf.Min(minX, px); maxX = Mathf.Max(maxX, px);
                    minY = Mathf.Min(minY, py); maxY = Mathf.Max(maxY, py);
                }
            }
            if (maxX <= minX || maxY <= minY) return frame;

            float side = Mathf.Max(maxX - minX, maxY - minY) / 0.84f;
            side = Mathf.Min(side, Mathf.Min(full.width, full.height));
            float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
            float x0 = Mathf.Clamp(cx - side / 2f, 0f, full.width - side);
            float y0 = Mathf.Clamp(cy - side / 2f, 0f, full.height - side);
            var rect = new Rect(full.x + x0, full.y + y0, side, side);
            return Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
