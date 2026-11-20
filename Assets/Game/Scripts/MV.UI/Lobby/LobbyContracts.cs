using System;
using jjevol;
using MV.Data;
using MV.Save;

namespace MV.UI
{
    /// <summary>로비가 읽는 영속 값(05 §5.4.2 1차: Level·Area·재화).</summary>
    public struct LobbyState
    {
        /// <summary>로비 Level = Area를 깨지 못한 정산 수(<c>mvProgress.fails</c>).</summary>
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
        /// <summary>현재 Area의 <c>302_area.id</c>(이름·그림 조회). 없으면 0.</summary>
        public int AreaId;
        /// <summary>1기반 Zone 번호(<c>301_zone</c> 순서).</summary>
        public int ZoneNo;
        /// <summary>시작 덱(<c>mvLobby.deck</c>, 등급별 장수). 읽기 전용으로 쓴다. 없으면 null.</summary>
        public int[] Deck;
    }

    public interface ILobbySource
    {
        bool TryRead(out LobbyState state);
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

    /// <summary>
    /// 원값을 직접 그리는 겉(디자이너 LobbyScreen 연결 <see cref="LobbyDesignView"/>). 컨트롤러가 <see cref="ILobbyView"/> 문자열과 함께 넘긴다.
    /// </summary>
    public interface ILobbyStateView
    {
        void SetState(in LobbyState state, MvTables tables);
    }

    /// <summary>시작 덱 연출을 그리는 겉(MV-UI3-LOBBY-WEAPONFX). 컨트롤러가 구매 결과를 넘긴다.</summary>
    public interface ILobbyDeckView
    {
        /// <summary>스크래쳐 구매로 덱에 <paramref name="tier"/> 등급 카드 1장이 들어갔다(머지·버림은 메타가 이미 저장).</summary>
        void AddDeckCard(int tier);
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
            s.Level = store.Progress.Fails; // 로비 Level = 실패 횟수(사용자 2026-11-19, 해금 조건 Level과 같은 값)
            s.AreaNo = store.Progress.Area + 1;
            s.Gold = store.Wealth.Get("gold");   // 01_wealth.type
            s.Souls = store.Wealth.Get("rescueCat");
            s.Dia = store.Wealth.Get("dia");
            s.AreaName = "";
            var deck = store.Lobby.Deck;
            s.Deck = new int[deck.Count];
            for (int i = 0; i < deck.Count; i++) s.Deck[i] = deck[i];
            var tables = MvTables.Current;
            if (tables != null)
            {
                s.Deck = MV.Meta.MvMetaRules.CleanDeck(tables, deck, store.Lobby.GetUp(MV.Meta.MvMetaRules.LobbyWeapon));
                foreach (var a in tables.Area.Datas)
                    if (a.areaNo == s.AreaNo) { s.NeedSouls = a.needSouls; s.AreaId = a.id; s.AreaName = MvUiText.Name(a.id, ""); break; }
                s.ZoneNo = MV.Meta.MvMetaRules.ZoneIndex(tables, store.Progress.Area) + 1;
            }
            return true;
        }
    }
}
