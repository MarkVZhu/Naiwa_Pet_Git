using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Naiwa.Save
{
    /// <summary>存档 v2（v1.0 §9.1）。沿用 v0.1 的类名（C11）。</summary>
    [Serializable]
    public sealed class SaveDataLite
    {
        public const int CurrentVersion = 2;
        /// <summary>windowX/windowY 未设置时的哨兵值（首次启动放右下角）。</summary>
        public const int NoPosition = int.MinValue;

        public int version = CurrentVersion;
        public long growth;
        /// <summary>当前显示形态 displayForm（FormId 的整数值）。</summary>
        public int form;
        /// <summary>已解锁最高形态（FormId 的整数值）。</summary>
        public int highestForm;
        public long clicks;
        public long clicksLifetimeEarned;
        public long clicksLifetimeSpent;
        public List<string> unlockedEmotes = new List<string>();
        /// <summary>0 = 已就绪。</summary>
        public long lotteryNextAvailableUnixMs;
        public int lotteryDrawCount;
        public int windowX = NoPosition;
        public int windowY = NoPosition;
        /// <summary>
        /// 保存 windowY 时窗口顶部的额外留白（window.headroomPx）。留白改变时 windowY 按差值平移，宠物在屏幕上的位置不变。
        /// 旧存档没有该字段 = 0。
        /// </summary>
        public int windowHeadroomPx;
        public bool countingPaused;
        public bool hudShowClicks = true;
        public bool hudShowGrowth = true;
        /// <summary>全局缩放（右键菜单「调整大小」）。旧存档没有该字段 = 1。windowX/windowY 是缩放后窗口的位置。</summary>
        public float displayScale = 1f;

        /// <summary>v1（v0.1）的计数框开关，只用于迁移。</summary>
        public bool showCounter = true;

        public bool HasWindowPosition => windowX != NoPosition && windowY != NoPosition;

        /// <summary>按当前顶部留白修正 windowY（留白随全局缩放 scale 放大）。返回是否有改动。</summary>
        public bool ApplyHeadroom(int headroomPx, float scale = 1f)
        {
            if (windowHeadroomPx == headroomPx) return false;
            if (HasWindowPosition) windowY -= Mathf.RoundToInt((headroomPx - windowHeadroomPx) * scale);
            windowHeadroomPx = headroomPx;
            return true;
        }
    }

    public enum SaveLoadSource { Main, Backup, NewDefault }

    /// <summary>新存档与迁移需要的外部参数。</summary>
    public sealed class SaveDefaults
    {
        public bool hudShowClicks = true;
        public bool hudShowGrowth = true;
        /// <summary>v1.0 窗口左侧多出的宽度：v1 存档的 windowX 减去它，宠物在屏幕上的位置不变（§7.3）。</summary>
        public int windowShiftX;
    }

    /// <summary>
    /// persistentDataPath/save.json。原子写入：先写 .tmp，再 File.Replace 替换并保留 .bak（§9）。
    /// 读档失败 → 读 .bak → 仍失败则新建，并把损坏的主文件改名为 save.corrupt.&lt;时间&gt;.json。
    /// v1 → v2 迁移前先把旧存档复制为 save.v1.bak.json。
    /// </summary>
    public sealed class SaveServiceLite
    {
        readonly string _dir;
        readonly Action<string> _warn;
        readonly SaveDefaults _defaults;

        public string MainPath => Path.Combine(_dir, "save.json");
        public string BackupPath => Path.Combine(_dir, "save.json.bak");
        public string TempPath => Path.Combine(_dir, "save.json.tmp");
        public string V1BackupPath => Path.Combine(_dir, "save.v1.bak.json");

        public SaveLoadSource LastLoadSource { get; private set; }
        /// <summary>本次读取是否做了 v1 → v2 迁移。</summary>
        public bool LastLoadMigrated { get; private set; }

        /// <summary>每次成功写入后递增（测试用：确认抽奖返回前已落盘）。</summary>
        public int SaveCount { get; private set; }

        public SaveServiceLite(string directory, Action<string> warn = null, SaveDefaults defaults = null)
        {
            _dir = directory;
            _warn = warn ?? (msg => Debug.LogWarning("[Naiwa] " + msg));
            _defaults = defaults ?? new SaveDefaults();
        }

        public SaveDataLite Load()
        {
            LastLoadMigrated = false;
            bool mainExists = File.Exists(MainPath);
            if (TryRead(MainPath, out var data, out int ver))
            {
                LastLoadSource = SaveLoadSource.Main;
                if (ver < 2) MigrateFromV1(data, MainPath);
                return data;
            }

            if (TryRead(BackupPath, out data, out ver))
            {
                if (mainExists)
                {
                    _warn("存档损坏，已从 .bak 恢复");
                    QuarantineCorrupt();
                }
                LastLoadSource = SaveLoadSource.Backup;
                if (ver < 2) MigrateFromV1(data, BackupPath);
                return data;
            }

            if (mainExists)
            {
                _warn("存档与备份均损坏，已新建存档");
                QuarantineCorrupt();
            }
            LastLoadSource = SaveLoadSource.NewDefault;
            return NewDefault();
        }

        public SaveDataLite NewDefault() => new SaveDataLite
        {
            hudShowClicks = _defaults.hudShowClicks,
            hudShowGrowth = _defaults.hudShowGrowth,
        };

        public bool Save(SaveDataLite data)
        {
            if (data == null) return false;
            try
            {
                Directory.CreateDirectory(_dir);
                data.version = SaveDataLite.CurrentVersion;
                if (data.unlockedEmotes == null) data.unlockedEmotes = new List<string>();
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
                SaveCount++;
                return true;
            }
            catch (Exception e)
            {
                _warn($"写入存档失败：{e.Message}");
                return false;
            }
        }

        /// <summary>
        /// v1 → v2（§9.1）：clicks = growth；highestForm = form（之后按新阈值正常进化，不会降级）；
        /// 抽奖冷却为就绪；显示开关沿用 v0.1 的计数框开关（缺省为开）；windowX 左移侧边区宽度。
        /// </summary>
        void MigrateFromV1(SaveDataLite data, string sourcePath)
        {
            try
            {
                if (!File.Exists(V1BackupPath)) File.Copy(sourcePath, V1BackupPath, false);
            }
            catch (Exception e)
            {
                _warn($"无法备份 v1 存档：{e.Message}");
            }

            data.clicks = data.growth;
            data.clicksLifetimeEarned = data.growth;
            data.clicksLifetimeSpent = 0;
            data.highestForm = data.form;
            data.unlockedEmotes = new List<string>();
            data.lotteryNextAvailableUnixMs = 0;
            data.lotteryDrawCount = 0;
            data.hudShowClicks = data.showCounter;
            data.hudShowGrowth = true;
            if (data.HasWindowPosition) data.windowX -= _defaults.windowShiftX;
            data.version = SaveDataLite.CurrentVersion;
            LastLoadMigrated = true;
        }

        static bool TryRead(string path, out SaveDataLite data, out int version)
        {
            data = null;
            version = 0;
            try
            {
                if (!File.Exists(path)) return false;
                string text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text) || !text.Contains("\"version\"")) return false;
                // 覆盖到新实例上：旧存档缺失的字段保留默认值
                data = new SaveDataLite();
                JsonUtility.FromJsonOverwrite(text, data);
                version = data.version;
                if (version < 1) { data = null; return false; }
                if (data.growth < 0) data.growth = 0;
                if (data.clicks < 0) data.clicks = 0;
                if (data.unlockedEmotes == null) data.unlockedEmotes = new List<string>();
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
