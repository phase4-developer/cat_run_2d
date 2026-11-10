using System;
using jjevol;
using MV.Data;
using MV.Save;

namespace MV.UI
{
    /// <summary>로비가 읽는 영속 값(05 §5.4.2 1차: Level·Area·재화).</summary>
    public struct LobbyState
    {
        public int Level;
        /// <summary>1기반 Area 번호(<c>save.area + 1</c>).</summary>
        public int AreaNo;
        /// <summary><c>name:&lt;302_area id&gt;</c>. 없으면 빈 문자열.</summary>
        public string AreaName;
        public long Gold;
        public long Souls;
        public long Dia;
        /// <summary>현재 Area <c>302_area.needSouls</c>.</summary>
        public int NeedSouls;
    }

    public interface ILobbySource
    {
        bool TryRead(out LobbyState state);

        /// <summary>
        /// 로비 아지트 시설 단계(0 = 잠김). key는 <see cref="LobbyFacilities.Keys"/>(= <c>LobbyIso.Slots</c>).
        /// 게임 루프(MV-PLAY-INT)가 로비를 열 때와 <see cref="LobbyViewData.OnFacilityBump"/> 때 <c>LobbyStage.SetFacility</c>로 넘긴다.
        /// </summary>
        int FacilityStage(string key, out int extra);
    }

    /// <summary>
    /// 로비 화면 데이터. 게임 루프(MV-PLAY-INT)가 런 시작 콜백을 넘긴다.
    /// <c>UIManager.Instance.screenManager.ChangeUIScreenAsync(MvUiIds.Lobby, data)</c>.
    /// </summary>
    public sealed class LobbyViewData : IScreenData
    {
        /// <summary>출발 입력(화면 누름·Enter). 한 번 보일 때 한 번만 부른다.</summary>
        public Action OnStart;
        /// <summary>비우면 <see cref="MvSaveLobbySource"/>.</summary>
        public ILobbySource Source;
        /// <summary>강화·잠금·메타 화면 입구. 비우면 <see cref="MvMetaUiPort.Instance"/>(<c>MvMeta.Current</c>).</summary>
        public IMetaUiPort Port;
        /// <summary>
        /// 강화 구매로 시설이 반짝일 때(시설 key, 정본 <c>AZ.bump</c>). 게임 루프가 시설 단계를 다시 넣고 <c>LobbyStage.Bump(key)</c>를 부른다.
        /// </summary>
        public Action<string> OnFacilityBump;
    }

    /// <summary>로비 겉(D-92). 컨트롤러 <see cref="LobbyScreen"/>는 이 인터페이스로만 겉을 만진다.</summary>
    public interface ILobbyView
    {
        /// <summary>출발 영역을 눌렀다(포인터 누름 또는 Submit).</summary>
        event Action StartPressed;
        void SetLevel(string text);
        void SetArea(string text);
        void SetWealth(string gold, string souls, string dia);
    }

    /// <summary>저장(<see cref="MvSave.Current"/>)과 표(<see cref="MvTables.Current"/>)에서 로비 값을 읽는다.</summary>
    public sealed class MvSaveLobbySource : ILobbySource
    {
        public static readonly MvSaveLobbySource Instance = new MvSaveLobbySource();

        public bool TryRead(out LobbyState s)
        {
            s = default;
            MvSaveStore store = MvSave.Current;
            if (store == null || store.IsDisposed) return false;
            s.Level = store.Progress.Level;
            s.AreaNo = store.Progress.Area + 1;
            s.Gold = store.Wealth.Get("gold");   // 01_wealth.type
            s.Souls = store.Wealth.Get("rescueCat");
            s.Dia = store.Wealth.Get("dia");
            s.AreaName = "";
            var tables = MvTables.Current;
            if (tables != null)
                foreach (var a in tables.Area.Datas)
                    if (a.areaNo == s.AreaNo) { s.NeedSouls = a.needSouls; s.AreaName = MvUiText.Name(a.id, ""); break; }
            return true;
        }

        public int FacilityStage(string key, out int extra) => LobbyFacilities.Stage(MvMetaUiPort.Instance, key, out extra);
    }
}
