using System;
using System.Text.RegularExpressions;
using UnityEditor;

namespace MV.Editor.Import
{
    /// <summary>추출 PNG 한 장의 갈래(기획서 03 §3.8.2 열).</summary>
    public enum MvArtKind
    {
        None,
        /// <summary>월드 스프라이트(Tight 메쉬). 월드 테마·고양이·로비·Fx 아틀라스.</summary>
        World,
        /// <summary>UI 스프라이트(Full Rect).</summary>
        Ui,
        /// <summary>단독 텍스처 `Tex_*`(AddressableTexture 주소).</summary>
        Texture,
    }

    /// <summary>경로에서 읽은 추출 그림 정보. 폴더 규칙: `Art/(Source|Temp)/Sprite/&lt;Atlas&gt;/&lt;Name&gt;.png`, `Art/(Source|Temp)/Texture/&lt;Name&gt;.png`.</summary>
    public readonly struct MvArtAsset
    {
        public readonly string AssetPath;
        /// <summary>`Source` 또는 `Temp`.</summary>
        public readonly string Root;
        /// <summary>뿌리 아래 경로(`Sprite/Atlas_UI_Icon/UI_Icon_Gold.png`) — manifest의 `file` 값과 같다.</summary>
        public readonly string File;
        /// <summary>아틀라스 이름. 단독 텍스처면 null.</summary>
        public readonly string Atlas;
        /// <summary>스프라이트 이름 = 파일 이름(확장자 제외).</summary>
        public readonly string Name;
        public readonly MvArtKind Kind;

        public MvArtAsset(string assetPath, string root, string file, string atlas, string name, MvArtKind kind)
        {
            AssetPath = assetPath;
            Root = root;
            File = file;
            Atlas = atlas;
            Name = name;
            Kind = kind;
        }

        public bool IsTemp => Root == MvArtRules.TempRootName;
    }

    /// <summary>
    /// 아틀라스 1개의 설정(03 §3.8.1, §3.8.4, §3.8.5, D-54). 이름 접두가 설정을 정한다(폴더 규칙, §3.2.8).
    /// </summary>
    public sealed class MvAtlasSpec
    {
        public string Name { get; internal set; }
        public bool IsUi { get; internal set; }
        public int MaxSize { get; internal set; }
        /// <summary>Android·iOS 형식.</summary>
        public TextureImporterFormat Format { get; internal set; }
        public bool Mipmap { get; internal set; }
        public bool Tight { get; internal set; }
        public int Padding { get; internal set; }
        /// <summary>Addressables 라벨(§3.8.5). 규칙이 없는 새 아틀라스는 null.</summary>
        public string Label { get; internal set; }
        /// <summary>기획서 예상 크기 [추정](참고 표시).</summary>
        public int ExpectedWidth { get; internal set; }
        public int ExpectedHeight { get; internal set; }

        public string Group => IsUi ? MvArtRules.UiAtlasGroup : MvArtRules.WorldAtlasGroup;
        public string Folder => IsUi ? MvArtRules.UiAtlasFolder : MvArtRules.WorldAtlasFolder;
        public string AssetPath => Folder + "/" + Name + MvArtRules.AtlasExtension;
        /// <summary>Addressables 주소 = 아틀라스 이름(03 §3.4.2).</summary>
        public string Address => Name;
    }

    /// <summary>
    /// 아틀라스·임포트 규칙의 정본(코드). 폴더·이름 접두만 보고 정하므로 새 PNG·새 아틀라스 폴더에도 같은 규칙이 붙는다.
    /// </summary>
    public static class MvArtRules
    {
        public const string SourceRootName = "Source";
        public const string TempRootName = "Temp";
        public const string ArtRoot = "Assets/Game/Art";
        public const string SourceRoot = ArtRoot + "/" + SourceRootName;
        public const string TempRoot = ArtRoot + "/" + TempRootName;
        public static readonly string[] ArtRoots = { SourceRoot, TempRoot };

        public const string SpriteFolderName = "Sprite";
        public const string TextureFolderName = "Texture";

        public const string SourceManifestPath = SourceRoot + "/manifest.json";
        public const string TempManifestPath = TempRoot + "/manifest.json";
        public const string CatalogPath = SourceRoot + "/sprite_catalog.tsv";
        public const string TempListPath = TempRoot + "/temp_art_list.tsv";

        public const string AddressablesRemote = "Assets/Game/Addressables/Remote";
        public const string UiAtlasFolder = AddressablesRemote + "/UIAtlas";
        public const string WorldAtlasFolder = AddressablesRemote + "/WorldAtlas";
        public const string TextureFolder = AddressablesRemote + "/Texture";
        public const string AtlasExtension = ".spriteatlasv2";

        /// <summary>Managed Addressables 폴더 규칙의 그룹 이름(`Remote/&lt;Category&gt;` → `Remote_&lt;Category&gt;`).</summary>
        public const string UiAtlasGroup = "Remote_UIAtlas";
        public const string WorldAtlasGroup = "Remote_WorldAtlas";
        public const string TextureGroup = "Remote_Texture";
        public static readonly string[] OwnedGroups = { UiAtlasGroup, WorldAtlasGroup, TextureGroup };

        public const int MaxTextureSize = 2048;
        public const int WorldPpu = 128;
        public const int LobbyPpu = 160;
        public const int UiPpu = 128;

        public const string ThemeAtlasPrefix = "Atlas_World_Theme_";
        public const string CatAtlasPrefix = "Atlas_World_Cat_T";
        public const string FriendAtlasPrefix = "Atlas_World_Friend_T";
        public const string LobbyAtlas = "Atlas_World_Lobby";

        static readonly Regex s_split = new Regex(@"_\d+$", RegexOptions.CultureInvariant);
        static readonly Regex s_theme = new Regex(@"^Atlas_World_Theme_([A-Za-z0-9]+)(_\d+)?$", RegexOptions.CultureInvariant);

        /// <summary>경로가 추출 그림 규칙에 맞으면 정보를 돌려준다. `.png`만.</summary>
        public static bool TryParse(string assetPath, out MvArtAsset asset)
        {
            asset = default;
            if (string.IsNullOrEmpty(assetPath)) return false;
            var path = assetPath.Replace('\\', '/');
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return false;

            string root;
            if (path.StartsWith(SourceRoot + "/", StringComparison.Ordinal)) root = SourceRootName;
            else if (path.StartsWith(TempRoot + "/", StringComparison.Ordinal)) root = TempRootName;
            else return false;

            var file = path.Substring(ArtRoot.Length + 1 + root.Length + 1);
            var parts = file.Split('/');
            var name = parts[parts.Length - 1];
            name = name.Substring(0, name.Length - 4);

            if (parts.Length >= 3 && parts[0] == SpriteFolderName && parts[1].StartsWith("Atlas_", StringComparison.Ordinal))
            {
                var atlas = parts[1];
                asset = new MvArtAsset(path, root, file, atlas, name, IsUiAtlas(atlas) ? MvArtKind.Ui : MvArtKind.World);
                return true;
            }

            if (parts.Length >= 2 && parts[0] == TextureFolderName)
            {
                asset = new MvArtAsset(path, root, file, null, name, MvArtKind.Texture);
                return true;
            }

            return false;
        }

        public static bool IsUiAtlas(string atlas) => atlas != null && atlas.StartsWith("Atlas_UI_", StringComparison.Ordinal);

        /// <summary>로비 아틀라스와 그 `_2` 나눔(03 §3.8.1). 나눔은 `Tools/MvExtract/jobs`의 job별 `atlas`가 정한다(시설 Tower·Rack = `_2`).</summary>
        public static bool IsLobbyAtlas(string atlas) => atlas != null && s_split.Replace(atlas, "") == LobbyAtlas;

        /// <summary>테마 아틀라스면 테마 key(`_2` 나눔 접미 제외), 아니면 null.</summary>
        public static string ThemeKeyOf(string atlas)
        {
            if (atlas == null) return null;
            var m = s_theme.Match(atlas);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>아틀라스 설정(03 §3.8.1). 2048을 넘어 `_2`로 나눈 아틀라스는 원래 이름의 설정을 쓴다.</summary>
        public static MvAtlasSpec SpecFor(string atlas)
        {
            if (string.IsNullOrEmpty(atlas)) throw new ArgumentException("atlas name required", nameof(atlas));
            var spec = new MvAtlasSpec { Name = atlas, MaxSize = MaxTextureSize };
            var baseName = ThemeKeyOf(atlas) != null ? atlas : s_split.Replace(atlas, "");
            // 고양이 룩 번호(T1)는 숫자로 끝나지만 `_숫자`가 아니라서 위 정규식에 걸리지 않는다.

            if (IsUiAtlas(atlas))
            {
                spec.IsUi = true;
                spec.Format = TextureImporterFormat.ASTC_4x4;
                spec.Mipmap = false;
                spec.Tight = false;
                spec.Padding = 4;
                spec.ExpectedWidth = spec.ExpectedHeight = 1024;
                switch (baseName)
                {
                    case "Atlas_UI_Common":
                    case "Atlas_UI_Hud":
                    case "Atlas_UI_Icon":
                        spec.Label = "ui.core";
                        break;
                    case "Atlas_UI_Lobby":
                        spec.Label = "ui.lobby";
                        break;
                    case "Atlas_UI_Card":
                        spec.Label = "ui.card";
                        break;
                }
                return spec;
            }

            spec.IsUi = false;
            if (atlas.StartsWith("Atlas_Fx_", StringComparison.Ordinal))
            {
                // 표의 Fx 패딩 칸은 4지만 같은 절 규칙 「Mipmap을 켜는 아틀라스는 8 px」을 따른다(보고에 불일치 적음).
                spec.MaxSize = 1024;
                spec.Format = TextureImporterFormat.ASTC_4x4;
                spec.Mipmap = true;
                spec.Tight = false;
                spec.Padding = 8;
                spec.Label = "fx";
                spec.ExpectedWidth = 1024;
                spec.ExpectedHeight = 512;
                return spec;
            }

            spec.Format = TextureImporterFormat.ASTC_6x6;
            spec.Tight = true;
            if (baseName == LobbyAtlas)
            {
                spec.Mipmap = false;
                spec.Padding = 4;
                spec.Label = "lobby";
                spec.ExpectedWidth = spec.ExpectedHeight = 2048;
                return spec;
            }

            spec.Mipmap = true;
            spec.Padding = 8;
            spec.ExpectedWidth = spec.ExpectedHeight = 2048;
            var key = ThemeKeyOf(atlas);
            if (key != null) spec.Label = "theme." + key;
            else if (atlas.StartsWith(CatAtlasPrefix, StringComparison.Ordinal))
            {
                spec.Label = "cat";
                spec.ExpectedHeight = 1024;
            }
            else if (atlas.StartsWith(FriendAtlasPrefix, StringComparison.Ordinal)) spec.Label = "cat";
            return spec;
        }

        /// <summary>단독 텍스처의 Android·iOS 형식(03 §3.8.3): 방 배경 6x6, 신 초상·비네트 등 4x4.</summary>
        public static TextureImporterFormat TextureFormatFor(string name) =>
            name != null && name.StartsWith("Tex_Lobby_Room", StringComparison.Ordinal) ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4;

        /// <summary>단독 텍스처 라벨(03 §3.8.5 `lobby`/`ui.god`). 규칙 없는 이름은 null.</summary>
        public static string TextureLabelFor(string name)
        {
            if (name == null) return null;
            if (name.StartsWith("Tex_God_", StringComparison.Ordinal)) return "ui.god";
            if (name.StartsWith("Tex_Lobby_", StringComparison.Ordinal)) return "lobby";
            return null;
        }

        /// <summary>manifest에 값이 없을 때의 기본 PPU.</summary>
        public static int DefaultPpu(MvArtAsset asset)
        {
            if (asset.Kind == MvArtKind.Ui) return UiPpu;
            if (IsLobbyAtlas(asset.Atlas)) return LobbyPpu;
            return WorldPpu;
        }
    }
}
