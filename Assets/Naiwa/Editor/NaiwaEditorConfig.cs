using System;
using System.IO;
using Naiwa.Core;
using UnityEngine;

namespace Naiwa.EditorTools
{
    /// <summary>编辑器侧共享的配置与路径。</summary>
    internal static class NaiwaEditorConfig
    {
        static GameConfig s_cached;

        public static GameConfig Config => s_cached ?? (s_cached = ConfigLoader.LoadFromFile(ConfigLoader.DefaultPath, w => Debug.LogWarning("[Naiwa] " + w)));

        public static void Invalidate() => s_cached = null;

        /// <summary>序列帧根目录（工程相对路径），例如 Assets/Resources/AnimationImages。</summary>
        public static string ClipsAssetRoot => "Assets/Resources/" + Config.content.clipsResourcePath.Trim('/');

        public static string ManifestAssetPath => "Assets/Naiwa/Resources/" + Config.content.manifestResourcePath.Trim('/') + ".json";

        public const string MainScenePath = "Assets/Naiwa/Scenes/Main.unity";
        public const string SmokeMaterialPath = "Assets/Naiwa/Materials/SmokePremultiplied.mat";
        public const string TextMaterialPath = "Assets/Naiwa/Materials/TextPremultiplied.mat";
        public const string BuildOutputPath = "Builds/NaiwaPet/NaiwaPet.exe";

        public static string ToFullPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        /// <summary>在 parentDir 下按忽略大小写查找子文件夹，返回真实名称；找不到返回 null。</summary>
        public static string ResolveChildDirectory(string parentDir, string name)
        {
            if (!Directory.Exists(parentDir) || string.IsNullOrEmpty(name)) return null;
            foreach (var dir in Directory.GetDirectories(parentDir))
            {
                string n = Path.GetFileName(dir);
                if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return n;
            }
            return null;
        }

        public static bool IsUnderClipsRoot(string assetPath)
        {
            string root = ClipsAssetRoot + "/";
            return assetPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith("Assets/Naiwa/Resources/Naiwa/Clips/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
