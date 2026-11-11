using System;
using System.Collections.Generic;
using System.Globalization;
using MV.Data;
using MV.Meta;

namespace MV.UI
{
    /// <summary>
    /// 로비 「컨텐츠」 메뉴 화면 코드(05 §5.15, D-92 ③). 메타 공통 틀(<see cref="MetaPanelPresenter"/>)을 그대로 쓴다.
    /// 「모드」 탭: <c>400_content</c>에서 <c>active 1</c>·<c>contentType ≥ 1</c> 행마다 카드 1장(sort 순, 행 추가 = 카드 추가).
    /// 「성장」 탭: <c>05_contentOpen</c>에서 <c>contentId 0</c>·<c>active 1</c>인 메타 시스템 행(<see cref="IsMetaSystemKey"/>).
    /// 잠금은 <c>05_contentOpen</c>(카드의 <c>openId</c>). 도전: 잠김·하루 한도는 토스트로 거절, 통과하면 메타 <c>RecordModePlay</c> 1번 → 시작 콜백.
    /// 1차 범위 밖(연출 2차·메타 API 없음): 입장권 차감·소탕·광고 +1·비용 확인(<c>ContentEntry</c>, 05 §5.15.4).
    /// </summary>
    public sealed class ContentMenuPresenter : MetaPanelPresenter
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public const string TabMode = "mode";
        public const string TabMeta = "meta";
        /// <summary>모드 카드 행·행동 id 앞머리(뒤는 <c>400_content.key</c>).</summary>
        public const string GoPrefix = "go:";
        /// <summary>성장 탭 행·행동 id 앞머리(뒤는 <c>05_contentOpen.key</c>).</summary>
        public const string MetaPrefix = "meta:";
        /// <summary>광고 충전 줄 자리(<c>08_adPlacement</c>).</summary>
        public const string AdTicketKey = "modeTicket";

        /// <summary>
        /// 「성장」 탭에 그릴 <c>05_contentOpen.key</c>(05 §5.15.5: 1차 퀘스트·출석·연구·상점·업적, 2차 펫·친구·장비·패스·축복).
        /// 표가 메타 시스템 행을 구분하는 열을 갖지 않아 key로 고른다(같은 key의 행 추가·잠금·active는 표가 정한다).
        /// </summary>
        static readonly HashSet<string> MetaSystemKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "quest", "attendance", "research", "shop", "achievement", "pet", "friend", "gear", "pass", "blessing",
        };

        public static bool IsMetaSystemKey(string key) => key != null && MetaSystemKeys.Contains(key);

        IContentMenuPort _content;
        string _tab = TabMode;
        readonly HashSet<string> _shownNew = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>모드 시작(<see cref="ContentMenuData.OnStartMode"/>). null이면 「준비 중」 토스트만 내고 메타를 부르지 않는다.</summary>
        public Action<string> StartMode;
        /// <summary>성장 탭 시스템 화면 열기(<c>05_contentOpen.key</c>). null이면 「준비 중」 토스트.</summary>
        public Action<string> OpenMeta;

        public ContentMenuPresenter()
        {
            CloseRequested += SaveSeen; // 컨트롤러의 닫기보다 먼저 붙는다
        }

        /// <summary>null이면 <see cref="MetaPanelPresenter.Port"/>가 이 인터페이스를 구현할 때 그것, 아니면 <see cref="MvContentMenuPort.Instance"/>.</summary>
        public IContentMenuPort Content => _content ?? Port as IContentMenuPort ?? MvContentMenuPort.Instance;
        public void SetContentPort(IContentMenuPort port) => _content = port;

        public string Tab => _tab;

        protected override void OnOpen()
        {
            _shownNew.Clear();
            string last = Port.Ready ? Content.LastTab : null;
            _tab = last == TabMeta ? TabMeta : TabMode; // 05 §5.15.4 ⑥: 같은 탭으로 다시 열림
        }

        protected override bool OnTab(string tabId)
        {
            if ((tabId != TabMode && tabId != TabMeta) || tabId == _tab) return false;
            _tab = tabId;
            Play(MetaSound.Click);
            return true;
        }

        protected override void Build(MetaPanelModel m, MvTables tables)
        {
            m.Title = "컨텐츠";                                     // ui.content.title
            m.Sub = "누르면 바로 시작 · 입장권은 매일 다시 채워짐";   // ui.content.sub
            m.Tabs.Add(new MetaTab { Id = TabMode, Text = "모드", Selected = _tab == TabMode }); // ui.content.tabMode
            m.Tabs.Add(new MetaTab { Id = TabMeta, Text = "성장", Selected = _tab == TabMeta }); // ui.content.tabMeta
            if (_tab == TabMeta) BuildMeta(m, tables);
            else BuildModes(m, tables);
        }

        // ───────── 모드 탭 ─────────

        /// <summary>카드가 되는 <c>400_content</c> 행(05 §5.15.3: contentType ≥ 1, active 1, sort 순).</summary>
        public static IEnumerable<ContentData> ModeRows(MvTables tables)
        {
            foreach (var c in tables.Content.Sorted)
                if (c.active && c.contentType != ContentType.Main) yield return c;
        }

        void BuildModes(MetaPanelModel m, MvTables tables)
        {
            if (tables.AdPlacement.HasKey(AdTicketKey))
            {
                int left = Port.AdsLeft(AdTicketKey);
                double gain = 0; // 08[modeTicket].adRewardValue(옛 문구 「+1」 고정)
                foreach (var r in tables.AdPlacement.ByKey(AdTicketKey).adReward) gain += r.Value;
                m.Add(new MetaRow("adInfo", MetaRowKind.Text,
                    "광고 보고 입장권 +" + gain.ToString("0.##", Inv) + " · 오늘 " + (left < 0 ? "∞" : left.ToString(Inv)) + "회 남음")); // ui.content.adInfo
            }

            foreach (var c in ModeRows(tables))
            {
                var open = c.openId != 0 ? tables.ContentOpen.Get(c.openId) : null;
                bool locked = IsLocked(open);
                var row = m.Add(new MetaRow(GoPrefix + c.key, MetaRowKind.Row, NameOf(c.id, c.key))
                {
                    Icon = c.icon,
                    Desc = DescOf(c.id),
                    Locked = locked,
                    Value = Chips(c, tables, locked, open),
                });
                // 잠김 카드도 누를 수 있다(누르면 잠금 토스트, 05 §5.15.3).
                row.Act(GoPrefix + c.key, locked ? LockText(open) : "도전", true); // ui.content.lockAt / ui.content.go
            }
        }

        string Chips(ContentData c, MvTables tables, bool locked, ContentOpenData open)
        {
            var parts = new List<string>(4);
            if (!locked && open != null && Content.IsNew(open.key))
            {
                parts.Add("NEW"); // ui.content.new
                _shownNew.Add(open.key);
            }

            var ticket = TicketOf(c, tables);
            if (ticket != null)
            {
                long have = Content.TicketCount(ticket.id);
                parts.Add("입장권 " + (have < 0 ? "-" : have.ToString(Inv)) + "/" + ticket.chargeMax.ToString(Inv));
            }

            if (c.dailyLimit > 0)
            {
                int left = Math.Max(0, c.dailyLimit - Content.ModePlaysToday(c.key));
                parts.Add("오늘 " + left.ToString(Inv) + "/" + c.dailyLimit.ToString(Inv)); // 남은 입장/하루 한도
            }
            return string.Join(" · ", parts);
        }

        /// <summary>입장 비용 중 입장권(<c>02_playResource</c>) 행. 없으면 null.</summary>
        static PlayResourceData TicketOf(ContentData c, MvTables tables)
        {
            if (c.spendResource == null) return null;
            foreach (var s in c.spendResource)
                if (s.Type == ConsumeType.PlayResource) return tables.PlayResource.Get(s.Id);
            return null;
        }

        // ───────── 성장 탭 ─────────

        void BuildMeta(MetaPanelModel m, MvTables tables)
        {
            foreach (var f in tables.ContentOpen.Sorted)
            {
                if (!f.active || f.contentId != 0 || !IsMetaSystemKey(f.key)) continue;
                bool locked = IsLocked(f);
                string sub = "";
                if (!locked && Content.IsNew(f.key))
                {
                    sub = "NEW"; // ui.content.new
                    _shownNew.Add(f.key);
                }
                m.Add(new MetaRow(MetaPrefix + f.key, MetaRowKind.Row, NameOf(f.id, f.key))
                {
                    Icon = f.icon,
                    Desc = DescOf(f.id),
                    Locked = locked,
                    Value = sub,
                }).Act(MetaPrefix + f.key, locked ? LockText(f) : "›", true);
            }
        }

        // ───────── 누름 ─────────

        protected override bool OnAction(string actionId)
        {
            var tables = Port.Tables;
            if (tables == null) return false;
            if (actionId.StartsWith(GoPrefix, StringComparison.Ordinal))
                return PressMode(tables, actionId.Substring(GoPrefix.Length));
            if (actionId.StartsWith(MetaPrefix, StringComparison.Ordinal))
                return PressMeta(tables, actionId.Substring(MetaPrefix.Length));
            return false;
        }

        bool PressMode(MvTables tables, string key)
        {
            var c = tables.Content.ByKey(key);
            if (c == null) return false;
            Play(MetaSound.Click);
            var open = c.openId != 0 ? tables.ContentOpen.Get(c.openId) : null;
            if (IsLocked(open))
            {
                Say(LockText(open)); // toastMessage:contentLocked
                return false;
            }
            if (c.dailyLimit > 0 && Content.ModePlaysToday(key) >= c.dailyLimit)
            {
                Say(NoCountText); // toastMessage:contentNoCount
                return false;
            }

            // 교체 지점(G7 MV-CONTENT-<모드>): 시작 콜백이 없으면 입장 기록 없이 「준비 중」만 낸다(하루 한도를 헛되이 쓰지 않는다).
            var start = StartMode;
            if (start == null)
            {
                Say(NameOf(c.id, c.key) + " 준비 중");
                return false;
            }

            var r = Content.RecordModePlay(key);
            if (!r.Ok)
            {
                Say(r.Code == MvMetaErrors.DailyLimit ? NoCountText : r.Code == MvMetaErrors.Locked ? LockText(open) : "입장하지 못했습니다");
                return false;
            }
            RequestClose(); // 05 §5.15.4 ④: 메뉴 닫힘 → 런 시작
            start(key);
            return true;
        }

        bool PressMeta(MvTables tables, string key)
        {
            var f = tables.ContentOpen.ByKey(key);
            if (f == null) return false;
            Play(MetaSound.Click);
            if (IsLocked(f))
            {
                Say(LockText(f));
                return false;
            }
            // 교체 지점(MV-META-<시스템>): 그 시스템의 UIPopup_<이름>을 메뉴 위에 연다(메뉴는 열린 채).
            var openMeta = OpenMeta;
            if (openMeta == null)
            {
                Say(NameOf(f.id, f.key) + " 준비 중");
                return false;
            }
            openMeta(key);
            return true;
        }

        // ───────── 잠금·문구 ─────────

        /// <summary>오늘 입장 한도를 다 썼을 때(<c>toastMessage:contentNoCount</c>).</summary>
        public const string NoCountText = "오늘은 더 입장할 수 없어요";

        bool IsLocked(ContentOpenData open) => open != null && !Port.IsOpen(open.key);

        /// <summary>잠금 문구(<c>ui.content.lockAt</c>·<c>toastMessage:contentLocked</c>). 조건 문구는 <see cref="MvUnlockText"/>.</summary>
        public static string LockText(ContentOpenData open)
        {
            if (open == null) return "";
            string need = MvUnlockText.Need(open);
            return need.Length > 0 ? need + " 달성 시 열려요" : "업데이트 예정";
        }

        void SaveSeen()
        {
            if (!Port.Ready) return;
            Content.MarkMenuSeen(new List<string>(_shownNew), _tab);
            _shownNew.Clear();
        }
    }
}
