using System.IO;
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

            var options = new BuildPlayerOptions
            {
                scenes = new[] { NaiwaEditorConfig.MainScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[Naiwa] 打包成功：{output}（{summary.totalSize / (1024f * 1024f):0.0} MB，用时 {summary.totalTime.TotalSeconds:0}s）");
            else
                Debug.LogError($"[Naiwa] 打包失败：{summary.result}，错误 {summary.totalErrors} 个");
        }
    }
}
