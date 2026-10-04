using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Naiwa.Content;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Naiwa.EditorTools
{
    /// <summary>v1.0 §9.2：Naiwa/导入素材、Naiwa/校验内容、Naiwa/导出图鉴图标（均读取 StreamingAssets/content/content.json）。</summary>
    public static class NaiwaContentTools
    {
        const int IconSize = 256;
        const float IconMargin = 0.08f;

        [MenuItem("Naiwa/导入素材", priority = 10)]
        public static void ImportContent()
        {
            NaiwaEditorConfig.Invalidate();
            var config = NaiwaEditorConfig.Config;
            string sourceDir = config.content.sourceDir;
            string targetRoot = NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.ClipsAssetRoot);

            if (!Directory.Exists(sourceDir))
            {
                Debug.LogWarning($"[Naiwa] 素材源目录不存在：{sourceDir}（可在 game_config.json 的 content.sourceDir 修改）");
                return;
            }

            var content = ReadContent(null);
            if (content == null) return;

            Directory.CreateDirectory(targetRoot);
            var folders = EnumerateFolders(content).ToList();
            int copiedFolders = 0, copiedFiles = 0, skipped = 0;

            try
            {
                for (int i = 0; i < folders.Count; i++)
                {
                    string folder = folders[i];
                    EditorUtility.DisplayProgressBar("Naiwa 导入素材", folder, i / (float)folders.Count);

                    string srcName = NaiwaEditorConfig.ResolveChildDirectory(sourceDir, folder);
                    if (srcName == null)
                    {
                        skipped++;
                        continue; // 只在工程里存在的文件夹不需要从源目录复制
                    }

                    string dstName = NaiwaEditorConfig.ResolveChildDirectory(targetRoot, folder) ?? srcName;
                    string dst = Path.Combine(targetRoot, dstName);
                    Directory.CreateDirectory(dst);
                    foreach (var file in Directory.GetFiles(Path.Combine(sourceDir, srcName), "*.png"))
                    {
                        string to = Path.Combine(dst, Path.GetFileName(file));
                        if (File.Exists(to) && new FileInfo(to).Length == new FileInfo(file).Length
                            && File.GetLastWriteTimeUtc(to) >= File.GetLastWriteTimeUtc(file))
                            continue;
                        File.Copy(file, to, true);
                        copiedFiles++;
                    }
                    copiedFolders++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            ReapplyImportSettings();
            WriteClipIndex();
            Debug.Log($"[Naiwa] 导入完成：{copiedFolders} 个文件夹从源目录同步，新复制 {copiedFiles} 个文件；{skipped} 个文件夹源目录里没有（沿用工程内的）");
        }

        [MenuItem("Naiwa/重新应用素材导入设置", priority = 11)]
        public static void ReapplyImportSettings()
        {
            var content = ReadContent(null);
            if (content == null) return;

            string root = NaiwaEditorConfig.ClipsAssetRoot;
            string rootFull = NaiwaEditorConfig.ToFullPath(root);
            int changed = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var folder in EnumerateFolders(content))
                {
                    string real = NaiwaEditorConfig.ResolveChildDirectory(rootFull, folder);
                    if (real == null) continue;
                    foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root + "/" + real }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                        if (NaiwaImportPostprocessor.Check(importer) == null) continue;
                        NaiwaImportPostprocessor.Apply(importer);
                        importer.SaveAndReimport();
                        changed++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log(changed == 0
                ? "[Naiwa] 所有素材导入设置均已符合规范"
                : $"[Naiwa] 已重新导入 {changed} 张贴图以应用导入设置");
        }

        [MenuItem("Naiwa/校验内容", priority = 12)]
        public static bool ValidateContent()
        {
            NaiwaEditorConfig.Invalidate();
            string root = NaiwaEditorConfig.ClipsAssetRoot;
            string rootFull = NaiwaEditorConfig.ToFullPath(root);
            string contentDir = NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.ContentDirAssetPath);
            var report = new StringBuilder();
            int errors = 0, warnings = 0;

            var content = ReadContent(name => ResolveNonEmptyFolder(rootFull, name));
            if (content == null) return false;

            var aligned = AlignNonStandardClips(content, rootFull, report, ref warnings);

            foreach (var w in content.Warnings) { report.AppendLine("  [警告] " + w); warnings++; }
            foreach (var e in content.Errors) { report.AppendLine("  [错误] " + e); errors++; }

            foreach (var form in content.Forms.Values.OrderBy(f => f.Form))
            {
                var clips = new List<(ClipDef clip, EmoteDef def)>();
                if (form.Idle != null) clips.Add((form.Idle, null));
                foreach (var e in form.Emotes) clips.Add((e.Clip, e));

                foreach (var (clip, def) in clips)
                {
                    string folderAsset = root + "/" + clip.Folder;
                    var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderAsset });
                    int frameCount = 0, badSize = 0, badImport = 0;
                    int expected = aligned.TryGetValue(clip.Folder, out int canvas) ? canvas : 600;
                    string firstImportIssue = null;
                    foreach (var guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                        frameCount++;
                        importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                        if (w != expected || h != expected) badSize++;
                        string issue = NaiwaImportPostprocessor.Check(importer);
                        if (issue != null) { badImport++; firstImportIssue = firstImportIssue ?? issue; }
                    }

                    string kind = def == null ? "idle" : def.Unlock == UnlockMode.Default ? "默认" : "抽奖";
                    string id = def?.Id ?? clip.Id;
                    if (frameCount == 0) { report.AppendLine($"  [错误] {form.Form} {kind} {id}：没有帧"); errors++; continue; }
                    if (badSize > 0) { report.AppendLine($"  [错误] {form.Form} {kind} {id}：{badSize} 帧尺寸不是 {expected}×{expected}（同一文件夹必须同尺寸）"); errors++; }
                    if (badImport > 0) { report.AppendLine($"  [错误] {form.Form} {kind} {id}：{badImport} 帧导入设置不符（{firstImportIssue}），请执行 Naiwa/重新应用素材导入设置"); errors++; }

                    string iconNote = "";
                    if (def != null)
                    {
                        if (!string.IsNullOrEmpty(def.Icon))
                        {
                            if (!File.Exists(Path.Combine(contentDir, def.Icon))) { report.AppendLine($"  [警告] {id} 的 icon 文件 {def.Icon} 不存在，运行时用序列帧兜底"); warnings++; iconNote = " 图标缺失"; }
                            else iconNote = " 图标✓";
                        }
                        else iconNote = " 图标=帧兜底";
                    }
                    string name = def != null ? def.DisplayName : "";
                    report.AppendLine($"  [OK] {form.Form,-5} {kind,-2} {id,-16} {name,-8} {clip.Folder,-16} {frameCount,4} 帧 @ {clip.Fps}fps = {frameCount / clip.Fps:0.0}s{iconNote}");
                }
            }

            var all = content.AllEmotes.ToList();
            int lottery = all.Count(e => e.InLotteryPool);
            WriteClipIndex();

            string header = $"[Naiwa] 校验内容：{content.Forms.Count} 个阶段，{all.Count} 个表情（默认 {all.Count(e => e.Unlock == UnlockMode.Default)}，抽奖 {lottery}），{errors} 个错误，{warnings} 个警告\n";
            if (errors > 0) Debug.LogError(header + report);
            else if (warnings > 0) Debug.LogWarning(header + report);
            else Debug.Log(header + report);
            return errors == 0;
        }

        /// <summary>
        /// 画布不是 600×600 的表情：按所属阶段 idle 第 0 帧的 alpha 包围盒自动对齐（写 clip_alignment.json 并重新导入），
        /// 让常态 ↔ 表情切换不跳脸。返回 folder → 画布尺寸。
        /// </summary>
        static Dictionary<string, int> AlignNonStandardClips(ContentConfigResult content, string rootFull, StringBuilder report, ref int warnings)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool changed = false;
            foreach (var form in content.Forms.Values)
            {
                if (form.Idle == null) continue;
                string idleFirst = FirstFrame(rootFull, form.Idle.Folder);
                var idleBox = idleFirst != null ? ClipAlignment.Measure(idleFirst) : null;

                foreach (var e in form.Emotes)
                {
                    string first = FirstFrame(rootFull, e.Folder);
                    if (first == null) continue;
                    var box = ClipAlignment.Measure(first);
                    if (!box.HasValue) continue;

                    if (box.Value.canvas == 600)
                    {
                        // 标准画布默认不需要对齐；手工校正过的（manual）保留，避免「Naiwa/对齐序列帧锚点」的结果被还原
                        var manual = ClipAlignment.Get(e.Folder);
                        if (manual != null && manual.manual)
                        {
                            report.AppendLine($"  [对齐·手工] {e.Id}：PPU {manual.ppu:0.##}，Pivot ({manual.pivotX:0.###}, {manual.pivotY:0.###})");
                            continue;
                        }
                        if (manual != null) { ClipAlignment.Remove(e.Folder); changed = true; }
                        continue;
                    }
                    if (!idleBox.HasValue || idleBox.Value.canvas != 600)
                    {
                        report.AppendLine($"  [警告] {e.Id} 画布 {box.Value.canvas}px，但 {form.Form} 的 idle 不是标准画布，无法自动对齐");
                        warnings++;
                        continue;
                    }

                    var entry = ClipAlignment.Compute(e.Folder, idleBox.Value, box.Value);
                    var old = ClipAlignment.Get(e.Folder);
                    if (old == null || Mathf.Abs(old.ppu - entry.ppu) > 0.01f || Mathf.Abs(old.pivotX - entry.pivotX) > 1e-4f || Mathf.Abs(old.pivotY - entry.pivotY) > 1e-4f)
                    {
                        ClipAlignment.Set(entry);
                        changed = true;
                    }
                    result[e.Folder] = box.Value.canvas;
                    report.AppendLine($"  [对齐] {e.Id}：画布 {box.Value.canvas}px，按 {form.Form} idle 第 0 帧对齐 → PPU {entry.ppu:0.##}，Pivot ({entry.pivotX:0.###}, {entry.pivotY:0.###})");
                }
            }

            if (changed)
            {
                ClipAlignment.Save();
                ReapplyImportSettings();
            }
            return result;
        }

        /// <summary>
        /// 手工对齐某个序列帧文件夹的锚点：按「该表情第 0 帧」对齐「所属阶段 idle 第 0 帧」的 alpha 包围盒，
        /// 结果写进 clip_alignment.json（manual = true，自动流程不会再动它）并重新导入。
        /// 用于「画布是标准 600px，但构图整体偏了」的素材，例如 small_icecream 整体偏左。
        /// </summary>
        [MenuItem("Naiwa/对齐序列帧锚点（选中的文件夹）", priority = 14)]
        public static void AlignSelectedClipAnchors()
        {
            NaiwaEditorConfig.Invalidate();
            var content = ReadContent(null);
            if (content == null) return;

            string rootFull = NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.ClipsAssetRoot);
            var folders = SelectedClipFolders(rootFull);
            if (folders.Count == 0)
            {
                Debug.LogWarning($"[Naiwa] 请先在 Project 窗口选中 {NaiwaEditorConfig.ClipsAssetRoot} 下的序列帧文件夹（或里面的贴图）");
                return;
            }

            var report = new StringBuilder();
            bool changed = false;
            foreach (string folder in folders)
            {
                string first = FirstFrame(rootFull, folder);
                var box = first != null ? ClipAlignment.Measure(first) : null;
                if (!box.HasValue)
                {
                    report.AppendLine($"  [跳过] {folder}：没有可测量的 PNG");
                    continue;
                }

                var form = FindOwningForm(content, folder, out bool isIdle);
                if (form == null)
                {
                    report.AppendLine($"  [跳过] {folder}：content.json 里找不到它所属的阶段");
                    continue;
                }

                var reference = StandardReference(box.Value);
                string referenceName = "标准画布（居中、脚底贴地）";
                string referencePng = null;
                if (!isIdle)
                {
                    if (form.Idle == null) { report.AppendLine($"  [跳过] {folder}：{form.Form} 没有 idle 可参照"); continue; }
                    string idleFirst = FirstFrame(rootFull, form.Idle.Folder);
                    var idleBox = idleFirst != null ? ClipAlignment.Measure(idleFirst) : null;
                    if (!idleBox.HasValue) { report.AppendLine($"  [跳过] {folder}：{form.Form} 的 idle 无法测量"); continue; }
                    reference = idleBox.Value;
                    referenceName = $"{form.Form} idle 第 0 帧";
                    referencePng = idleFirst;
                }

                // 画布尺寸相同 → 按轮廓重叠对齐，只挪锚点不缩放；
                // 画布不同（如 egg_sleep 的 768px）才退回按包围盒缩放对齐
                string detail = string.Empty;
                ClipAlignment.Entry entry;
                if (referencePng != null && box.Value.canvas == reference.canvas)
                {
                    entry = ClipAlignment.ComputeSilhouette(folder, referencePng, reference, first, box.Value, out int shift, out float iou);
                    detail = $"，轮廓位移 {shift}px（重叠度 {iou:0.00}）";
                    if (iou < 0.6f)
                        report.AppendLine($"  [注意] {folder}：第 0 帧与 idle 姿势差异较大（重叠度 {iou:0.00}），位移 {shift}px 仅供参考");
                }
                else if (box.Value.canvas == reference.canvas)
                {
                    entry = ClipAlignment.ComputeOffset(folder, reference, box.Value);
                }
                else
                {
                    entry = ClipAlignment.Compute(folder, reference, box.Value);
                }
                entry.manual = true;
                var old = ClipAlignment.Get(folder);
                bool same = old != null && Mathf.Abs(old.ppu - entry.ppu) <= 0.01f
                    && Mathf.Abs(old.pivotX - entry.pivotX) <= 1e-4f && Mathf.Abs(old.pivotY - entry.pivotY) <= 1e-4f;
                ClipAlignment.Set(entry);
                changed = true;
                report.AppendLine($"  [对齐] {folder}：参照{referenceName} → PPU {entry.ppu:0.##}，Pivot ({entry.pivotX:0.###}, {entry.pivotY:0.###}){detail}" +
                                  (same ? "（与现有值一致）" : string.Empty));
            }

            if (changed)
            {
                ClipAlignment.Save();
                ReapplyImportSettings();
            }
            Debug.Log($"[Naiwa] 对齐序列帧锚点（{folders.Count} 个文件夹）：\n{report}");
        }

        /// <summary>Project 窗口当前选中的、位于序列帧根目录下的文件夹名（选贴图时取它所在的文件夹）。</summary>
        static List<string> SelectedClipFolders(string rootFull)
        {
            var folders = new List<string>();
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;
                string full = NaiwaEditorConfig.ToFullPath(path);
                if (File.Exists(full)) full = Path.GetDirectoryName(full);
                if (full == null || !Directory.Exists(full)) continue;
                if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) continue;
                string name = Path.GetFileName(full);
                if (!folders.Contains(name)) folders.Add(name);
            }
            folders.Sort(StringComparer.Ordinal);
            return folders;
        }

        static FormContent FindOwningForm(ContentConfigResult content, string folder, out bool isIdle)
        {
            foreach (var form in content.Forms.Values)
            {
                if (form.Idle != null && SameFolder(form.Idle.Folder, folder)) { isIdle = true; return form; }
                foreach (var e in form.Emotes)
                    if (SameFolder(e.Folder, folder)) { isIdle = false; return form; }
            }
            isIdle = false;
            return null;
        }

        static bool SameFolder(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>idle 自己对齐时用：把身体水平居中、脚底落在标准地面线（画布下沿 20px）上的假想包围盒。</summary>
        static ClipAlignment.BBox StandardReference(ClipAlignment.BBox box)
        {
            float canvas = Naiwa.Core.PetGeometry.CanvasPx;
            float centerX = canvas * 0.5f;
            float ground = Naiwa.Core.PetGeometry.Pivot.y * canvas;
            return new ClipAlignment.BBox
            {
                canvas = (int)canvas,
                minX = centerX - box.Width * 0.5f,
                maxX = centerX + box.Width * 0.5f,
                minY = ground,
                maxY = ground + box.Height,
            };
        }

        static string FirstFrame(string rootFull, string folder)
        {
            string dir = Path.Combine(rootFull, folder);
            if (!Directory.Exists(dir)) return null;
            return Directory.GetFiles(dir, "*.png").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).FirstOrDefault();
        }

        [MenuItem("Naiwa/导出图鉴图标", priority = 13)]
        public static void ExportCollectionIcons()
        {
            NaiwaEditorConfig.Invalidate();
            string rootFull = NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.ClipsAssetRoot);
            string contentDir = NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.ContentDirAssetPath);
            string jsonPath = NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.ContentJsonAssetPath);
            var content = ReadContent(name => ResolveNonEmptyFolder(rootFull, name));
            if (content == null) return;

            string iconDir = Path.Combine(contentDir, "icons");
            Directory.CreateDirectory(iconDir);
            string json = File.ReadAllText(jsonPath);
            int exported = 0, filled = 0;
            var notFilled = new List<string>();

            try
            {
                var emotes = content.AllEmotes.Where(e => string.IsNullOrEmpty(e.Icon)).ToList();
                for (int i = 0; i < emotes.Count; i++)
                {
                    var def = emotes[i];
                    EditorUtility.DisplayProgressBar("Naiwa 导出图鉴图标", def.Id, i / (float)Math.Max(1, emotes.Count));
                    var frames = Directory.GetFiles(Path.Combine(rootFull, def.Folder), "*.png").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).ToArray();
                    if (frames.Length == 0) continue;
                    int index = def.IconFrame >= 0 && def.IconFrame < frames.Length ? def.IconFrame : frames.Length / 2;

                    var png = MakeIcon(frames[index]);
                    if (png == null) continue;
                    string rel = $"icons/{def.Id}.png";
                    File.WriteAllBytes(Path.Combine(contentDir, rel), png);
                    exported++;

                    // 回填 content.json：给该表情所在那一行的对象补上 "icon"
                    var pattern = new Regex("(\\{[^{}\\n]*\"id\"\\s*:\\s*\"" + Regex.Escape(def.Id) + "\"[^{}\\n]*?)\\s*\\}");
                    var m = pattern.Match(json);
                    if (m.Success && !m.Value.Contains("\"icon\""))
                    {
                        json = json.Substring(0, m.Index) + m.Groups[1].Value + $", \"icon\": \"{rel}\" }}" + json.Substring(m.Index + m.Length);
                        filled++;
                    }
                    else notFilled.Add(def.Id);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (filled > 0) File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
            AssetDatabase.Refresh();
            string msg = $"[Naiwa] 导出图鉴图标：{exported} 个 → {NaiwaEditorConfig.ContentDirAssetPath}/icons/，回填 content.json {filled} 处";
            if (notFilled.Count > 0) Debug.LogWarning(msg + $"；以下表情未能自动回填 icon 字段，请手动添加：{string.Join(", ", notFilled)}");
            else Debug.Log(msg);
        }

        /// <summary>读取源 PNG，按 alpha 包围盒裁成正方形（四周留 8%），缩放到 256×256。</summary>
        static byte[] MakeIcon(string sourcePng)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!src.LoadImage(File.ReadAllBytes(sourcePng))) return null;
                var px = src.GetPixels32();
                int w = src.width, h = src.height;
                int minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a <= 8) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
                if (maxX < minX) return null;

                float side = Mathf.Max(maxX - minX + 1, maxY - minY + 1) / (1f - IconMargin * 2f);
                float cx = (minX + maxX + 1) * 0.5f, cy = (minY + maxY + 1) * 0.5f;
                float x0 = cx - side / 2f, y0 = cy - side / 2f;

                var dst = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
                var outPx = new Color[IconSize * IconSize];
                const int ss = 3; // 超采样，避免缩小时锯齿
                for (int y = 0; y < IconSize; y++)
                for (int x = 0; x < IconSize; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = x0 + (x + (sx + 0.5f) / ss) / IconSize * side;
                        float v = y0 + (y + (sy + 0.5f) / ss) / IconSize * side;
                        if (u < 0 || v < 0 || u >= w || v >= h) continue;
                        var c = src.GetPixelBilinear(u / w, v / h);
                        r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                    }
                    a /= ss * ss;
                    outPx[y * IconSize + x] = a > 0f ? new Color(r / (a * ss * ss), g / (a * ss * ss), b / (a * ss * ss), a) : new Color(0, 0, 0, 0);
                }
                dst.SetPixels(outPx);
                dst.Apply();
                var bytes = dst.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(dst);
                return bytes;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(src);
            }
        }

        /// <summary>生成序列帧文件名索引（运行时图鉴图标兜底只加载单帧用）。</summary>
        public static void WriteClipIndex()
        {
            string rootFull = NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.ClipsAssetRoot);
            if (!Directory.Exists(rootFull)) return;
            var index = new ClipIndex
            {
                folders = Directory.GetDirectories(rootFull)
                    .Select(d => new ClipIndex.Entry
                    {
                        folder = Path.GetFileName(d),
                        frames = Directory.GetFiles(d, "*.png").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                    })
                    .Where(e => e.frames.Length > 0)
                    .OrderBy(e => e.folder, StringComparer.Ordinal)
                    .ToArray(),
            };

            string path = NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.ClipIndexAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string json = JsonUtility.ToJson(index);
            if (File.Exists(path) && File.ReadAllText(path) == json) return;
            File.WriteAllText(path, json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(NaiwaEditorConfig.ClipIndexAssetPath);
        }

        static string ResolveNonEmptyFolder(string rootFull, string name)
        {
            string real = NaiwaEditorConfig.ResolveChildDirectory(rootFull, name);
            if (real == null) return null;
            return Directory.GetFiles(Path.Combine(rootFull, real), "*.png").Length > 0 ? real : null;
        }

        static IEnumerable<string> EnumerateFolders(ContentConfigResult content)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var form in content.Forms.Values)
            {
                if (form.Idle != null && seen.Add(form.Idle.Folder)) yield return form.Idle.Folder;
                foreach (var e in form.Emotes)
                    if (seen.Add(e.Folder)) yield return e.Folder;
            }
        }

        static ContentConfigResult ReadContent(Func<string, string> resolve)
        {
            string path = NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.ContentJsonAssetPath);
            if (!File.Exists(path))
            {
                string legacy = NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.LegacyManifestAssetPath);
                if (!File.Exists(legacy))
                {
                    Debug.LogError($"[Naiwa] 找不到内容配置 {NaiwaEditorConfig.ContentJsonAssetPath}");
                    return null;
                }
                Debug.LogWarning($"[Naiwa] 找不到 {NaiwaEditorConfig.ContentJsonAssetPath}，回退读取 v0.1 映射表");
                path = legacy;
            }
            return ContentConfig.Parse(File.ReadAllText(path), resolve);
        }
    }

    /// <summary>打包前刷新序列帧索引，保证图鉴图标兜底可用。</summary>
    public sealed class NaiwaClipIndexBuildStep : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => NaiwaContentTools.WriteClipIndex();
    }
}
