using System;
using System.Collections.Generic;
using System.IO;
using Naiwa.Core;
using UnityEditor;
using UnityEngine;

namespace Naiwa.EditorTools
{
    /// <summary>
    /// 非标准画布（不是 600×600 或构图不同）的序列帧对齐表：按「该表情第 0 帧」对齐「所属阶段 idle 第 0 帧」的
    /// alpha 包围盒，算出每个文件夹的 Pixels Per Unit 与 Pivot，让常态 ↔ 表情切换不跳脸（支柱 2）。
    /// 表存放在 Assets/Naiwa/Editor/clip_alignment.json，由导入后处理器读取。
    /// </summary>
    public static class ClipAlignment
    {
        public const string AssetPath = "Assets/Naiwa/Editor/clip_alignment.json";

        [Serializable]
        public sealed class Entry
        {
            public string folder;
            public float ppu;
            public float pivotX;
            public float pivotY;
            public int canvasPx;
            /// <summary>
            /// 手工对齐（「Naiwa/对齐序列帧锚点」写的）。标准画布（600px）的自动对齐只在构图正好时才写表，
            /// 带这个标记的条目不会被自动流程删掉，免得手工校正「同画布但构图偏移」的序列帧后被还原。
            /// </summary>
            public bool manual;
        }

        [Serializable]
        sealed class Table
        {
            public List<Entry> entries = new List<Entry>();
        }

        static Table s_table;

        static Table Data
        {
            get
            {
                if (s_table != null) return s_table;
                string path = NaiwaEditorConfig.ToFullPath(AssetPath);
                try
                {
                    s_table = File.Exists(path) ? JsonUtility.FromJson<Table>(File.ReadAllText(path)) : new Table();
                }
                catch (Exception)
                {
                    s_table = new Table();
                }
                if (s_table.entries == null) s_table.entries = new List<Entry>();
                return s_table;
            }
        }

        public static Entry Get(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            foreach (var e in Data.entries)
                if (string.Equals(e.folder, folder, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        /// <summary>该贴图应使用的 PPU 与 Pivot（不在表里 = 标准值）。</summary>
        public static (float ppu, Vector2 pivot) For(string assetPath)
        {
            string folder = Path.GetFileName(Path.GetDirectoryName(assetPath));
            var e = Get(folder);
            return e != null ? (e.ppu, new Vector2(e.pivotX, e.pivotY)) : (PetGeometry.PixelsPerUnit, PetGeometry.Pivot);
        }

        public static void Set(Entry entry)
        {
            Data.entries.RemoveAll(e => string.Equals(e.folder, entry.folder, StringComparison.OrdinalIgnoreCase));
            Data.entries.Add(entry);
            Data.entries.Sort((a, b) => string.CompareOrdinal(a.folder, b.folder));
        }

        public static void Remove(string folder) =>
            Data.entries.RemoveAll(e => string.Equals(e.folder, folder, StringComparison.OrdinalIgnoreCase));

        public static void Save()
        {
            string path = NaiwaEditorConfig.ToFullPath(AssetPath);
            string json = JsonUtility.ToJson(Data, true);
            if (File.Exists(path) && File.ReadAllText(path) == json) return;
            File.WriteAllText(path, json);
            AssetDatabase.ImportAsset(AssetPath);
        }

        /// <summary>
        /// 画布尺寸相同（都是标准 600px）但构图整体偏了的对齐：只挪 Pivot、不动 PPU，
        /// 保持美术原有的大小，避免为了对齐包围盒把角色整体缩放一点点。
        /// </summary>
        public static Entry ComputeOffset(string folder, BBox reference, BBox target)
        {
            float refPivotX = PetGeometry.Pivot.x * reference.canvas;
            float refPivotY = PetGeometry.Pivot.y * reference.canvas;
            float dx = refPivotX - reference.CenterX;
            float dy = refPivotY - reference.minY;

            return new Entry
            {
                folder = folder,
                ppu = PetGeometry.PixelsPerUnit,
                pivotX = (float)Math.Round((target.CenterX + dx) / target.canvas, 5),
                pivotY = (float)Math.Round((target.minY + dy) / target.canvas, 5),
                canvasPx = target.canvas,
            };
        }

        public struct BBox
        {
            public int canvas;
            public float minX, maxX, minY, maxY; // 像素，y 从底边算
            public float Width => maxX - minX;
            public float Height => maxY - minY;
            public float CenterX => (minX + maxX) * 0.5f;
        }

        /// <summary>读源 PNG 的 alpha 包围盒（alpha &gt; 20）。非正方形返回 null。</summary>
        public static BBox? Measure(string pngPath)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(pngPath))) return null;
                if (tex.width != tex.height) return null;
                var px = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                int minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a <= 20) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
                if (maxX < minX) return null;
                return new BBox { canvas = w, minX = minX, maxX = maxX + 1, minY = minY, maxY = maxY + 1 };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>最大对齐搜索位移（像素）。</summary>
        public const int MaxAlignShiftPx = 120;

        /// <summary>
        /// 同画布（都是标准 600px）但构图整体偏了的对齐：按轮廓重叠度（IoU）找 target 相对参照的水平位移，
        /// 只挪 Pivot、不动 PPU。比包围盒可靠 —— 尾巴、耳朵、手里的道具会把包围盒带偏。
        /// </summary>
        /// <param name="referencePng">参照帧（通常是该阶段 idle 第 0 帧）。</param>
        /// <param name="targetPng">要对齐的帧（通常是该表情第 0 帧）。</param>
        /// <param name="shift">负值 = target 相对参照偏右（要往左挪 |shift|）。</param>
        /// <param name="iou">对齐后的重叠度，越接近 1 说明两帧姿势越一致、位移越可信。</param>
        public static Entry ComputeSilhouette(string folder, string referencePng, BBox reference, string targetPng, BBox target, out int shift, out float iou)
        {
            shift = BestSilhouetteShift(referencePng, targetPng, out iou);

            // 参照用的是标准 Pivot（0.5）；target 偏左 shift 像素 → 把锚点往右挪同样的量，画面就会往左移
            float refPivotY = PetGeometry.Pivot.y * reference.canvas;
            float dy = refPivotY - reference.minY;

            return new Entry
            {
                folder = folder,
                ppu = PetGeometry.PixelsPerUnit,
                pivotX = (float)Math.Round(PetGeometry.Pivot.x - shift / (float)target.canvas, 5),
                pivotY = (float)Math.Round((target.minY + dy) / target.canvas, 5),
                canvasPx = target.canvas,
            };
        }

        /// <summary>两帧 alpha 轮廓的最佳水平位移：先粗扫（步长 2px）再在 ±2px 内细化。</summary>
        static int BestSilhouetteShift(string referencePng, string targetPng, out float bestIou)
        {
            var reference = AlphaMask(referencePng, out int rw, out int rh);
            var target = AlphaMask(targetPng, out int tw, out int th);
            bestIou = 0f;
            if (reference == null || target == null || rw != tw || rh != th) return 0;

            int best = 0;
            for (int s = -MaxAlignShiftPx; s <= MaxAlignShiftPx; s += 2)
            {
                float iou = SilhouetteIoU(reference, target, rw, rh, s);
                if (iou > bestIou) { bestIou = iou; best = s; }
            }
            for (int s = best - 2; s <= best + 2; s++)
            {
                float iou = SilhouetteIoU(reference, target, rw, rh, s);
                if (iou > bestIou) { bestIou = iou; best = s; }
            }
            return best;
        }

        static float SilhouetteIoU(bool[] reference, bool[] target, int w, int h, int shift)
        {
            int inter = 0, union = 0;
            for (int y = 0; y < h; y += 2)
            for (int x = 0; x < w; x += 2)
            {
                bool a = reference[y * w + x];
                int xs = x - shift;
                bool b = xs >= 0 && xs < w && target[y * w + xs];
                if (a && b) inter++;
                if (a || b) union++;
            }
            return union > 0 ? inter / (float)union : 0f;
        }

        static bool[] AlphaMask(string pngPath, out int width, out int height)
        {
            width = height = 0;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(pngPath))) return null;
                int w = tex.width, h = tex.height;
                var px = tex.GetPixels32();
                var mask = new bool[w * h];
                for (int i = 0; i < mask.Length; i++) mask[i] = px[i].a > 20;
                width = w;
                height = h;
                return mask;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// 以 idle 第 0 帧（标准画布、标准 PPU / Pivot）为参照，算出让 target 第 0 帧在世界坐标里与之重合的 PPU 与 Pivot。
        /// </summary>
        public static Entry Compute(string folder, BBox idle, BBox target)
        {
            // idle 每像素对应的世界单位
            float idleUnitsPerPx = 1f / PetGeometry.PixelsPerUnit;
            // target 每像素对应多少 idle 像素（宽高比例取平均）
            float scale = (idle.Width / target.Width + idle.Height / target.Height) * 0.5f;
            float ppu = 1f / (scale * idleUnitsPerPx);

            // idle 的脚底（pivot）相对 idle 包围盒的偏移，映射到 target
            float idlePivotX = PetGeometry.Pivot.x * idle.canvas;
            float idlePivotY = PetGeometry.Pivot.y * idle.canvas;
            float dx = (idlePivotX - idle.CenterX) / scale;
            float dy = (idlePivotY - idle.minY) / scale;
            float pivotX = (target.CenterX + dx) / target.canvas;
            float pivotY = (target.minY + dy) / target.canvas;

            return new Entry
            {
                folder = folder,
                ppu = (float)Math.Round(ppu, 3),
                pivotX = (float)Math.Round(pivotX, 5),
                pivotY = (float)Math.Round(pivotY, 5),
                canvasPx = target.canvas,
            };
        }
    }
}
