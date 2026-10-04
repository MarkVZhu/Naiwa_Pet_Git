using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Naiwa.Growth;
using UnityEngine;

namespace Naiwa.Content
{
    public enum UnlockMode { Default, Lottery }

    /// <summary>一段序列帧的定义。</summary>
    public sealed class ClipDef
    {
        public string Id;
        public string Folder;
        public float Fps;
        public bool Loop;

        public override string ToString() => $"{Id}({Folder})";
    }

    /// <summary>一个表情的完整定义（v1.0 §3.3）。</summary>
    public sealed class EmoteDef
    {
        public string Id;
        public string DisplayName;
        public FormId Form;
        public ClipDef Clip;
        public UnlockMode Unlock;
        public int LotteryWeight;
        /// <summary>相对 StreamingAssets/content/ 的图鉴图片路径，可为空。</summary>
        public string Icon;
        /// <summary>-1 = 中间帧。</summary>
        public int IconFrame;
        public int Order;

        public string Folder => Clip.Folder;
        public bool InLotteryPool => Unlock == UnlockMode.Lottery && LotteryWeight > 0;

        public override string ToString() => Id;
    }

    public sealed class FormContent
    {
        public FormId Form;
        public string DisplayName;
        /// <summary>为 null 表示该阶段缺 idle（致命配置错误，由 FormLibrary 兜底）。</summary>
        public ClipDef Idle;
        /// <summary>按 order 排序。</summary>
        public readonly List<EmoteDef> Emotes = new List<EmoteDef>();
    }

    public sealed class ContentConfigResult
    {
        public readonly Dictionary<FormId, FormContent> Forms = new Dictionary<FormId, FormContent>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public int SchemaVersion;

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

        public IEnumerable<EmoteDef> AllEmotes
        {
            get
            {
                foreach (FormId f in Enum.GetValues(typeof(FormId)))
                {
                    var c = Get(f);
                    if (c == null) continue;
                    foreach (var e in c.Emotes) yield return e;
                }
            }
        }

        public string FormDisplayName(FormId form)
        {
            var c = Get(form);
            return c != null && !string.IsNullOrEmpty(c.DisplayName) ? c.DisplayName : form.DisplayName();
        }
    }

    /// <summary>
    /// 解析内容配置。schema 2 = v1.0 content.json；schema 1 = v0.1 mvp_content.json（所有表情视为 Default）。
    /// 任何内容错误只记 Warning/Error，不抛异常（C10）。
    /// </summary>
    public static class ContentConfig
    {
        public const int CurrentSchema = 2;
        public const float DefaultIdleFps = 12f;
        public const float DefaultEmoteFps = 24f;

        static readonly Regex s_idPattern = new Regex("^[a-z0-9_]+$");

        /// <param name="resolveFolder">
        /// 可选：把文件夹名解析为真实存在的文件夹名（忽略大小写），不存在或为空返回 null。传 null 跳过检查。
        /// </param>
        public static ContentConfigResult Parse(string json, Func<string, string> resolveFolder = null)
        {
            var result = new ContentConfigResult();

            if (string.IsNullOrWhiteSpace(json))
            {
                result.Errors.Add("内容配置为空");
                return result;
            }

            RootJson root;
            try
            {
                root = JsonUtility.FromJson<RootJson>(json);
            }
            catch (Exception e)
            {
                result.Errors.Add($"内容配置 JSON 解析失败：{e.Message}");
                return result;
            }

            if (root == null || root.forms == null)
            {
                result.Errors.Add("内容配置缺少 forms 字段");
                return result;
            }

            result.SchemaVersion = root.schemaVersion;
            bool legacy = root.schemaVersion <= 1;
            if (legacy)
                result.Warnings.Add("内容配置是 schema 1（v0.1），所有表情按 Default 处理");
            else if (root.schemaVersion > CurrentSchema)
                result.Warnings.Add($"schemaVersion={root.schemaVersion} 高于当前支持的 {CurrentSchema}，按 {CurrentSchema} 尝试解析");

            var seenIds = new HashSet<string>(StringComparer.Ordinal);

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

                var content = new FormContent
                {
                    Form = formId,
                    DisplayName = string.IsNullOrWhiteSpace(formJson.displayName) ? formId.DisplayName() : formJson.displayName.Trim(),
                };

                content.Idle = ParseClip(formJson.idle, $"{formId}.idle", null, DefaultIdleFps, true, resolveFolder, result, isIdle: true);
                if (content.Idle == null)
                    result.Errors.Add($"阶段 {formId} 没有可用的 idle（致命配置错误，运行时将用其他阶段的 idle 兜底）");

                if (formJson.emotes != null)
                {
                    for (int i = 0; i < formJson.emotes.Length; i++)
                    {
                        var ej = formJson.emotes[i];
                        if (ej == null) continue;
                        string where = $"{formId}.emotes[{i}]";
                        var clip = ParseClip(ej, where, ej.id, DefaultEmoteFps, false, resolveFolder, result, isIdle: false);
                        if (clip == null) continue;

                        if (!s_idPattern.IsMatch(clip.Id))
                        {
                            result.Warnings.Add($"{where} 的 id '{clip.Id}' 只能包含 [a-z0-9_]，已跳过");
                            continue;
                        }

                        if (!seenIds.Add(clip.Id))
                        {
                            result.Warnings.Add($"表情 id '{clip.Id}' 重复，已跳过后者");
                            continue;
                        }

                        var def = new EmoteDef
                        {
                            Id = clip.Id,
                            DisplayName = string.IsNullOrWhiteSpace(ej.displayName) ? clip.Id : ej.displayName.Trim(),
                            Form = formId,
                            Clip = clip,
                            Unlock = legacy ? UnlockMode.Default : ParseUnlock(ej.unlock, clip.Id, result),
                            LotteryWeight = ej.lotteryWeight == int.MinValue ? 1 : ej.lotteryWeight,
                            Icon = string.IsNullOrWhiteSpace(ej.icon) ? null : ej.icon.Trim().Replace('\\', '/'),
                            IconFrame = ej.iconFrame,
                            Order = ej.order == int.MinValue ? i : ej.order,
                        };

                        if (def.Unlock == UnlockMode.Lottery && def.LotteryWeight <= 0)
                            result.Warnings.Add($"表情 {def.Id} 的 lotteryWeight={def.LotteryWeight} ≤ 0，不会进入抽奖池");

                        content.Emotes.Add(def);
                    }
                }

                content.Emotes.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));

                if (content.Emotes.Count == 0)
                    result.Warnings.Add($"阶段 {formId} 没有可用表情");
                else if (!content.Emotes.Exists(e => e.Unlock == UnlockMode.Default))
                    result.Warnings.Add($"阶段 {formId} 没有 Default 表情，抽到之前点奶蛙不会播放表情");

                result.Forms.Add(formId, content);
            }

            foreach (FormId f in Enum.GetValues(typeof(FormId)))
            {
                if (!result.Forms.ContainsKey(f))
                    result.Errors.Add($"内容配置缺少阶段 {f}");
            }

            return result;
        }

        static UnlockMode ParseUnlock(string value, string id, ContentConfigResult result)
        {
            if (string.IsNullOrWhiteSpace(value)) return UnlockMode.Default;
            if (Enum.TryParse(value.Trim(), true, out UnlockMode mode) && Enum.IsDefined(typeof(UnlockMode), mode))
                return mode;
            result.Warnings.Add($"表情 {id} 的 unlock='{value}' 非法，按 Default 处理");
            return UnlockMode.Default;
        }

        static ClipDef ParseClip(ClipJson json, string where, string id, float defaultFps, bool loop,
            Func<string, string> resolveFolder, ContentConfigResult result, bool isIdle)
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

            string clipId = string.IsNullOrWhiteSpace(id) ? folder : id.Trim();
            return new ClipDef { Id = clipId, Folder = folder, Fps = fps, Loop = loop };
        }

#pragma warning disable 0649
        [Serializable]
        sealed class RootJson
        {
            public int schemaVersion;
            public FormJson[] forms;
        }

        [Serializable]
        sealed class FormJson
        {
            public string form;
            public string displayName;
            public ClipJson idle;
            public EmoteJson[] emotes;
        }

        [Serializable]
        class ClipJson
        {
            public string folder;
            public float fps;
        }

        [Serializable]
        sealed class EmoteJson : ClipJson
        {
            public string id;
            public string displayName;
            public string unlock;
            public int lotteryWeight = int.MinValue;
            public string icon;
            public int iconFrame = -1;
            public int order = int.MinValue;
        }
#pragma warning restore 0649
    }
}
