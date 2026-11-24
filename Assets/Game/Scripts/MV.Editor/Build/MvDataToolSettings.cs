using UnityEditor;

namespace MV.Editor.Build
{
    /// <summary>
    /// Framework 데이터 도구(표 제작·「Localization Sheet Import」 창)의 기본 주소·경로를 이 프로젝트 값으로 등록한다.
    /// 등록이 없으면 창이 빈 시트 주소와 Framework 기본 경로(<c>Assets/GameData/...</c>)로 열린다.
    /// 메모리의 기본값만 바꾸고 자산은 건드리지 않는다. 이미 열린 창은 닫았다 다시 열어야 새 기본값을 읽는다.
    /// </summary>
    [InitializeOnLoad]
    static class MvDataToolSettings
    {
        /// <summary>언어팩 문구 원본 시트 「MoonVillage Localization」(D-LOC-PLAN ⑤, 탭 = 컬렉션, 열 Key·ko·en·ja).</summary>
        public const string LocalizationSpreadsheet = "https://docs.google.com/spreadsheets/d/1L8bOKhK_bjuoJSXTywXbY9sv7zNdZ8Ivqth27ouZq9o/edit";

        static MvDataToolSettings() => jjevol.CommonDataToolSettings.Current = new Settings();

        sealed class Settings : jjevol.ICommonDataToolSettings
        {
            public string TableInputDirectory => "Assets/Game/GameData/Table/TSV";
            public string TableOutputDirectory => "Assets/Game/GameData/Table/Resources";
            public string LocalizationInputDirectory => MvLocBuild.SheetDir;
            public string LocalizationOutputDirectory => MvLocBuild.TablesDir;
            public string LocalizationSpreadsheet => MvDataToolSettings.LocalizationSpreadsheet;
        }
    }
}
