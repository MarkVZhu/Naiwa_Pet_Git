using System;
using System.IO;
using UnityEngine;

namespace Naiwa.Save
{
    [Serializable]
    public sealed class SaveDataLite
    {
        public const int CurrentVersion = 1;
        /// <summary>windowX/windowY 未设置时的哨兵值（首次启动放右下角）。</summary>
        public const int NoPosition = int.MinValue;

        public int version = CurrentVersion;
        public int growth;
        /// <summary>FormId 的整数值。</summary>
        public int form;
        public int windowX = NoPosition;
        public int windowY = NoPosition;
        public bool countingPaused;
        /// <summary>是否显示脚下的计数框（默认开启；旧存档没有此字段时视为开启）。</summary>
        public bool showCounter = true;

        public bool HasWindowPosition => windowX != NoPosition && windowY != NoPosition;

        public SaveDataLite Clone() => (SaveDataLite)MemberwiseClone();
    }

    public enum SaveLoadSource { Main, Backup, NewDefault }

    /// <summary>
    /// persistentDataPath/save.json。原子写入：先写 .tmp，再 File.Replace 替换并保留 .bak（§9）。
    /// 读档失败 → 读 .bak → 仍失败则新建，并把损坏的主文件改名为 save.corrupt.&lt;时间&gt;.json。
    /// </summary>
    public sealed class SaveServiceLite
    {
        readonly string _dir;
        readonly Action<string> _warn;

        public string MainPath => Path.Combine(_dir, "save.json");
        public string BackupPath => Path.Combine(_dir, "save.json.bak");
        public string TempPath => Path.Combine(_dir, "save.json.tmp");

        public SaveLoadSource LastLoadSource { get; private set; }

        public SaveServiceLite(string directory, Action<string> warn = null)
        {
            _dir = directory;
            _warn = warn ?? (msg => Debug.LogWarning("[Naiwa] " + msg));
        }

        public SaveDataLite Load()
        {
            bool mainExists = File.Exists(MainPath);
            if (TryRead(MainPath, out var data))
            {
                LastLoadSource = SaveLoadSource.Main;
                return data;
            }

            if (TryRead(BackupPath, out data))
            {
                if (mainExists)
                {
                    _warn($"存档损坏，已从 .bak 恢复");
                    QuarantineCorrupt();
                }
                LastLoadSource = SaveLoadSource.Backup;
                return data;
            }

            if (mainExists)
            {
                _warn("存档与备份均损坏，已新建存档");
                QuarantineCorrupt();
            }
            LastLoadSource = SaveLoadSource.NewDefault;
            return new SaveDataLite();
        }

        public bool Save(SaveDataLite data)
        {
            if (data == null) return false;
            try
            {
                Directory.CreateDirectory(_dir);
                data.version = SaveDataLite.CurrentVersion;
                File.WriteAllText(TempPath, JsonUtility.ToJson(data, true));

                if (File.Exists(MainPath))
                {
                    try
                    {
                        File.Replace(TempPath, MainPath, BackupPath);
                    }
                    catch (Exception)
                    {
                        // 部分文件系统不支持 Replace：退化为复制（非原子，但保留 .bak）
                        File.Copy(MainPath, BackupPath, true);
                        File.Copy(TempPath, MainPath, true);
                        File.Delete(TempPath);
                    }
                }
                else
                {
                    File.Move(TempPath, MainPath);
                }
                return true;
            }
            catch (Exception e)
            {
                _warn($"写入存档失败：{e.Message}");
                return false;
            }
        }

        static bool TryRead(string path, out SaveDataLite data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                string text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text) || !text.Contains("\"version\"")) return false;
                // 覆盖到新实例上：旧存档缺失的字段保留默认值
                data = new SaveDataLite();
                JsonUtility.FromJsonOverwrite(text, data);
                if (data.version < 1) { data = null; return false; }
                if (data.growth < 0) data.growth = 0;
                return true;
            }
            catch (Exception)
            {
                data = null;
                return false;
            }
        }

        void QuarantineCorrupt()
        {
            try
            {
                if (!File.Exists(MainPath)) return;
                string target = Path.Combine(_dir, $"save.corrupt.{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
                File.Move(MainPath, target);
            }
            catch (Exception e)
            {
                _warn($"无法隔离损坏的存档：{e.Message}");
            }
        }
    }
}
