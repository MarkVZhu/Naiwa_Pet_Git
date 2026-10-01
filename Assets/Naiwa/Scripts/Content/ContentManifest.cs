using System;
using System.Collections.Generic;
using Naiwa.Growth;
using UnityEngine;

namespace Naiwa.Content
{
    /// <summary>一段序列帧的定义（来自 mvp_content.json）。</summary>
    public sealed class ClipDef
    {
        public string Id;
        public string Folder;
        public float Fps;
        public bool Loop;

        public override string ToString() => $"{Id}({Folder})";
    }

    public sealed class FormContent
    {
        public FormId Form;
        /// <summary>为 null 表示该阶段缺 idle（致命配置错误，由 FormLibrary 兜底）。</summary>
        public ClipDef Idle;
        public readonly List<ClipDef> Emotes = new List<ClipDef>();
    }

    public sealed class ContentManifestResult
    {
        public readonly Dictionary<FormId, FormContent> Forms = new Dictionary<FormId, FormContent>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool HasErrors => Errors.Count > 0;

        public int EmoteCount
        {
            get
            {
                int n = 0;
                foreach (var f in Forms.Values) n += f.Emotes.Count;
                return n;
            }
        }

        public FormContent Get(FormId form) => Forms.TryGetValue(form, out var c) ? c : null;
    }

    /// <summary>
    /// 解析 §V.3.1 的映射表。任何内容错误只记 Warning/Error，不抛异常（C10）。
    /// </summary>
    public static class ContentManifest
    {
        public const int SupportedSchemaVersion = 1;
        public const float DefaultIdleFps = 12f;
        public const float DefaultEmoteFps = 24f;

        /// <param name="json">映射表文本。</param>
        /// <param name="resolveFolder">
        /// 可选：把映射表里的文件夹名解析为真实存在的文件夹名（忽略大小写），不存在或为空返回 null。
        /// 传 null 表示跳过存在性检查（运行时由 FormLibrary 在加载时检查）。
        /// </param>
        public static ContentManifestResult Parse(string json, Func<string, string> resolveFolder = null)
        {
            var result = new ContentManifestResult();

            if (string.IsNullOrWhiteSpace(json))
            {
                result.Errors.Add("映射表为空");
                return result;
            }

            ManifestJson root;
            try
            {
                root = JsonUtility.FromJson<ManifestJson>(json);
            }
            catch (Exception e)
            {
                result.Errors.Add($"映射表 JSON 解析失败：{e.Message}");
                return result;
            }

            if (root == null || root.forms == null)
            {
                result.Errors.Add("映射表缺少 forms 字段");
                return result;
            }

            if (root.schemaVersion != SupportedSchemaVersion)
                result.Warnings.Add($"schemaVersion={root.schemaVersion}，当前只支持 {SupportedSchemaVersion}，按 v1 尝试解析");

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var formJson in root.forms)
            {
                if (formJson == null) continue;

                if (!Enum.TryParse(formJson.form, true, out FormId formId) || !Enum.IsDefined(typeof(FormId), formId))
                {
                    result.Warnings.Add($"未知阶段 '{formJson.form}'，已跳过");
                    continue;
                }

                if (result.Forms.ContainsKey(formId))
                {
                    result.Warnings.Add($"阶段 {formId} 重复定义，已忽略后一份");
                    continue;
                }

                var content = new FormContent { Form = formId };
                content.Idle = ParseClip(formJson.idle, $"{formId}.idle", DefaultIdleFps, true, resolveFolder, result, isIdle: true);
                if (content.Idle == null)
                    result.Errors.Add($"阶段 {formId} 没有可用的 idle（致命配置错误，运行时将用上一阶段的 idle 兜底）");

                if (formJson.emotes != null)
                {
                    for (int i = 0; i < formJson.emotes.Length; i++)
                    {
                        var clip = ParseClip(formJson.emotes[i], $"{formId}.emotes[{i}]", DefaultEmoteFps, false, resolveFolder, result, isIdle: false);
                        if (clip == null) continue;

                        if (!seenIds.Add(clip.Id))
                        {
                            result.Warnings.Add($"表情 id '{clip.Id}' 重复，已跳过");
                            continue;
                        }

                        content.Emotes.Add(clip);
                    }
                }

                if (content.Emotes.Count == 0)
                    result.Warnings.Add($"阶段 {formId} 没有可用表情，点击将没有反应");

                result.Forms.Add(formId, content);
            }

            foreach (FormId f in Enum.GetValues(typeof(FormId)))
            {
                if (!result.Forms.ContainsKey(f))
                    result.Errors.Add($"映射表缺少阶段 {f}");
            }

            return result;
        }

        static ClipDef ParseClip(ClipJson json, string where, float defaultFps, bool loop,
            Func<string, string> resolveFolder, ContentManifestResult result, bool isIdle)
        {
            if (json == null || string.IsNullOrWhiteSpace(json.folder))
            {
                if (!isIdle) result.Warnings.Add($"{where} 缺少 folder，已跳过");
                return null;
            }

            string folder = json.folder.Trim();
            if (resolveFolder != null)
            {
                string resolved = resolveFolder(folder);
                if (string.IsNullOrEmpty(resolved))
                {
                    result.Warnings.Add($"{where} 的文件夹 '{folder}' 不存在或没有帧，已跳过");
                    return null;
                }
                folder = resolved;
            }

            float fps = json.fps;
            if (fps <= 0f)
            {
                result.Warnings.Add($"{where} 的 fps={json.fps} 非法，使用默认 {defaultFps}");
                fps = defaultFps;
            }

            string id = string.IsNullOrWhiteSpace(json.id) ? folder : json.id.Trim();
            return new ClipDef { Id = id, Folder = folder, Fps = fps, Loop = loop };
        }

#pragma warning disable 0649
        [Serializable]
        sealed class ManifestJson
        {
            public int schemaVersion;
            public FormJson[] forms;
        }

        [Serializable]
        sealed class FormJson
        {
            public string form;
            public ClipJson idle;
            public ClipJson[] emotes;
        }

        [Serializable]
        sealed class ClipJson
        {
            public string id;
            public string folder;
            public float fps;
        }
#pragma warning restore 0649
    }
}
