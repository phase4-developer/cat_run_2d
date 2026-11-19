using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.UI
{
    /// <summary>로비 사이드·시스템 버튼 ID(05 §5.4.2·§5.14·§5.15.1). 눌림은 <see cref="ILobbyMenuView.SidePressed"/>로 이 값을 넘긴다.</summary>
    public static class LobbySideIds
    {
        /// <summary>야옹신 <c>#godBtn</c> → §5.7.</summary>
        public const string God = "god";
        /// <summary>장난감 바구니 <c>#aupBtn</c> → Area 강화 §5.8.</summary>
        public const string AreaUpgrade = "aup";
        /// <summary>아홉 목숨 <c>#rbBtn</c> → 부활 §5.9.</summary>
        public const string Rebirth = "rebirth";
        /// <summary>지도 <c>#mapBtn</c> → §5.10.</summary>
        public const string Map = "map";
        /// <summary>컨텐츠 버튼(§5.15.1). 메뉴는 MV-UI-CONTENT.</summary>
        public const string Content = "content";
        /// <summary>소리 버튼 <c>#sndBtn</c>(§5.14, 설정 자리).</summary>
        public const string Sound = "sound";
        /// <summary>테스트 버튼 <c>#dbgBtn</c>(§5.14, 개발 빌드 전용).</summary>
        public const string Debug = "debug";
    }

    /// <summary>사이드·시스템 버튼 1개.</summary>
    public sealed class LobbySideButton
    {
        public string Id;
        public string Title = "";
        /// <summary>부제(1초마다 다시 그림, 05 §5.4.2).</summary>
        public string Sub = "";
        /// <summary>false면 겉이 숨긴다(해금 행 꺼짐·개발 빌드 아님).</summary>
        public bool Visible = true;
        /// <summary>잠김 모양(SoftLock: 보이고, 누르면 토스트).</summary>
        public bool Locked;
        /// <summary>false면 눌러도 무시(메타 없음).</summary>
        public bool Enabled = true;
        /// <summary>해금 키(<c>05_contentOpen.key</c>). 없으면 null.</summary>
        public string UnlockKey;
        /// <summary>빨간 점(받을 것 있음, 05 §5.16 탭 점과 같은 뜻).</summary>
        public bool Dot;

        public LobbySideButton(string id) { Id = id; }
    }

    /// <summary>강화 버튼 1개(05 §5.5.1 <c>UpgradeButton</c>). <c>10_lobbyUpgrade</c> 행 1개 = 1개.</summary>
    public sealed class LobbyUpgradeItem
    {
        /// <summary><c>10_lobbyUpgrade.key</c>.</summary>
        public string Key;
        public string Title = "";
        /// <summary>아이콘 스프라이트 키(1차 더미 겉은 그리지 않는다).</summary>
        public string Icon = "";
        /// <summary>레벨 줄 <c>.l</c>.</summary>
        public string LevelText = "";
        /// <summary>설명 <c>.e</c>.</summary>
        public string Desc = "";
        /// <summary>진행바 비율 0~1. 진행바가 없으면(생선 건조대) 음수.</summary>
        public float Progress = -1f;
        /// <summary>진행바 가운데 글.</summary>
        public string ProgressText = "";
        /// <summary>가격 줄(<c>PriceButton</c>) 또는 「MAX」.</summary>
        public string Price = "";
        public bool Max;
        /// <summary>꺼짐: 최대·비용 부족·메타 없음(05 §5.5.1 상태 표).</summary>
        public bool Enabled;
        /// <summary>
        /// 가격 버튼 입력 허용(메타 준비됨·최대 아님). 잔액 확인은 Framework <c>PriceBtn</c>이 재화 서비스로 직접 하고,
        /// 모자라면 가격 글자를 빨갛게·버튼을 잠근다(사용자 2026-11-19 「Framework PriceBtn 사용」).
        /// </summary>
        public bool InputAllowed;
        /// <summary>단계 숫자(캣타워 = 계단 Lv, 무기 = 무기 등급, 건조대 = 산 단계 + 1). 디자이너 강화 칸 「Level {0}」.</summary>
        public int LevelNo;
        /// <summary>디자이너 강화 칸 <c>Slot_Count</c> 지금 값·다음 값(캣타워 = 칸당 계단, 건조대 = 츄르 배수). 없으면 빈 문자열.</summary>
        public string CurValue = "";
        public string NextValue = "";
        /// <summary>다음 단계 비용(<c>16_levelPrice</c> consumeData). 최대면 null. 글자·부족 색은 <c>PriceBtn</c>이 그린다.</summary>
        public IReadOnlyList<ConsumeData> Cost;
        /// <summary>가격 재화 아이콘(<c>01_wealth.icon</c>, Atlas_UI_Icon). 최대면 빈 문자열.</summary>
        public string PriceIcon = "";
        /// <summary>
        /// <see cref="Icon"/>이 103_item 그림(Atlas_UI_Icon)인지. 무기 강화만 true(노션: 레벨업하면 다음 등급 무기 그림).
        /// 나머지는 옛 아지트 시설 그림 키라 디자이너 칸에 넣지 않는다.
        /// </summary>
        public bool IconIsItem;

        public bool HasProgress => Progress >= 0f;
    }

    /// <summary>부활(냥환생) 버튼 값(디자이너 <c>Button Rebirth</c>: 해금 진행·쿨타임·대기 조각).</summary>
    public sealed class LobbyRebirthInfo
    {
        /// <summary>false면 숨김(해금 행 꺼짐 또는 잠금 표시 조건 전).</summary>
        public bool Visible;
        /// <summary>사용 조건 전(잠김 진행 <c>Progress_UnLock</c>을 보인다).</summary>
        public bool Locked;
        /// <summary>해금 진행: 지금 Level(실패 횟수)·필요 Level(<c>05_contentOpen</c> 사용 조건 Level 값). 조건에 Level이 없으면 Need = 0.</summary>
        public int UnlockNow;
        public int UnlockNeed;
        /// <summary>쿨타임 남은 ms·전체 ms(<c>00_config.rebirthCooldownSec</c>).</summary>
        public long CoolLeftMs;
        public long CoolTotalMs;
        /// <summary>부활하면 받을 조각(<c>save.rb.pending</c>).</summary>
        public int Pending;
    }

    /// <summary>로비 화면 코드(<see cref="LobbyPresenter"/>)가 만들고 겉이 그리는 값(재화·Level·Area 외).</summary>
    public sealed class LobbyMenuModel
    {
        /// <summary>진행 상자 왼쪽 글(<c>ui.lobby.roadNext</c>).</summary>
        public string RoadLeft = "";
        /// <summary>진행 상자 오른쪽 「{0} / {1}」.</summary>
        public string RoadRight = "";
        public float RoadFill;
        public readonly List<LobbySideButton> Side = new List<LobbySideButton>();
        public readonly List<LobbyUpgradeItem> Upgrades = new List<LobbyUpgradeItem>();
        public readonly LobbyRebirthInfo Rebirth = new LobbyRebirthInfo();
        /// <summary>Area 강화(<c>13_areaUpgrade</c>)에 지금 살 수 있는 행이 있다(디자이너 <c>Button AreaUpgrade/RedDot</c>).</summary>
        public bool AreaUpgradeDot;
        /// <summary>
        /// 런 시작 계단 등급(캣타워 강화 → <c>11_stairLevel</c>, 런의 게이지 시작값과 같은 계산 <see cref="MV.Meta.MvMetaRules.StairStart"/>).
        /// 디자이너 Mid/Stair. 표가 없으면 Lv 0.
        /// </summary>
        public int StairLv, StairStep, StairNeed;

        public void Clear()
        {
            RoadLeft = RoadRight = "";
            RoadFill = 0;
            Side.Clear();
            Upgrades.Clear();
            var r = Rebirth;
            r.Visible = r.Locked = false;
            r.UnlockNow = r.UnlockNeed = r.Pending = 0;
            r.CoolLeftMs = r.CoolTotalMs = 0;
            AreaUpgradeDot = false;
            StairLv = StairStep = StairNeed = 0;
        }

        public LobbySideButton FindSide(string id)
        {
            foreach (var b in Side) if (b.Id == id) return b;
            return null;
        }

        public LobbyUpgradeItem FindUpgrade(string key)
        {
            foreach (var u in Upgrades) if (u.Key == key) return u;
            return null;
        }
    }

    /// <summary>
    /// 로비 겉의 사이드 버튼·강화 버튼·진행 상자 부분(D-92). 기존 <see cref="ILobbyView"/>(Level·Area·재화·출발)와 나눠
    /// 기존 겉 구현을 깨지 않는다. 겉 <see cref="LobbyDesignView"/>(디자이너 LobbyScreen 연결)는 둘 다 구현한다.
    /// </summary>
    public interface ILobbyMenuView
    {
        /// <summary>사이드·시스템 버튼(<see cref="LobbySideIds"/>).</summary>
        event Action<string> SidePressed;
        /// <summary>강화 버튼(<see cref="LobbyUpgradeItem.Key"/>).</summary>
        event Action<string> UpgradePressed;
        void Render(LobbyMenuModel model);
    }
}
