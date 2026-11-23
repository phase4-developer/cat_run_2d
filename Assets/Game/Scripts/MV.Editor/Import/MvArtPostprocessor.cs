using System;
using UnityEditor;
using UnityEngine;

namespace MV.Editor.Import
{
    /// <summary>
    /// 추출 PNG 임포트 후처리(기획서 03 §3.2.8). `Art/(Source|Temp)/Sprite/Atlas_*/*.png`·`Art/(Source|Temp)/Texture/*.png`에
    /// manifest의 PPU·피벗·9-slice와 §3.8.2 공통 값을 붙인다. 새 PNG·새 아틀라스 폴더가 들어오면 <see cref="MvArtSetup"/>가
    /// 아틀라스·Addressables 주소를 맞춘다.
    /// <para>설정 소유권(중앙 policies/Unity/EditorTools.md 「Editor 자동화의 설정 소유권」, MV-ART-IMPORTOWN 2026-11-23):
    /// manifest에 있는 PNG는 추출 도구 산출물이라 manifest 값을 늘 붙인다. manifest에 없는 PNG(디자이너가 넣은 그림)는
    /// 처음 Import(<c>importSettingsMissing</c>)일 때만 기본값을 넣고, 그 뒤 사람이 Inspector에서 정한 PPU·9-slice·Mesh Type은 다시 쓰지 않는다.</para>
    /// </summary>
    public sealed class MvArtPostprocessor : AssetPostprocessor
    {
        /// <summary>규칙을 바꾸면 올린다(해당 텍스처 재임포트).</summary>
        const uint Version = 1;

        public override uint GetVersion() => Version;

        void OnPreprocessTexture()
        {
            if (!MvArtRules.TryParse(assetPath, out var asset)) return;
            if (!(assetImporter is TextureImporter importer)) return;
            var desired = MvArtImport.Desired(asset);
            if (desired.FromDefaults && !importer.importSettingsMissing) return; // 사람이 정한 Import 설정(디자이너 그림)은 그대로 둔다
            MvArtImport.Apply(importer, desired);
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom, bool didDomainReload)
        {
            if (didDomainReload || Touches(imported) || Touches(deleted) || Touches(moved) || Touches(movedFrom))
                MvArtSetup.RequestEnsure();
        }

        static bool Touches(string[] paths)
        {
            if (paths == null) return false;
            foreach (var p in paths)
            {
                if (p.StartsWith(MvArtRules.ArtRoot + "/", StringComparison.Ordinal)) return true;
                if (p.StartsWith(MvArtRules.UiAtlasFolder + "/", StringComparison.Ordinal)) return true;
                if (p.StartsWith(MvArtRules.WorldAtlasFolder + "/", StringComparison.Ordinal)) return true;
                if (p.StartsWith(MvArtRules.TextureFolder + "/", StringComparison.Ordinal)) return true;
                if (p.StartsWith(MvArtRules.CharacterRoot + "/", StringComparison.Ordinal)) return true;
                if (p.StartsWith(MvArtRules.BgPrefabFolder + "/", StringComparison.Ordinal)) return true;
                if (p.StartsWith(MvArtRules.ProjectilePrefabFolder + "/", StringComparison.Ordinal)) return true;
                if (p.StartsWith(MvArtRules.ProjectileImageFolder + "/", StringComparison.Ordinal)) return true;
                if (p.StartsWith(MvArtRules.EffectPrefabFolder + "/", StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
