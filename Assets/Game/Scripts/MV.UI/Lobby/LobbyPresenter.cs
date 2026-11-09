using System;
using System.Globalization;
using System.Linq;
using MV.Data;
using MV.Meta;
using MV.Save;

namespace MV.UI
{
    /// <summary>
    /// 로비 화면 코드(05 §5.4·§5.5.1·§5.14 버튼, D-92 ③⑤). Framework 화면 타입과 떨어진 순수 클래스.
    /// 사이드 버튼·강화 버튼·진행 상자를 표(<c>10_lobbyUpgrade</c>·<c>05_contentOpen</c>)와 메타 입구에서 만든다.
    /// 규칙: 메타가 없으면(<see cref="IMetaUiPort.Ready"/> false) 강화와 메타 화면 버튼을 막는다. 강화 1번 = 메타 구매 1번.
    /// 덱 머지·lvup/tick·배너 연출은 2차(MV-UI-FX2).
    /// </summary>
    public sealed class LobbyPresenter
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>부활 버튼 해금 키(<c>05_contentOpen</c>).</summary>
        public const string RebirthUnlockKey = MvMetaRules.RebirthKey;
        /// <summary>컨텐츠 버튼 해금 키(<c>05_contentOpen 5001003</c>).</summary>
        public const string ContentUnlockKey = "contentMenu";

        IMetaUiPort _port;
        ILobbySource _source;
        float _subTimer;

        public IMetaUiPort Port => _port ?? MvMetaUiPort.Instance;
        public ILobbySource Source => _source ?? MvSaveLobbySource.Instance;
        public ILobbyMenuView View { get; private set; }
        public LobbyMenuModel Model { get; } = new LobbyMenuModel();

        /// <summary>테스트 버튼을 보일지(개발 빌드, 05 §5.14). 컨트롤러가 <c>Debug.isDebugBuild</c>로 정한다.</summary>
        public bool DevBuild { get; set; }
        /// <summary>소리 꺼짐 표시. 컨트롤러가 소리 모듈 값으로 맞춘다.</summary>
        public bool Muted { get; set; }

        /// <summary>사이드·시스템 버튼이 화면을 열거나 설정을 바꿔 달라(<see cref="LobbySideIds"/>). 컨트롤러가 처리한다.</summary>
        public event Action<string> SideRequested;
        public event Action<string> Toast;
        /// <summary>강화 구매가 성공했다(소리 buy, 정본 L2644).</summary>
        public event Action Bought;
        /// <summary>시설 반짝임(<see cref="LobbyFacilities"/> key).</summary>
        public event Action<string> FacilityBump;

        public void SetPort(IMetaUiPort port) => _port = port;
        public void SetSource(ILobbySource source) => _source = source;

        public void SetView(ILobbyMenuView view)
        {
            View = view;
            Draw();
        }

        public void Draw()
        {
            Model.Clear();
            var t = Port.Tables;
            bool ready = Port.Ready;
            bool has = Source.TryRead(out var s);
            BuildRoad(has, s);
            BuildSide(t, ready, has ? s.AreaNo : Port.AreaIndex + 1);
            if (t != null) BuildUpgrades(t, ready);
            View?.Render(Model);
        }

        /// <summary>1초마다 부제를 다시 그린다(정본 L2565: 부활 쿨타임 등).</summary>
        public void Tick(float unscaledDt)
        {
            _subTimer += unscaledDt;
            if (_subTimer < 1f) return;
            _subTimer = 0;
            Draw();
        }

        // ───────── 진행 상자 ─────────

        void BuildRoad(bool has, LobbyState s)
        {
            if (!has) return;
            Model.RoadLeft = "Area " + (s.AreaNo + 1) + " 개방: Area 포털 도달 + 누적 생선"; // ui.lobby.roadNext
            Model.RoadRight = MvUiFormat.Fmt(s.Souls) + " / " + MvUiFormat.Fmt(s.NeedSouls);
            Model.RoadFill = s.NeedSouls > 0 ? (float)Math.Min(s.Souls, s.NeedSouls) / s.NeedSouls : 1f;
        }

        // ───────── 사이드·시스템 버튼 ─────────

        void BuildSide(MvTables t, bool ready, int areaNo)
        {
            Add(LobbySideIds.Map, "지도", "Area " + areaNo, ready); // ui.lobby.map / ui.lobby.mapSub

            string godKey = Port.SelectedGod;
            var god = t != null && godKey != null ? t.God.ByKey(godKey) : null;
            Add(LobbySideIds.God, god != null ? MvUiText.Name(god.id, god.key) : "야옹신", "야옹신 선택", ready); // name:<113_god id> / ui.lobby.godSub

            Add(LobbySideIds.AreaUpgrade, "장난감 바구니", "캣닢 " + MvUiFormat.Fmt(Port.Wealth(MvWealthIds.Dia)), ready); // ui.lobby.aup

            var rb = Add(LobbySideIds.Rebirth, "아홉 목숨", "", ready); // ui.lobby.rb
            rb.UnlockKey = RebirthUnlockKey;
            ApplyLock(rb, t);
            long cool = Port.RebirthCooldownLeftMs;
            rb.Sub = rb.Locked ? LockSub(t, rb.UnlockKey)                                      // ui.lobby.rbLocked
                : cool > 0 ? MetaPanelPresenter.Hms(cool)
                : "조각 +" + Port.RebirthPending + " 대기";                                      // ui.lobby.rbPending

            var content = Add(LobbySideIds.Content, "컨텐츠", "", ready); // ui.lobby.content
            content.UnlockKey = ContentUnlockKey;
            ApplyLock(content, t);
            content.Sub = content.Locked ? LockSub(t, content.UnlockKey) : OpenModes(t) + "개 열림"; // ui.lobby.contentSub

            Add(LobbySideIds.Sound, "♪", Muted ? "꺼짐" : "", true);
            Add(LobbySideIds.Debug, "테스트", "", true).Visible = DevBuild; // ui.debug.btn
        }

        LobbySideButton Add(string id, string title, string sub, bool enabled)
        {
            var b = new LobbySideButton(id) { Title = title, Sub = sub, Enabled = enabled };
            Model.Side.Add(b);
            return b;
        }

        /// <summary>해금 행이 꺼져 있으면 숨김, 아직 안 열렸으면 잠김 표시(SoftLock).</summary>
        void ApplyLock(LobbySideButton b, MvTables t)
        {
            var row = t?.ContentOpen.ByKey(b.UnlockKey);
            if (row == null) return;
            if (!row.active) { b.Visible = false; return; }
            b.Locked = !Port.IsOpen(b.UnlockKey);
        }

        static string LockSub(MvTables t, string key)
        {
            var row = t?.ContentOpen.ByKey(key);
            return row != null && row.conditionType == 1 ? "Area " + row.conditionValue + " 해금" : "잠김";
        }

        int OpenModes(MvTables t)
        {
            if (t == null) return 0;
            int n = 0;
            foreach (var row in t.ContentOpen.Sorted)
                if (row.active && row.contentId != 0 && Port.IsOpen(row.key)) n++;
            return n;
        }

        /// <summary>사이드·시스템 버튼 1번. 숨김·꺼짐이면 무시, 잠김이면 토스트만. 화면을 열어 달라고 했으면 true.</summary>
        public bool PressSide(string id)
        {
            var b = Model.FindSide(id);
            if (b == null || !b.Visible || !b.Enabled) return false;
            if (b.Locked)
            {
                Say(LockedToast(Port.Tables, b));
                return false;
            }
            SideRequested?.Invoke(id);
            return true;
        }

        static string LockedToast(MvTables t, LobbySideButton b)
        {
            var row = t?.ContentOpen.ByKey(b.UnlockKey);
            int area = row != null && row.conditionType == 1 ? row.conditionValue : 0;
            if (b.Id == LobbySideIds.Rebirth) return "부활은 Area " + area + "를 열면 해금됩니다"; // toastMessage:rbLocked
            return "Area " + area + "에서 열려요";                                              // toastMessage:contentLocked
        }

        // ───────── 강화 버튼 3종(05 §5.5.1) ─────────

        void BuildUpgrades(MvTables t, bool ready)
        {
            foreach (var row in t.LobbyUpgrade.Sorted)
            {
                if (!row.active) continue;
                int p = Port.Level(MvGrowth.Lobby, row.key);
                bool max = Port.IsMax(MvGrowth.Lobby, row.key);
                var u = new LobbyUpgradeItem
                {
                    Key = row.key,
                    Title = MvUiText.Name(row.id, FallbackName(row.key)),
                    Max = max,
                    Price = max ? "MAX" : MetaPanelPresenter.PriceText(Port.NextPrice(MvGrowth.Lobby, row.key), t), // ui.common.max
                    Enabled = ready && !max && Port.CanBuy(MvGrowth.Lobby, row.key),
                };
                Describe(u, row, t, p);
                Model.Upgrades.Add(u);
            }
        }

        static string FallbackName(string key)
        {
            switch (key)
            {
                case MvMetaRules.LobbyStair: return "캣타워";
                case MvMetaRules.LobbyWeapon: return "스크래쳐";
                case MvMetaRules.LobbyIncome: return "생선 건조대";
                default: return key;
            }
        }

        /// <summary>레벨·설명·진행바(05 §5.5.1 두 번째 표, 정본 UPS.info L823~828). 효과 종류로 나눈다.</summary>
        static void Describe(LobbyUpgradeItem u, LobbyUpgradeData row, MvTables t, int p)
        {
            switch (row.effectType)
            {
                case LobbyEffectType.StartStairLv:
                {
                    var k = MvMetaRules.StairStart(t, p);
                    int maxLv = t.RunConfig.I(RunConfigKeys.LOBBY_STAIR_MAX_LV);
                    u.Icon = string.IsNullOrEmpty(row.icon) ? "" : row.icon + "_" + StageOf(t, LobbyEffectType.StartStairLv, p);
                    if (k.Lv >= maxLv)
                    {
                        u.LevelText = "계단 Lv " + maxLv;          // ui.up.stairLv
                        u.Desc = "로비 최대 · 이후는 벽돌 우물로";   // ui.up.stairMax
                        u.Progress = 1f;
                        u.ProgressText = "MAX";
                    }
                    else
                    {
                        u.LevelText = "계단 Lv " + k.Lv;
                        u.Desc = "다 차면 Lv " + (k.Lv + 1) + " · 계단 게이트 1칸 +" + (k.Lv + 1); // ui.up.stairDesc
                        u.Progress = k.Need > 0 ? (float)k.Step / k.Need : 0f;
                        u.ProgressText = k.Step + "/" + k.Need;
                    }
                    break;
                }
                case LobbyEffectType.DeckAdd:
                {
                    var w = MvMetaRules.WeaponLevel(t, p);
                    int top = t.WeaponLevel.Datas.Count == 0 ? 1 : t.WeaponLevel.Datas.Max(r => r.lv);
                    int tier = w.Lv;
                    bool last = tier < top && w.Step == w.Need - 1;
                    u.Icon = WeaponIcon(t, tier);
                    u.LevelText = "Level " + tier; // ui.up.weaponLv
                    u.Desc = last ? WeaponName(t, tier) + " 카드 +1 · 다 차면 " + WeaponName(t, tier + 1) + " 카드로" // ui.up.weaponDescLast
                                  : WeaponName(t, tier) + " 카드 1장 추가";                                          // ui.up.weaponDesc
                    u.Progress = tier >= top ? 1f : w.Need > 0 ? (float)w.Step / w.Need : 0f;
                    u.ProgressText = tier >= top ? w.Step.ToString(Inv) : w.Step + "/" + w.Need;
                    break;
                }
                default:
                {
                    u.Icon = string.IsNullOrEmpty(row.icon) ? "" : row.icon + "_" + StageOf(t, LobbyEffectType.GoldMul, p);
                    u.LevelText = "Lv " + (p + 1); // ui.up.incomeLv
                    u.Desc = "츄르 ×" + MvMetaRules.GoldMul(t, p).ToString("0.00", Inv) + " → ×" + MvMetaRules.GoldMul(t, p + 1).ToString("0.00", Inv); // ui.up.incomeDesc
                    u.Progress = -1f; // 진행바 없음
                    break;
                }
            }
        }

        /// <summary>아이콘 뒤 아지트 단계(정본 facIconURL(kind, azStages())).</summary>
        static int StageOf(MvTables t, LobbyEffectType effect, int p)
        {
            if (effect == LobbyEffectType.StartStairLv)
            {
                int kl = MvMetaRules.StairStart(t, p).Lv;
                return kl >= 4 ? 3 : kl >= 2 ? 2 : 1;
            }
            return p >= 15 ? 3 : p >= 5 ? 2 : 1;
        }

        static ItemData WeaponItem(MvTables t, int tier)
        {
            foreach (var r in t.WeaponLevel.Datas)
                if (r.lv == tier) return t.Item.Get(r.itemId);
            return null;
        }

        static string WeaponName(MvTables t, int tier)
        {
            var item = WeaponItem(t, tier);
            return item != null ? MvUiText.Name(item.id, "무기 " + tier) : "무기 " + tier;
        }

        static string WeaponIcon(MvTables t, int tier) => WeaponItem(t, tier)?.icon ?? "";

        /// <summary>
        /// 강화 버튼 1번(05 §5.5.1 구매 순서 0초). 메타가 없거나 꺼진 버튼이면 무시. 메타 구매 명령은 한 번만 부른다. 샀으면 true.
        /// </summary>
        public bool PressUpgrade(string key)
        {
            if (!Port.Ready) return false;
            var u = Model.FindUpgrade(key);
            var t = Port.Tables;
            var row = t?.LobbyUpgrade.ByKey(key);
            if (u == null || !u.Enabled || row == null) return false;
            int before = StepLevel(t, row, Port.Level(MvGrowth.Lobby, key));
            var r = Port.Buy(MvGrowth.Lobby, key);
            if (!r.Ok)
            {
                Say(r.Code);
                Draw();
                return false;
            }
            Bought?.Invoke();
            FacilityBump?.Invoke(LobbyFacilities.OfEffect(row.effectType));
            int after = StepLevel(t, row, Port.Level(MvGrowth.Lobby, key));
            // 배너 자리(OverlayHud.ShowBanner는 2차 연출). 1차는 토스트로 알린다.
            if (after > before)
                Say(row.effectType == LobbyEffectType.StartStairLv ? "캣타워 Lv " + after   // ui.banner.stairUp
                                                                   : "무기 Level " + after); // ui.banner.weaponUp
            Draw();
            return true;
        }

        static int StepLevel(MvTables t, LobbyUpgradeData row, int p)
        {
            switch (row.effectType)
            {
                case LobbyEffectType.StartStairLv: return MvMetaRules.StairStart(t, p).Lv;
                case LobbyEffectType.DeckAdd: return MvMetaRules.WeaponLevel(t, p).Lv;
                default: return 0;
            }
        }

        void Say(string text)
        {
            if (!string.IsNullOrEmpty(text)) Toast?.Invoke(text);
        }
    }
}
