using System;
using UnityEditor;
using UnityEngine;

namespace MV.Editor.Import
{
    /// <summary>
    /// 추출 PNG 임포트 후처리(기획서 03 §3.2.8). `Art/(Source|Temp)/Sprite/Atlas_*/*.png`·`Art/(Source|Temp)/Texture/*.png`에
    /// manifest의 PPU·피벗·9-slice와 §3.8.2 공통 값을 붙인다. 새 PNG·새 아틀라스 폴더가 들어오면 <see cref="MvArtSetup"/>가
    /// 아틀라스·Addressables 주소를 맞춘다.
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
            if (desired.FromDefaults)
                Debug.LogWarning($"[MvArtPostprocessor] manifest에 없는 PNG — 기본 PPU·피벗 적용: {assetPath}");
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
            }
            return false;
        }
    }
}
