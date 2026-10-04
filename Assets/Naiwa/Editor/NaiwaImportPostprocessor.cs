using Naiwa.Core;
using UnityEditor;
using UnityEngine;

namespace Naiwa.EditorTools
{
    /// <summary>
    /// 对序列帧目录（默认 Assets/Resources/AnimationImages/**）自动应用 §V.3.1 的导入设置，不靠手点。
    /// </summary>
    public sealed class NaiwaImportPostprocessor : AssetPostprocessor
    {
        public const int MaxSize = 1024;
        const string StandalonePlatform = "Standalone";

        /// <summary>
        /// [P] Mipmap 关闭：实测 Unity 2022.3 对开启 Mipmap 的 600×600（NPOT）贴图不做 BC7 压缩，
        /// 会退回 RGBA32（每帧约 1.9MB，包体约 1.8GB）。按 §V.3.1「显存超预算再关」关闭。
        /// </summary>
        public const bool UseMipmaps = false;

        public override uint GetVersion() => 3;

        void OnPreprocessTexture()
        {
            if (!NaiwaEditorConfig.IsUnderClipsRoot(assetPath)) return;
            Apply((TextureImporter)assetImporter);
        }

        public static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var (ppu, pivot) = ClipAlignment.For(importer.assetPath);

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            s.spriteMode = (int)SpriteImportMode.Single;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            s.spritePixelsPerUnit = ppu;
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteExtrude = 1;
            s.spriteGenerateFallbackPhysicsShape = true;
            s.mipmapEnabled = UseMipmaps;
            s.alphaIsTransparency = true;
            s.alphaSource = TextureImporterAlphaSource.FromInput;
            s.filterMode = FilterMode.Bilinear;
            s.wrapMode = TextureWrapMode.Clamp;
            s.readable = false;
            s.sRGBTexture = true;
            s.npotScale = TextureImporterNPOTScale.None;
            importer.SetTextureSettings(s);

            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;

            var ps = importer.GetPlatformTextureSettings(StandalonePlatform);
            ps.overridden = true;
            ps.maxTextureSize = MaxSize;
            ps.format = TextureImporterFormat.BC7;
            ps.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(ps);
        }

        /// <summary>检查导入设置是否与规范一致，返回不一致的描述；一致返回 null。</summary>
        public static string Check(TextureImporter importer)
        {
            if (importer.textureType != TextureImporterType.Sprite) return "Texture Type 不是 Sprite";
            if (importer.spriteImportMode != SpriteImportMode.Single) return "Sprite Mode 不是 Single";
            var (ppu, pivot) = ClipAlignment.For(importer.assetPath);
            if (Mathf.Abs(importer.spritePixelsPerUnit - ppu) > 0.01f) return $"PPU={importer.spritePixelsPerUnit}（应为 {ppu}）";
            if (Vector2.Distance(importer.spritePivot, pivot) > 0.001f) return $"Pivot={importer.spritePivot}（应为 {pivot}）";
            if (importer.mipmapEnabled != UseMipmaps) return UseMipmaps ? "Mipmap 未开启" : "Mipmap 应关闭";
            if (!importer.alphaIsTransparency) return "Alpha Is Transparency 未开启";
            var ps = importer.GetPlatformTextureSettings(StandalonePlatform);
            if (!ps.overridden || ps.format != TextureImporterFormat.BC7) return "Standalone 未设置 BC7";
            if (ps.maxTextureSize != MaxSize) return $"Max Size={ps.maxTextureSize}";
            return null;
        }
    }
}
