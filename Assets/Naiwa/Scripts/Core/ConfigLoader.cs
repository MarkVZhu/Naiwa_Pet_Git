using System;
using System.IO;
using UnityEngine;

namespace Naiwa.Core
{
    /// <summary>
    /// 两层配置：GameConfig 默认值 + StreamingAssets/config/game_config.json 逐字段覆盖。
    /// JSON 解析失败 → 整份回退到默认值并打 Warning（C10）。
    /// </summary>
    public static class ConfigLoader
    {
        public const string RelativePath = "config/game_config.json";

        public static string DefaultPath => Path.Combine(Application.streamingAssetsPath, RelativePath);

        public static GameConfig Load() => LoadFromFile(DefaultPath, msg => Debug.LogWarning("[Naiwa] " + msg));

        public static GameConfig LoadFromFile(string path, Action<string> warn)
        {
            if (!File.Exists(path))
                return Finalize(new GameConfig(), warn);

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                warn?.Invoke($"读取配置失败，使用默认值：{path} ({e.Message})");
                return Finalize(new GameConfig(), warn);
            }

            return Finalize(FromJson(json, warn), warn);
        }

        public static GameConfig FromJson(string json, Action<string> warn)
        {
            var config = new GameConfig();
            if (string.IsNullOrWhiteSpace(json))
                return config;

            try
            {
                JsonUtility.FromJsonOverwrite(json, config);
                return config;
            }
            catch (Exception e)
            {
                warn?.Invoke($"配置 JSON 解析失败，整份回退到默认值 ({e.Message})");
                return new GameConfig();
            }
        }

        static GameConfig Finalize(GameConfig config, Action<string> warn)
        {
            config.Sanitize(warn);
            return config;
        }
    }
}
