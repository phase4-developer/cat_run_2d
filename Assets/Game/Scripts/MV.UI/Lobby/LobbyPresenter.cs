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
        /// <summary>퀘스트 사이드 버튼 ID·해금 키(MV-META-QUEST, <c>05_contentOpen 5001005</c>). 열기는 <see cref="MvUiNav.OpenQuestAsync"/>.</summary>
        public const string QuestSideId = "quest";
        public const string QuestUnlockKey = "quest";

        IQuestUiPort _questPort;
        QuestNotifications _questNotifications;
        /// <summary>퀘스트 빨간 점 입구. null이면 <see cref="MvQuestUiPort.Instance"/>(저장소 컨테이너 읽기).</summary>
        public IQuestUiPort QuestPort => _questPort ?? MvQuestUiPort.Instance;
        public void SetQuestPort(IQuestUiPort port) => _questPort = port;

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
        /// <summary>스크래쳐(<see cref="LobbyEffectType.DeckAdd"/>) 구매로 덱에 이 등급 카드 1장이 들어갔다(05 §5.5.1 「무기 그림이 버튼에서 덱으로」, 노션 Spwan → Dst → 줄).</summary>
        public event Action<int> DeckCardAdded;

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
            BuildRebirth(t, has ? s.Level : 0);
            if (t != null) BuildUpgrades(t, ready);
            if (t != null) Model.AreaUpgradeDot = ready && AnyAreaUpgrade(t);
            if (t != null) BuildStair(t);
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
            var t = Port.Tables ?? MvTables.Current;
            // {0} = 다음 Area 이름(언어팩 name:<302 id>), {1} = 생선 재화 이름(언어팩 name:<01_wealth id>)
            Model.RoadLeft = MvUiText.Ui("ui.lobby.roadNext", t?.AreaName(s.AreaNo + 1) ?? "",
                MvUiText.WealthName(t, MvWealthIds.TypeOf(MvWealthIds.Souls)));
            Model.RoadRight = MvUiFormat.Fmt(s.Souls) + " / " + MvUiFormat.Fmt(s.NeedSouls);
            Model.RoadFill = s.NeedSouls > 0 ? (float)Math.Min(s.Souls, s.NeedSouls) / s.NeedSouls : 1f;
        }

        // ───────── 사이드·시스템 버튼 ─────────

        void BuildSide(MvTables t, bool ready, int areaNo)
        {
            Add(LobbySideIds.Map, MvUiText.Ui("ui.lobby.map"), t?.AreaName(areaNo) ?? "", ready);

            string godKey = Port.SelectedGod;
            var god = t != null && godKey != null ? t.God.ByKey(godKey) : null;
            Add(LobbySideIds.God, god != null ? god.name : MvUiText.Ui("ui.lobby.godDefault"), MvUiText.Ui("ui.lobby.godSub"), ready); // name:<113_god id>

            Add(LobbySideIds.AreaUpgrade, MvUiText.Ui("ui.lobby.aup"),
                MvUiText.WealthName(t, MvWealthIds.TypeOf(MvWealthIds.Dia)) + " " + MvUiFormat.Fmt(Port.Wealth(MvWealthIds.Dia)), ready);

            var rb = Add(LobbySideIds.Rebirth, MvUiText.Ui("ui.lobby.rb"), "", ready);
            rb.UnlockKey = RebirthUnlockKey;
            rb.OpensWhenLocked = true; // 노션 [팝업] 환생 업그레이드: 미해금이면 팝업에서 Lock·해금 진행
            ApplyLock(rb, t);
            long cool = Port.RebirthCooldownLeftMs;
            rb.Sub = rb.Locked ? LockSub(t, rb.UnlockKey)
                : cool > 0 ? MetaPanelPresenter.Hms(cool)
                : MvUiText.Ui("ui.lobby.rbPending", MvUiText.WealthName(t, MvWealthIds.TypeOf(MvWealthIds.RbToken)), Port.RebirthPending);

            var content = Add(LobbySideIds.Content, MvUiText.Ui("ui.content.title"), "", ready);
            content.UnlockKey = ContentUnlockKey;
            ApplyLock(content, t);
            content.Sub = content.Locked ? LockSub(t, content.UnlockKey) : MvUiText.Ui("ui.lobby.contentSub", OpenModes(t));

            var quest = Add(QuestSideId, MvUiText.Ui("ui.quest.title"), "", ready);
            quest.UnlockKey = QuestUnlockKey;
            ApplyLock(quest, t);
            if (quest.Locked) quest.Sub = MvUiText.Ui("ui.lobby.locked"); // 해금 조건은 05_contentOpen[quest]
            if (!quest.Locked && QuestPort.Ready)
            {
                _questNotifications ??= new QuestNotifications();
                _questNotifications.Refresh(QuestPort.Groups());
                quest.Dot = _questNotifications.Any;
            }

            Add(LobbySideIds.Sound, "♪", Muted ? MvUiText.Ui("ui.lobby.soundOff") : "", true);
            Add(LobbySideIds.Debug, MvUiText.Ui("ui.debug.btn"), "", true).Visible = DevBuild;
        }

        /// <summary>부활 버튼 값(디자이너 Button Rebirth). 잠김·숨김은 사이드 버튼 판정(<see cref="ApplyLock"/>)과 같다.</summary>
        void BuildRebirth(MvTables t, int level)
        {
            var b = Model.FindSide(LobbySideIds.Rebirth);
            var r = Model.Rebirth;
            r.Visible = b != null && b.Visible;
            r.Locked = b != null && b.Locked;
            r.UnlockNow = level;
            r.UnlockNeed = LevelNeed(t?.ContentOpen.ByKey(RebirthUnlockKey));
            r.CoolLeftMs = Math.Max(0, Port.RebirthCooldownLeftMs);
            var cfg = t?.Config.Value;
            r.CoolTotalMs = cfg != null ? Math.Max(0, (long)cfg.rebirthCooldownSec * 1000L) : 0;
            r.Pending = Port.RebirthPending;
            r.UpgradeAvailable = r.Visible && Port.Ready && t != null &&
                t.RebirthPerk.Sorted.Any(row => row.active && Port.CanBuy(MvGrowth.Perk, row.key));
        }

        /// <summary>사용 조건 중 Level 값(07 §7.8.9 조건 3). 없으면 0.</summary>
        static int LevelNeed(ContentOpenData row)
        {
            if (row?.openConditions == null) return 0;
            foreach (var c in row.openConditions)
                if (c.type == UnlockConditionType.Level) return c.value;
            return 0;
        }

        void BuildStair(MvTables t)
        {
            var k = MvMetaRules.StairStart(t, Port.Level(MvGrowth.Lobby, MvMetaRules.LobbyStair));
            Model.StairLv = k.Lv;
            Model.StairStep = k.Step;
            Model.StairNeed = k.Need;
        }

        bool AnyAreaUpgrade(MvTables t)
        {
            foreach (var row in t.AreaUpgrade.Sorted)
                if (row.active && !Port.IsMax(MvGrowth.AreaUp, row.key) && Port.CanBuy(MvGrowth.AreaUp, row.key)) return true;
            return false;
        }

        LobbySideButton Add(string id, string title, string sub, bool enabled)
        {
            var b = new LobbySideButton(id) { Title = title, Sub = sub, Enabled = enabled };
            Model.Side.Add(b);
            return b;
        }

        /// <summary>
        /// 해금 단계(<see cref="MvUnlockRules"/>): 행이 꺼져 있거나 잠금 표시 조건 전이면 숨김, 아직 사용 가능 전이면 잠김 표시(SoftLock).
        /// </summary>
        void ApplyLock(LobbySideButton b, MvTables t)
        {
            var row = t?.ContentOpen.ByKey(b.UnlockKey);
            if (row == null) return;
            if (!row.active) { b.Visible = false; return; }
            var state = Port.UnlockState(b.UnlockKey);
            if (Port.Ready && state == MvUnlockState.Hidden) { b.Visible = false; return; }
            b.Locked = state != MvUnlockState.Open;
        }

        string LockSub(MvTables t, string key)
        {
            string need = MvUnlockText.Need(t?.ContentOpen.ByKey(key), Port.UnlockState(key));
            return need.Length > 0 ? MvUiText.Ui("ui.lobby.unlockAt", need) : MvUiText.Ui("ui.lobby.locked");
        }

        int OpenModes(MvTables t)
        {
            if (t == null) return 0;
            int n = 0;
            foreach (var row in t.ContentOpen.Sorted)
                if (row.active && row.contentId != 0 && Port.IsOpen(row.key)) n++;
            return n;
        }

        /// <summary>사이드·시스템 버튼 1번. 숨김·꺼짐이면 무시, 잠김이면 토스트만(<see cref="LobbySideButton.OpensWhenLocked"/>는 연다). 화면을 열어 달라고 했으면 true.</summary>
        public bool PressSide(string id)
        {
            var b = Model.FindSide(id);
            if (b == null || !b.Visible || !b.Enabled) return false;
            if (b.Locked && !b.OpensWhenLocked)
            {
                Say(LockedToast(Port.Tables, b));
                return false;
            }
            SideRequested?.Invoke(id);
            return true;
        }

        string LockedToast(MvTables t, LobbySideButton b)
        {
            string need = MvUnlockText.Need(t?.ContentOpen.ByKey(b.UnlockKey), Port.UnlockState(b.UnlockKey));
            if (need.Length == 0) return MvUiText.Toast("toastMessage.locked");
            return MvUiText.Toast("toastMessage.lockedAt", need);
        }

        // ───────── 강화 버튼 3종(05 §5.5.1) ─────────

        void BuildUpgrades(MvTables t, bool ready)
        {
            foreach (var row in t.LobbyUpgrade.Sorted)
            {
                if (!row.active) continue;
                int p = Port.Level(MvGrowth.Lobby, row.key);
                bool max = Port.IsMax(MvGrowth.Lobby, row.key);
                bool canBuy = ready && !max && Port.CanBuy(MvGrowth.Lobby, row.key);
                var u = new LobbyUpgradeItem
                {
                    Key = row.key,
                    Title = row.name, // 언어팩 name:<10_lobbyUpgrade id>
                    Max = max,
                    Price = max ? "MAX" : MetaPanelPresenter.PriceText(Port.NextPrice(MvGrowth.Lobby, row.key), t), // ui.common.max
                    Enabled = canBuy,
                    InputAllowed = ready && !max,
                };
                if (!max) SetPrice(u, Port.NextPrice(MvGrowth.Lobby, row.key), t);
                Describe(u, row, t, p);
                Model.Upgrades.Add(u);
            }
        }

        /// <summary>비용과 첫 항목의 재화 아이콘(디자이너 Slot_Price). 재화가 아니면 아이콘 없음.</summary>
        static void SetPrice(LobbyUpgradeItem u, System.Collections.Generic.IReadOnlyList<ConsumeData> price, MvTables t)
        {
            if (price == null || price.Count == 0) return;
            u.Cost = price;
            var c = price[0];
            if (MvConsumeIcon.TryGet(t, (int)c.Type, c.Id, out var atlas, out var sprite)) { u.PriceIconAtlas = atlas; u.PriceIcon = sprite; }
        }

        /// <summary>레벨·설명·진행바(05 §5.5.1 두 번째 표, 정본 UPS.info L823~828). 효과 종류로 나눈다.</summary>
        static void Describe(LobbyUpgradeItem u, LobbyUpgradeData row, MvTables t, int p)
        {
            switch (row.effectType)
            {
                case LobbyEffectType.StartStairLv:
                {
                    var k = MvMetaRules.StairStart(t, p);
                    int maxLv = t.StairMaxLv();
                    u.Icon = string.IsNullOrEmpty(row.icon) ? "" : row.icon + "_" + StageOf(t, LobbyEffectType.StartStairLv, p);
                    u.LevelNo = Math.Min(k.Lv, maxLv);
                    u.CurValue = u.LevelNo.ToString(Inv);
                    u.NextValue = Math.Min(u.LevelNo + 1, maxLv).ToString(Inv); // 계단 게이트 1칸 +N(ui.up.stairDesc)
                    if (k.Lv >= maxLv)
                    {
                        u.LevelText = MvUiText.Ui("ui.up.stairLv", maxLv);
                        u.Desc = MvUiText.Ui("ui.up.stairMax");
                        u.Progress = 1f;
                        u.ProgressText = "MAX";
                    }
                    else
                    {
                        u.LevelText = MvUiText.Ui("ui.up.stairLv", k.Lv);
                        u.Desc = MvUiText.Ui("ui.up.stairDesc", k.Lv + 1);
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
                    u.IconIsItem = true;
                    u.LevelNo = tier;
                    u.LevelText = MvUiText.Ui("ui.up.weaponLv", tier);
                    u.Desc = last ? MvUiText.Ui("ui.up.weaponDescLast", WeaponName(t, tier), WeaponName(t, tier + 1))
                                  : MvUiText.Ui("ui.up.weaponDesc", WeaponName(t, tier));
                    u.Progress = tier >= top ? 1f : w.Need > 0 ? (float)w.Step / w.Need : 0f;
                    u.ProgressText = tier >= top ? w.Step.ToString(Inv) : w.Step + "/" + w.Need;
                    // 디자이너 칸 Slot_Count_Weapon(현재값 / 다음값) = 이번 Level에서 산 수 / 12_weaponLevel.need(사용자 2026-11-19 「8 9 11 10 10 10 일텐데 어떻게 1/3 2/3 이라는 수치가」).
                    u.CurValue = w.Step.ToString(Inv);
                    u.NextValue = w.Need.ToString(Inv);
                    break;
                }
                default:
                {
                    u.Icon = string.IsNullOrEmpty(row.icon) ? "" : row.icon + "_" + StageOf(t, LobbyEffectType.GoldMul, p);
                    u.LevelNo = p + 1;
                    u.CurValue = MvMetaRules.GoldMul(t, p).ToString("0.##", Inv);
                    u.NextValue = MvMetaRules.GoldMul(t, p + 1).ToString("0.##", Inv);
                    u.LevelText = MvUiText.Ui("ui.up.incomeLv", p + 1);
                    u.Desc = MvUiText.Ui("ui.up.incomeDesc", MvUiText.WealthName(t, MvWealthIds.TypeOf(MvWealthIds.Gold)),
                        MvMetaRules.GoldMul(t, p).ToString("0.00", Inv), MvMetaRules.GoldMul(t, p + 1).ToString("0.00", Inv));
                    u.Progress = -1f; // 진행바 없음
                    break;
                }
            }
        }

        /// <summary>아이콘 뒤 아지트 단계(정본 facIconURL(kind, azStages())).</summary>
        static int StageOf(MvTables t, LobbyEffectType effect, int p)
        {
            // 문턱은 LobbyFacilities 한 곳(PC-19 정리 대기).
            if (effect == LobbyEffectType.StartStairLv) return LobbyFacilities.TowerStage(MvMetaRules.StairStart(t, p).Lv);
            return LobbyFacilities.RackStage(p);
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
            return item != null ? item.name : MvUiText.Ui("ui.up.weaponFallback", tier); // 언어팩 name:<item id>
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
            int after = StepLevel(t, row, Port.Level(MvGrowth.Lobby, key));
            if (r.HasLobbyBuy) { before = r.LvBefore; after = r.LvAfter; } // 메타 구매 결과(MvLobbyBuyResult)의 Lv.
            // 덱에 넣은 카드(메타 AddedTier, 없으면 사기 전 무기 Level = 등급). 머지·낮은 카드 버림은 메타가 이미 저장했고 연출은 겉이 손패 모델로 다시 보인다.
            if (row.effectType == LobbyEffectType.DeckAdd)
                DeckCardAdded?.Invoke(r.HasLobbyBuy && r.AddedTier > 0 ? r.AddedTier : before);
            // 배너 자리(OverlayHud.ShowBanner는 2차 연출). 1차는 토스트로 알린다.
            if (after > before)
                Say(MvUiText.Ui(row.effectType == LobbyEffectType.StartStairLv ? "ui.banner.stairUp" : "ui.banner.weaponUp", row.name, after));
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
