using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MV.Data;
using MV.Meta;
using NUnit.Framework;

namespace MV.UI.Tests
{
    /// <summary>
    /// 컨텐츠 메뉴(05 §5.15, D-92 ③): <c>400_content</c> 행 추가 → 카드 추가, 잠금(<c>05_contentOpen</c>), 하루 한도 거절, 도전 1번 → 메타 1번.
    /// </summary>
    public sealed class ContentMenuTests
    {
        const string Alley = "alleyWar";

        static int Cards(MetaPanelModel m) => m.Rows.Count(r => r.Id.StartsWith(ContentMenuPresenter.GoPrefix, StringComparison.Ordinal));

        static ContentMenuPresenter Open(ContentFakePort port, Action<string> start = null)
        {
            var p = new ContentMenuPresenter { StartMode = start };
            p.SetPort(port);
            p.Open();
            return p;
        }

        /// <summary><c>400_content</c>의 <paramref name="from"/> 행을 복사해 1행 더한 표(파일은 바꾸지 않는다).</summary>
        static MvTables WithContentRow(string from, string newKey, bool active)
        {
            return ContentTables.Edit(ContentData.Table, (head, rows) =>
            {
                var add = (string[])rows.First(r => r[Array.IndexOf(head, "key")] == from).Clone();
                foreach (var col in new[] { "id", "sort" })
                {
                    int c = Array.IndexOf(head, col);
                    add[c] = (rows.Max(r => long.Parse(r[c])) + 1).ToString();
                }
                add[Array.IndexOf(head, "key")] = newKey;
                add[Array.IndexOf(head, "active")] = active ? "1" : "0";
                rows.Add(add);
            });
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)] // 2차(active 0)는 숨김(05 §5.15.3)
        public void ContentRowAdded_AddsOneCard(bool active, int added)
        {
            int before = Cards(Open(new ContentFakePort(MetaUiTestKit.Base())).Model);
            var after = Open(new ContentFakePort(WithContentRow(Alley, "alleyWar_t", active))).Model;

            Assert.That(before, Is.EqualTo(ContentMenuPresenter.ModeRows(MetaUiTestKit.Base()).Count()));
            Assert.That(before, Is.GreaterThan(0));
            Assert.That(Cards(after), Is.EqualTo(before + added));
            Assert.That(after.FindRow(ContentMenuPresenter.GoPrefix + "alleyWar_t") != null, Is.EqualTo(active));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Lock_FollowsContentOpen(bool open)
        {
            var tables = MetaUiTestKit.Base();
            var openKey = tables.ContentOpen.Get(tables.Content.ByKey(Alley).openId).key;
            var port = new ContentFakePort(tables);
            if (!open) port.Closed.Add(openKey);
            var toasts = new List<string>();
            int started = 0;
            var p = Open(port, _ => started++);
            p.Toast += toasts.Add;

            string id = ContentMenuPresenter.GoPrefix + Alley;
            Assert.That(p.Model.FindRow(id).Locked, Is.EqualTo(!open));
            p.Press(id);

            Assert.That(port.RecordCalls, Is.EqualTo(open ? 1 : 0));
            Assert.That(started, Is.EqualTo(open ? 1 : 0));
            if (!open) Assert.That(toasts, Is.EqualTo(new[] { ContentMenuPresenter.LockText(tables.ContentOpen.ByKey(openKey)) }));
        }

        [TestCase(1, false, 1, true)]  // 한도 안: 메타 1번, 시작
        [TestCase(2, false, 0, false)] // 오늘 다 씀: 메타를 부르지 않고 토스트
        [TestCase(1, true, 1, false)]  // 메타가 DailyLimit 거절: 토스트, 시작 안 함
        public void DailyLimit_RejectsWithToast(int playsToday, bool metaRejects, int recordCalls, bool starts)
        {
            var tables = ContentTables.Edit(ContentData.Table, (head, rows) =>
                rows.First(r => r[Array.IndexOf(head, "key")] == Alley)[Array.IndexOf(head, "dailyLimit")] = "2");
            var port = new ContentFakePort(tables) { RejectRecord = metaRejects ? MvMetaErrors.DailyLimit : null };
            port.Plays[Alley] = playsToday;
            var toasts = new List<string>();
            string startedKey = null;
            var p = Open(port, k => startedKey = k);
            p.Toast += toasts.Add;

            StringAssert.Contains("오늘 " + (2 - playsToday) + "/2", p.Model.FindRow(ContentMenuPresenter.GoPrefix + Alley).Value);
            Assert.That(p.Press(ContentMenuPresenter.GoPrefix + Alley), Is.EqualTo(starts));

            Assert.That(port.RecordCalls, Is.EqualTo(recordCalls));
            Assert.That(startedKey, Is.EqualTo(starts ? Alley : null));
            Assert.That(toasts, Is.EqualTo(starts ? new string[0] : new[] { ContentMenuPresenter.NoCountText }));
        }

        [Test]
        public void Go_Once_CallsMetaOnceThenClosesAndStarts()
        {
            var port = new ContentFakePort(MetaUiTestKit.Base());
            var order = new List<string>();
            var p = Open(port, k => order.Add("start:" + k));
            p.CloseRequested += () => order.Add("close");

            Assert.That(p.Press(ContentMenuPresenter.GoPrefix + Alley), Is.True);

            Assert.That(port.RecordCalls, Is.EqualTo(1));
            Assert.That(port.LastRecord, Is.EqualTo(Alley));
            Assert.That(order, Is.EqualTo(new[] { "close", "start:" + Alley }));
        }

        [Test]
        public void Go_WithoutStartHook_ToastsWithoutMeta()
        {
            var port = new ContentFakePort(MetaUiTestKit.Base());
            var toasts = new List<string>();
            var p = Open(port);
            p.Toast += toasts.Add;

            Assert.That(p.Press(ContentMenuPresenter.GoPrefix + Alley), Is.False);
            Assert.That(port.RecordCalls, Is.EqualTo(0));
            Assert.That(toasts.Single(), Does.EndWith("준비 중"));
        }

        [Test]
        public void MetaNull_BlocksEveryButton()
        {
            var port = new ContentFakePort(MetaUiTestKit.Base()) { Ready = false };
            int started = 0;
            var p = Open(port, _ => started++);
            var actions = p.Model.AllActions().ToList();

            Assert.That(actions, Is.Not.Empty);
            Assert.That(actions.All(a => !a.Enabled), Is.True);
            foreach (var a in actions) Assert.That(p.Press(a.Id), Is.False, a.Id);
            Assert.That(port.RecordCalls + started, Is.EqualTo(0));
        }

        [Test]
        public void Close_SavesShownNewAndTabOnce()
        {
            var port = new ContentFakePort(MetaUiTestKit.Base());
            port.New.Add("contentMenu"); // alleyWar의 openId 행
            var p = Open(port);
            p.SelectTab(ContentMenuPresenter.TabMeta);
            Assert.That(p.Model.Rows, Is.Not.Empty);
            Assert.That(p.Model.Rows.All(r => r.Id.StartsWith(ContentMenuPresenter.MetaPrefix, StringComparison.Ordinal)), Is.True);

            p.PressClose();

            Assert.That(port.SeenCalls, Is.EqualTo(1));
            Assert.That(port.LastSeen, Is.EqualTo(new[] { "contentMenu" }));
            Assert.That(port.LastTabSaved, Is.EqualTo(ContentMenuPresenter.TabMeta));
        }
    }

    /// <summary>표 원문을 메모리에서만 고친 표.</summary>
    internal static class ContentTables
    {
        public static MvTables Edit(string table, Action<string[], List<string[]>> edit)
        {
            string dir = MvTableLoader.TsvDirectory;
            string text = File.ReadAllText(Path.Combine(dir, table + ".txt"), new UTF8Encoding(false));
            var lines = text.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).ToList();
            var head = lines[0].Split('\t');
            var rows = lines.Skip(1).Select(l => l.Split('\t')).ToList();
            edit(head, rows);
            var body = string.Join("\n", new[] { lines[0] }.Concat(rows.Select(r => string.Join("\t", r)))) + "\n";
            var t = new MvTables();
            t.LoadAll(new MvMemoryTableSource(new MvTsvDirectorySource(dir)).Set(table, body));
            return t;
        }
    }

    /// <summary>메타 입구 + 컨텐츠 입구를 함께 흉내 내며 호출을 센다. 기본은 모두 열림.</summary>
    internal sealed class ContentFakePort : IMetaUiPort, IContentMenuPort
    {
        public bool Ready { get; set; } = true;
        public MvTables Tables { get; set; }
        public int AreaIndex => 0;
        public int Cp => 0;
        public long Wealth(int wealthId) => 0;

        public readonly HashSet<string> Closed = new HashSet<string>();
        public readonly HashSet<string> New = new HashSet<string>();
        public readonly Dictionary<string, int> Plays = new Dictionary<string, int>();
        public string RejectRecord;
        public int RecordCalls;
        public string LastRecord;
        public int SeenCalls;
        public string[] LastSeen;
        public string LastTabSaved;

        public ContentFakePort(MvTables tables) { Tables = tables; }

        public int ModePlaysToday(string contentKey) => Plays.TryGetValue(contentKey, out var n) ? n : 0;

        public MetaResult RecordModePlay(string contentKey)
        {
            RecordCalls++;
            LastRecord = contentKey;
            return RejectRecord != null ? MetaResult.Fail(RejectRecord) : MetaResult.Success();
        }

        public bool IsNew(string openKey) => New.Contains(openKey);

        public MetaResult MarkMenuSeen(IReadOnlyCollection<string> newSeen, string tab)
        {
            SeenCalls++;
            LastSeen = newSeen.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            LastTabSaved = tab;
            return MetaResult.Success();
        }

        public string LastTab => "";
        public long TicketCount(int playResourceId) => -1;

        public bool IsOpen(string key) => !Closed.Contains(key);
        public int AdsLeft(string key) => 3;

        public int Level(MvGrowth growth, string key) => 0;
        public bool IsMax(MvGrowth growth, string key) => false;
        public IReadOnlyList<ConsumeData> NextPrice(MvGrowth growth, string key) => null;
        public bool CanBuy(MvGrowth growth, string key) => false;
        public MetaResult Buy(MvGrowth growth, string key) => MetaResult.Fail(null);
        public string SelectedGod => null;
        public bool GodUnlocked(string key) => false;
        public MetaResult SelectGod(string key) => MetaResult.Fail(null);
        public int RebirthLevel => 0;
        public int RebirthPending => 0;
        public bool RebirthOpen => false;
        public long RebirthCooldownLeftMs => 0;
        public bool RebirthArmed => false;
        public MetaResult? PressRebirth() => null;
        public void WatchAdForReward(string key, Action<MetaResult> done) => done?.Invoke(MetaResult.Fail(null));
        public IReadOnlyList<string> PendingUnlockShows() => Array.Empty<string>();
        public MetaResult MarkUnlockSeen(string key) => MetaResult.Fail(null);
        public MetaResult SendDebug(string requestName, string key = null, long amount = 0) => MetaResult.Fail(null);
        public IMvAdService Ads => null;
    }
}
