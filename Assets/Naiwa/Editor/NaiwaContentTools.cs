using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Naiwa.Content;
using UnityEditor;
using UnityEngine;

namespace Naiwa.EditorTools
{
    /// <summary>§V.3.1：Naiwa/导入 MVP 素材、Naiwa/校验 MVP 素材。</summary>
    public static class NaiwaContentTools
    {
        [MenuItem("Naiwa/导入 MVP 素材", priority = 10)]
        public static void ImportMvpContent()
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

            var manifest = ReadManifest(null);
            if (manifest == null) return;

            Directory.CreateDirectory(targetRoot);
            var folders = EnumerateFolders(manifest).ToList();
            int copiedFolders = 0, copiedFiles = 0;

            try
            {
                for (int i = 0; i < folders.Count; i++)
                {
                    string folder = folders[i];
                    EditorUtility.DisplayProgressBar("Naiwa 导入素材", folder, i / (float)folders.Count);

                    string srcName = NaiwaEditorConfig.ResolveChildDirectory(sourceDir, folder);
                    if (srcName == null)
                    {
                        Debug.LogWarning($"[Naiwa] 源目录中找不到文件夹 '{folder}'，已跳过");
                        continue;
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
            Debug.Log($"[Naiwa] 导入完成：{copiedFolders} 个文件夹，新复制 {copiedFiles} 个文件 → {NaiwaEditorConfig.ClipsAssetRoot}");
        }

        [MenuItem("Naiwa/重新应用素材导入设置", priority = 11)]
        public static void ReapplyImportSettings()
        {
            var manifest = ReadManifest(null);
            if (manifest == null) return;

            string root = NaiwaEditorConfig.ClipsAssetRoot;
            string rootFull = NaiwaEditorConfig.ToFullPath(root);
            int changed = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var folder in EnumerateFolders(manifest))
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

        [MenuItem("Naiwa/校验 MVP 素材", priority = 12)]
        public static bool ValidateMvpContent()
        {
            NaiwaEditorConfig.Invalidate();
            string root = NaiwaEditorConfig.ClipsAssetRoot;
            string rootFull = NaiwaEditorConfig.ToFullPath(root);
            var report = new StringBuilder();
            int errors = 0, warnings = 0;

            var manifest = ReadManifest(name => ResolveNonEmptyFolder(rootFull, name));
            if (manifest == null) return false;

            foreach (var w in manifest.Warnings) { report.AppendLine("  [警告] " + w); warnings++; }
            foreach (var e in manifest.Errors) { report.AppendLine("  [错误] " + e); errors++; }

            foreach (var form in manifest.Forms.Values.OrderBy(f => f.Form))
            {
                if (form.Emotes.Count < 1) { report.AppendLine($"  [错误] {form.Form} 没有表情（要求 ≥ 1）"); errors++; }

                var clips = new List<ClipDef>();
                if (form.Idle != null) clips.Add(form.Idle);
                clips.AddRange(form.Emotes);

                foreach (var clip in clips)
                {
                    string folderAsset = root + "/" + clip.Folder;
                    var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderAsset });
                    int frameCount = 0, badSize = 0, badImport = 0;
                    string firstImportIssue = null;
                    foreach (var guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                        frameCount++;
                        importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                        if (w != 600 || h != 600) badSize++;
                        string issue = NaiwaImportPostprocessor.Check(importer);
                        if (issue != null) { badImport++; firstImportIssue = firstImportIssue ?? issue; }
                    }

                    string kind = clip.Loop ? "idle" : "表情";
                    if (frameCount == 0) { report.AppendLine($"  [错误] {form.Form} {kind} {clip.Id}：没有帧"); errors++; continue; }
                    if (badSize > 0) { report.AppendLine($"  [错误] {form.Form} {kind} {clip.Id}：{badSize} 帧尺寸不是 600×600"); errors++; }
                    if (badImport > 0) { report.AppendLine($"  [错误] {form.Form} {kind} {clip.Id}：{badImport} 帧导入设置不符（{firstImportIssue}），请执行 Naiwa/重新应用素材导入设置"); errors++; }
                    report.AppendLine($"  [OK] {form.Form,-5} {kind,-2} {clip.Id,-16} {clip.Folder,-16} {frameCount,4} 帧 @ {clip.Fps}fps = {frameCount / clip.Fps:0.0}s");
                }
            }

            string header = $"[Naiwa] 校验 MVP 素材：{manifest.Forms.Count} 个阶段，{manifest.EmoteCount} 个表情，{errors} 个错误，{warnings} 个警告\n";
            if (errors > 0) Debug.LogError(header + report);
            else if (warnings > 0) Debug.LogWarning(header + report);
            else Debug.Log(header + report);
            return errors == 0;
        }

        static string ResolveNonEmptyFolder(string rootFull, string name)
        {
            string real = NaiwaEditorConfig.ResolveChildDirectory(rootFull, name);
            if (real == null) return null;
            return Directory.GetFiles(Path.Combine(rootFull, real), "*.png").Length > 0 ? real : null;
        }

        static IEnumerable<string> EnumerateFolders(ContentManifestResult manifest)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var form in manifest.Forms.Values)
            {
                if (form.Idle != null && seen.Add(form.Idle.Folder)) yield return form.Idle.Folder;
                foreach (var e in form.Emotes)
                    if (seen.Add(e.Folder)) yield return e.Folder;
            }
        }

        static ContentManifestResult ReadManifest(Func<string, string> resolve)
        {
            string path = NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.ManifestAssetPath);
            if (!File.Exists(path))
            {
                Debug.LogError($"[Naiwa] 找不到映射表 {NaiwaEditorConfig.ManifestAssetPath}");
                return null;
            }
            return ContentManifest.Parse(File.ReadAllText(path), resolve);
        }
    }
}
