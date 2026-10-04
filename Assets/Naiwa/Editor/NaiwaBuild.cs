using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Naiwa.EditorTools
{
    /// <summary>Naiwa/打包 Windows：输出到 Builds/NaiwaPet/NaiwaPet.exe。</summary>
    public static class NaiwaBuild
    {
        [MenuItem("Naiwa/打包 Windows", priority = 50)]
        public static void BuildWindows()
        {
            if (!File.Exists(NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.MainScenePath)))
            {
                Debug.LogError("[Naiwa] 主场景不存在，请先执行 Naiwa/搭建主场景");
                return;
            }

            NaiwaMenu.ApplyPlayerSettings();

            string output = NaiwaEditorConfig.ToFullPath(NaiwaEditorConfig.BuildOutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            bool includeDebug = NaiwaBuildSettings.instance.includeDebugInBuild;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { NaiwaEditorConfig.MainScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
                extraScriptingDefines = includeDebug ? new string[0] : new[] { "NAIWA_NO_DEBUG" },
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                SyncBuiltConfig(output, includeDebug);
                Debug.Log($"[Naiwa] 打包成功：{output}（{summary.totalSize / (1024f * 1024f):0.0} MB，用时 {summary.totalTime.TotalSeconds:0}s，调试菜单：{(includeDebug ? "包含" : "不包含")}）");
            }
            else
                Debug.LogError($"[Naiwa] 打包失败：{summary.result}，错误 {summary.totalErrors} 个");
        }

        /// <summary>
        /// 包内 StreamingAssets/config/game_config.json 先用工程里的原文件覆盖（Unity 增量打包不会重拷未改动的文件，
        /// 上一次「不包含调试」写入的 false 会残留）；不包含调试时再把 debug.enabled 写成 false（与 NAIWA_NO_DEBUG 一致）。
        /// </summary>
        static void SyncBuiltConfig(string exePath, bool includeDebug)
        {
            string dataDir = Path.Combine(Path.GetDirectoryName(exePath), Path.GetFileNameWithoutExtension(exePath) + "_Data");
            string json = Path.Combine(dataDir, "StreamingAssets", "config", "game_config.json");
            string source = Path.Combine(Application.streamingAssetsPath, "config", "game_config.json");
            if (!File.Exists(source)) return;
            string text = File.ReadAllText(source);
            if (!includeDebug)
                text = Regex.Replace(text, "(\"debug\"\\s*:\\s*\\{[^}]*\"enabled\"\\s*:\\s*)true", "${1}false");
            Directory.CreateDirectory(Path.GetDirectoryName(json));
            File.WriteAllText(json, text);
        }
    }
}
