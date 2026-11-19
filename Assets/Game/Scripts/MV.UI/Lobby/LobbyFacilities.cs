namespace MV.UI
{
    /// <summary>
    /// 로비 강화 아이콘 단계 문턱(정본 <c>azStages</c> L3468~3471의 캣타워·생선 건조대). 아지트 방(옛 <c>LobbyStage</c>)과
    /// 시설 단계·반짝임 연결은 로비 트랙 전환 뒤 받는 곳이 없어 2026-11-19 정리했다(MV-CLEAN-AZIT).
    /// 문턱 숫자를 어떻게 둘지는 원장 MV-PC-LOBBYFAC(PC-19, 「로비 작업 끝난 뒤 정리」)가 정한다.
    /// </summary>
    public static class LobbyFacilities
    {
        /// <summary>캣타워 단계(시작 계단 Lv 기준, 정본 문턱 — 표 열 없음). 로비 아이콘(LobbyPresenter)이 쓴다.</summary>
        public static int TowerStage(int stairLv) => stairLv >= 4 ? 3 : stairLv >= 2 ? 2 : 1;

        /// <summary>생선 건조대 단계(구매 수 기준, 정본 문턱 — 표 열 없음). 로비 아이콘(LobbyPresenter)이 쓴다.</summary>
        public static int RackStage(int incomeBought) => incomeBought >= 15 ? 3 : incomeBought >= 5 ? 2 : 1;
    }
}
