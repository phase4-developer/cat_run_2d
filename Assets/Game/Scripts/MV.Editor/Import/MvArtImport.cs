using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MV.Editor.Import
{
    /// <summary>PNG 1장에 붙일 임포트 값(03 §3.2.8, §3.8.2).</summary>
    public sealed class MvImportSettings
    {
        public MvArtKind Kind;
        public float Ppu;
        public Vector2 Pivot;
        public Vector4 Border;
        public SpriteMeshType MeshType;
        /// <summary>Android·iOS 덮어쓰기 형식. 아틀라스에 들어가는 스프라이트는 null(아틀라스가 압축).</summary>
        public TextureImporterFormat? MobileFormat;
        /// <summary>manifest에 없어서 기본값을 쓴 경우.</summary>
        public bool FromDefaults;
    }

    /// <summary>
    /// 임포트 규칙 적용·비교. <see cref="MvArtPostprocessor"/>가 새 PNG에, <see cref="MvArtSetup"/>가 기존 PNG에 쓴다.
    /// </summary>
    public static class MvArtImport
    {
        public const string AndroidPlatform = "Android";
        public const string IosPlatform = "iPhone";
        public static readonly string[] MobilePlatforms = { AndroidPlatform, IosPlatform };
        const float Epsilon = 1e-4f;

        public static MvImportSettings Desired(MvArtAsset asset)
        {
            var entry = MvArtManifest.Find(asset.Root, asset.File);
            var s = new MvImportSettings
            {
                Kind = asset.Kind,
                FromDefaults = entry == null,
                Ppu = entry != null && entry.Ppu > 0 ? entry.Ppu : MvArtRules.DefaultPpu(asset),
                Pivot = entry?.Pivot ?? new Vector2(0.5f, 0.5f),
                Border = entry?.Border ?? Vector4.zero,
                MeshType = asset.Kind == MvArtKind.World ? SpriteMeshType.Tight : SpriteMeshType.FullRect,
                MobileFormat = asset.Kind == MvArtKind.Texture ? MvArtRules.TextureFormatFor(asset.Name) : (TextureImporterFormat?)null,
            };
            return s;
        }

        /// <summary>규칙 값을 넣는다. 저장·재임포트는 호출자 몫(전처리 안이면 필요 없음).</summary>
        public static void Apply(TextureImporter importer, MvImportSettings desired)
        {
            var t = new TextureImporterSettings();
            importer.ReadTextureSettings(t);
            t.textureType = TextureImporterType.Sprite;
            t.textureShape = TextureImporterShape.Texture2D;
            t.spriteMode = (int)SpriteImportMode.Single;
            t.spriteMeshType = desired.MeshType;
            t.spriteAlignment = (int)SpriteAlignment.Custom;
            t.spritePivot = desired.Pivot;
            t.spriteBorder = desired.Border;
            t.spritePixelsPerUnit = desired.Ppu;
            t.spriteGenerateFallbackPhysicsShape = false;
            t.spriteExtrude = 1;
            t.sRGBTexture = true;
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = true;
            t.readable = false;
            t.mipmapEnabled = false;
            t.streamingMipmaps = false;
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            t.aniso = 0;
            t.npotScale = TextureImporterNPOTScale.None;
            importer.SetTextureSettings(t);

            importer.maxTextureSize = MvArtRules.MaxTextureSize;
            importer.textureCompression = desired.MobileFormat.HasValue ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;

            foreach (var platform in MobilePlatforms)
            {
                var p = importer.GetPlatformTextureSettings(platform);
                if (desired.MobileFormat.HasValue)
                {
                    p.overridden = true;
                    p.maxTextureSize = MvArtRules.MaxTextureSize;
                    p.format = desired.MobileFormat.Value;
                    importer.SetPlatformTextureSettings(p);
                }
                else if (p.overridden)
                {
                    importer.ClearPlatformTextureSettings(platform);
                }
            }
        }

        /// <summary>규칙과 다른 항목 목록. 같으면 빈 목록.</summary>
        public static List<string> Differences(TextureImporter importer, MvImportSettings desired)
        {
            var d = new List<string>();
            var t = new TextureImporterSettings();
            importer.ReadTextureSettings(t);
            if (t.textureType != TextureImporterType.Sprite) d.Add($"textureType {t.textureType}");
            if (t.spriteMode != (int)SpriteImportMode.Single) d.Add($"spriteMode {t.spriteMode}");
            if (t.spriteMeshType != desired.MeshType) d.Add($"meshType {t.spriteMeshType}≠{desired.MeshType}");
            if (t.spriteAlignment != (int)SpriteAlignment.Custom) d.Add($"alignment {t.spriteAlignment}");
            if ((t.spritePivot - desired.Pivot).sqrMagnitude > Epsilon * Epsilon) d.Add($"pivot {t.spritePivot}≠{desired.Pivot}");
            if ((t.spriteBorder - desired.Border).sqrMagnitude > Epsilon) d.Add($"border {t.spriteBorder}≠{desired.Border}");
            if (Math.Abs(t.spritePixelsPerUnit - desired.Ppu) > Epsilon) d.Add($"ppu {t.spritePixelsPerUnit}≠{desired.Ppu}");
            if (t.spriteGenerateFallbackPhysicsShape) d.Add("physicsShape on");
            if (!t.sRGBTexture) d.Add("sRGB off");
            if (!t.alphaIsTransparency) d.Add("alphaIsTransparency off");
            if (t.readable) d.Add("readable on");
            if (t.mipmapEnabled) d.Add("mipmap on");
            if (t.filterMode != FilterMode.Bilinear) d.Add($"filter {t.filterMode}");
            if (t.wrapModeU != TextureWrapMode.Clamp || t.wrapModeV != TextureWrapMode.Clamp) d.Add("wrap not clamp");
            if (t.aniso != 0) d.Add($"aniso {t.aniso}");
            if (importer.maxTextureSize != MvArtRules.MaxTextureSize) d.Add($"maxSize {importer.maxTextureSize}");
            var compression = desired.MobileFormat.HasValue ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
            if (importer.textureCompression != compression) d.Add($"compression {importer.textureCompression}");
            foreach (var platform in MobilePlatforms)
            {
                var p = importer.GetPlatformTextureSettings(platform);
                if (desired.MobileFormat.HasValue)
                {
                    if (!p.overridden || p.format != desired.MobileFormat.Value || p.maxTextureSize != MvArtRules.MaxTextureSize)
                        d.Add($"{platform} {(p.overridden ? p.format.ToString() : "not overridden")}≠{desired.MobileFormat.Value}");
                }
                else if (p.overridden) d.Add($"{platform} overridden {p.format}");
            }
            return d;
        }
    }
}
