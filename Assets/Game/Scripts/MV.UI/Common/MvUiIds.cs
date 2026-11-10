namespace MV.UI
{
    /// <summary>
    /// UI ID(Framework 주소 = 접두어 + ID, 05 §5.1.14). 교체 지점: Addressables 등록 뒤 `JJEvol/UI/Generate UI ID Constants`가 만드는 상수로 바꿔도 값은 같다.
    /// </summary>
    public static class MvUiIds
    {
        /// <summary><c>UIScreen_Lobby</c>.</summary>
        public const string Lobby = "Lobby";
        /// <summary><c>UIHud_Run</c>.</summary>
        public const string Run = "Run";
        /// <summary><c>UIPopup_Result</c>.</summary>
        public const string Result = "Result";
        /// <summary><c>UIPopup_GodSelect</c>(MV-UI-META).</summary>
        public const string GodSelect = "GodSelect";
        /// <summary><c>UIPopup_AreaUpgrade</c>(MV-UI-META).</summary>
        public const string AreaUpgrade = "AreaUpgrade";
        /// <summary><c>UIPopup_Rebirth</c>(MV-UI-META).</summary>
        public const string Rebirth = "Rebirth";
        /// <summary><c>UIPopup_Map</c>(MV-UI-META).</summary>
        public const string Map = "Map";
        /// <summary><c>UIPopup_FeatureUnlock</c>(MV-UI-META).</summary>
        public const string FeatureUnlock = "FeatureUnlock";
        /// <summary><c>UIPopup_Debug</c>(MV-UI-META, 개발 빌드 전용).</summary>
        public const string Debug = "Debug";
        /// <summary><c>UIPopup_FakeAd</c>(MV-UI-LOBBY, 광고 SDK 없을 때만).</summary>
        public const string FakeAd = "FakeAd";
        /// <summary><c>UIPopup_ContentMenu</c>(MV-UI-CONTENT).</summary>
        public const string ContentMenu = "ContentMenu";
        /// <summary><c>UIPopup_Research</c>(MV-META-RESEARCH, 컨텐츠 메뉴 「성장」 탭).</summary>
        public const string Research = "Research";
    }

    /// <summary>층 sortingOrder(05 §5.1.5). 각 화면 프리팹 루트의 Canvas(overrideSorting)에 넣는다.</summary>
    public static class MvUiLayer
    {
        public const int Vignette = 10;
        public const int Screen = 100;
        public const int Hud = 200;
        public const int FloatText = 210;
        public const int HudCenter = 220;
        public const int Overlay = 280;
        public const int Result = 300;
        public const int System = 400;
        public const int Debug = 410;
        public const int Popup = 500;
        public const int FullPanel = 550;
        public const int Unlock = 600;
        public const int Ad = 700;
        public const int Toast = 800;
        public const int Loading = 900;
    }
}
