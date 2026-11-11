using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using jjevol;
using MV.Data;
using MV.Meta;
using MV.Save;
using NUnit.Framework;

namespace MV.Content.Tests
{
    /// <summary>합성 시험 키(실제 빌드 키를 쓰지 않는다).</summary>
    internal sealed class QuestTestKey : IMvSaveKeyProvider
    {
        public string KeyId => "mv-quest-test-1";
        public byte[] GetMasterKey()
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes("MoonVillage.Quest.Test.Key"));
        }
    }

    /// <summary>
    /// 퀘스트 규칙(08 §8.5.1): 카운터·받기·초기화·업적 단계·표 확장. TSV 정본 표 + 메모리 저장소(실제 PlayerPrefs·persistentDataPath 안 씀) + 고정 시계.
    /// </summary>
    public sealed class MvQuestTests
    {
        /// <summary>2026-11-10T12:00:00Z.</summary>
        const long T0 = 1_794_312_000_000L;
        const int DailyRun = 413001001;     // runComplete 3, 캣닢 10 + 일일 포인트 20
        const int DailyAd = 413001006;      // adWatch 2
        const int Summary40 = 413002001;    // dailyPoint 40 → 츄르 300
        const int MonthlySoul = 413003001;  // soulEarn 50 (+50씩 반복)
        const int MonthlyIncome = 413003004; // lobbyUpgrade 조건 3(생선 건조대) 5
        const int AchRun1 = 420300001, AchRun2 = 420300002;
        /// <summary>[TestCase] 성공 표시(거절 코드 자리).</summary>
        const string Ok = "ok";

        static int _serial;
        static MvTables _tables;

        MvTables Tables;
        MvSaveStore Store;
        ResourceService Res;
        MvManualClock Clock;
        MvMeta Meta;
        MvQuest Quest;

        static MvTables LoadTables()
        {
            if (_tables != null) return _tables;
            var t = new MvTables();
            t.LoadAll(new MvTsvDirectorySource(MvTableLoader.TsvDirectory));
            return _tables = t;
        }

        [SetUp]
        public void SetUp()
        {
            Tables = LoadTables();
            Clock = new MvManualClock(T0);
            var config = Tables.Config.Value;
            MvSaveOpenResult opened = MvSaveStore.Open(new MvSaveOptions
            {
                ProfileId = "mvquest" + (++_serial),
                KeyNamespace = "mv.test.quest",
                KeyProvider = new QuestTestKey(),
                Preferences = new MvMemorySavePrefs(),
                NowMs = () => Clock.NowMs,
                Config = new MvSaveConfig
                {
                    StartDeck = config.startDeck.ToArray(),
                    StartGodKey = config.startGodKey,
                    CpMax = Tables.RunConfig.I(RunConfigKeys.CP_MAX),
                    FloorCount = Tables.FloorCount(),
                    DayUtcOffsetHour = config.adDayUtcOffsetHour,
                },
            });
            Assert.That(opened.IsOpen, Is.True, opened.Status + " " + opened.Error);
            Store = opened.Store;
            Res = new ResourceService(Store.Repository, Tables.Wealth, new ConsumeTypeId((int)ConsumeType.Wealth));
            Meta = new MvMeta(() => Store, () => Res, Tables, Clock, new MvFakeAdService());
            // quest·achievement 열림(런 1번): 메타 정산 요청으로만 진행을 바꾼다. 카운터 연결 전이라 이 정산은 세지 않는다.
            Assert.That(Meta.Settle(new MvRunOutcome { RunSeq = Meta.BeginRun(), End = MvRunEnd.Wall }).Ok, Is.True);
            Quest = new MvQuest(Meta);
        }

        [TearDown]
        public void TearDown()
        {
            Quest?.Dispose();
            Res?.Dispose();
            Store?.Dispose();
            Quest = null; Res = null; Store = null; Meta = null;
        }

        MvQuestEntry Entry(int id) => Quest.Board().SelectMany(g => g.Entries).FirstOrDefault(e => e.Id == id);
        MvQuestEntry StepOf(int group) => Quest.Board().First(g => g.Row.group == group).Entries.Single();
        void Rec(QuestClearType t, long v, int c = 0) => Assert.That(Quest.Record(new MvQuestEvent(t, v, c)).Ok, Is.True);

        // ───────── 받기 경계값 ─────────

        [TestCase(2, MvMetaErrors.NotDone)]
        [TestCase(3, Ok)]
        [TestCase(7, Ok)]
        public void Claim_DailyTarget_Boundary_AndOnlyOnce(int runs, string expected)
        {
            string code = expected == Ok ? null : expected;
            Rec(QuestClearType.RunComplete, runs);
            long dia = Res.GetRaw(MvWealthIds.Dia);
            MvMetaResponse first = Quest.Claim(DailyRun);
            Assert.That(first.Code, Is.EqualTo(code));
            if (code != null)
            {
                Assert.That(Res.GetRaw(MvWealthIds.Dia), Is.EqualTo(dia), "거절은 보상 없음");
                return;
            }
            Assert.That(Res.GetRaw(MvWealthIds.Dia), Is.EqualTo(dia + 10));
            Assert.That(Entry(DailyRun).State, Is.EqualTo(MvQuestState.Done));
            Assert.That(Quest.Claim(DailyRun).Code, Is.EqualTo(MvMetaErrors.AlreadyClaimed), "수령 1번");
            Assert.That(Res.GetRaw(MvWealthIds.Dia), Is.EqualTo(dia + 10));
        }

        [TestCase(-1, false)]
        [TestCase(0, true)]
        public void Daily_ResetsAtUtcMidnight(int offsetFromMidnightMs, bool reset)
        {
            long midnight = MvQuestRules.NextDayMs(T0);
            Clock.NowMs = midnight - 1000;
            Rec(QuestClearType.RunComplete, 3);
            Rec(QuestClearType.SoulEarn, 7);
            Assert.That(Quest.Claim(DailyRun).Ok, Is.True);
            Clock.NowMs = midnight + offsetFromMidnightMs;
            var e = Entry(DailyRun);
            Assert.That(e.Count, Is.EqualTo(reset ? 0 : 3));
            Assert.That(e.State, Is.EqualTo(reset ? MvQuestState.Progress : MvQuestState.Done));
            Assert.That(Entry(Summary40).Count, Is.EqualTo(reset ? 0 : 20), "일일 포인트도 그날 것");
            Assert.That(Entry(MonthlySoul).Count, Is.EqualTo(7), "월간은 그대로");
            Assert.That(StepOf(300).Count, Is.EqualTo(3), "평생 카운터는 그대로");
        }

        [TestCase(0, 1, MvMetaErrors.NotDone)]
        [TestCase(1, 1, Ok)]
        [TestCase(1, 2, MvMetaErrors.NotDone)]
        public void Monthly_RepeatTargetGrowsPerClaim(int extraBeforeFirst, int claims, string expected)
        {
            string lastCode = expected == Ok ? null : expected;
            Rec(QuestClearType.SoulEarn, 49 + extraBeforeFirst);
            MvMetaResponse last = null;
            for (int i = 0; i < claims; i++) last = Quest.Claim(MonthlySoul);
            Assert.That(last.Code, Is.EqualTo(lastCode));
            var e = Entry(MonthlySoul);
            Assert.That(e.Target, Is.EqualTo(50 + 50 * e.Claimed), "목표 = clearValue + clearValueStep × 받은 횟수");
            Rec(QuestClearType.SoulEarn, 50);
            if (e.Claimed == 1) Assert.That(Quest.Claim(MonthlySoul).Ok, Is.True, "100 도달 → 2번째");
        }

        [Test]
        public void Monthly_ResetsAtUtcMonthStart()
        {
            Rec(QuestClearType.SoulEarn, 60);
            Assert.That(Quest.Claim(MonthlySoul).Ok, Is.True);
            Clock.NowMs = MvQuestRules.NextMonthMs(T0) - 1;
            Assert.That(Entry(MonthlySoul).Claimed, Is.EqualTo(1));
            Clock.NowMs += 1;
            var e = Entry(MonthlySoul);
            Assert.That((e.Count, e.Claimed, e.Target), Is.EqualTo((0L, 0, 50L)));
        }

        // ───────── 일일 종합·업적 ─────────

        [Test]
        public void DailyPoints_FromClaims_UnlockSummary()
        {
            Rec(QuestClearType.RunComplete, 3);
            Assert.That(Quest.Claim(Summary40).Code, Is.EqualTo(MvMetaErrors.NotDone));
            Assert.That(Quest.Claim(DailyRun).Ok, Is.True);
            Assert.That(Entry(Summary40).Count, Is.EqualTo(20));
            Rec(QuestClearType.AdWatch, 2);
            Assert.That(Quest.Claim(DailyAd).Ok, Is.True);
            long gold = Res.GetRaw(MvWealthIds.Gold);
            Assert.That(Quest.Claim(Summary40).Ok, Is.True);
            Assert.That(Res.GetRaw(MvWealthIds.Gold), Is.EqualTo(gold + 300));
        }

        [Test]
        public void Achievement_StepsInOrder()
        {
            Rec(QuestClearType.RunComplete, 60);
            Assert.That(Quest.Claim(AchRun2).Code, Is.EqualTo(MvMetaErrors.NotDone), "앞 단계 먼저");
            var e = StepOf(300);
            Assert.That((e.Id, e.Step, e.StepCount, e.State), Is.EqualTo((AchRun1, 1, 5, MvQuestState.Claimable)));
            Assert.That(Quest.Claim(AchRun1).Ok, Is.True);
            Assert.That(Quest.Claim(AchRun1).Code, Is.EqualTo(MvMetaErrors.AlreadyClaimed));
            e = StepOf(300);
            Assert.That((e.Id, e.PrevTarget, e.Target, e.State), Is.EqualTo((AchRun2, 10L, 50L, MvQuestState.Claimable)));
        }

        [Test]
        public void Claim_Locked_BeforeFirstRun()
        {
            Assert.That(Meta.SendDebug(MvMetaRequestNames.DebugReset).Ok, Is.True, "진행 초기화(런 0번, 개발 빌드 요청)");
            Rec(QuestClearType.RunComplete, 3);
            Assert.That(Quest.Claim(DailyRun).Code, Is.EqualTo(MvMetaErrors.Locked));
        }

        [TestCase(20, 1)]
        [TestCase(19, 1)]
        [TestCase(1, 0)]
        [TestCase(1, -5)]
        public void Record_RejectsBadEvents(int type, int amount)
        {
            var r = Quest.Record(new MvQuestEvent((QuestClearType)type, amount));
            if (amount > 0) Assert.That(r.Code, Is.EqualTo(MvMetaErrors.BadParams), "모르는 종류·일일 포인트(받기 전용) 거절");
            else Assert.That(r.Ok, Is.True, "0 이하 양은 보내지 않는다");
            Assert.That(Entry(DailyRun).Count, Is.EqualTo(0), "카운터 그대로");
            Assert.That(Entry(Summary40).Count, Is.EqualTo(0));
        }

        // ───────── 메타 요청 → 카운터 ─────────

        [Test]
        public void MetaRequests_CountedInSameResponse()
        {
            long seq = Meta.BeginRun();
            var settled = Meta.Settle(new MvRunOutcome { RunSeq = seq, End = MvRunEnd.Wall, Gold = 120.7, Souls = 4 });
            Assert.That(settled.Ok, Is.True, settled.Code);
            Assert.That(Entry(DailyRun).Count, Is.EqualTo(1), "runComplete");
            Assert.That(Entry(413001004).Count, Is.EqualTo(settled.GoldGained), "goldEarn = 계정 츄르 증가분");
            Assert.That(Entry(MonthlySoul).Count, Is.EqualTo(4), "soulEarn");

            Assert.That(MvQuest.LobbyCondition(Tables, MvMetaRules.LobbyIncome), Is.EqualTo(3), "생선 건조대 = 조건 3");
            Assert.That(Quest.Record(new MvQuestEvent(QuestClearType.LobbyUpgrade, 1, 3), new MvQuestEvent(QuestClearType.LobbyUpgrade, 1, 1)).Ok, Is.True);
            Assert.That(Entry(MonthlyIncome).Count, Is.EqualTo(1), "조건 3만");
            Assert.That(Quest.LifetimeCount(QuestClearType.LobbyUpgrade), Is.EqualTo(2), "조건 0 = 전체");
        }

        // ───────── 통합: 원래 요청의 같은 저장·기간 굴림 ─────────

        [Test]
        public void Settle_OneRun_AddsDailyProgressInSameSave()
        {
            int applied = 0;
            Meta.Applied += (rq, rs) => applied++;
            var settled = Meta.Settle(new MvRunOutcome { RunSeq = Meta.BeginRun(), End = MvRunEnd.Wall, Souls = 2 });
            Assert.That(settled.Ok, Is.True, settled.Code);
            Assert.That(applied, Is.EqualTo(1), "성공 알림 안에서 기록 요청을 다시 보내지 않는다");
            Assert.That(Store.Quest.Count(MvQuestRules.DailySlot, MvQuestRules.CounterKey(QuestClearType.RunComplete, 0)), Is.EqualTo(1), "정산 저장에 일일 진행 +1");
            Assert.That(Store.Quest.Count(MvQuestRules.MonthlySlot, MvQuestRules.CounterKey(QuestClearType.SoulEarn, 0)), Is.EqualTo(2));
            Assert.That(Entry(DailyRun).Count, Is.EqualTo(1));
        }

        [Test]
        public void AnyWrite_RollsStalePeriodBeforeSaving()
        {
            Rec(QuestClearType.RunComplete, 2);
            string today = MvQuestRules.DayKey(T0);
            Clock.NowMs = MvQuestRules.NextMonthMs(T0);
            Assert.That(Store.Quest.Period(MvQuestRules.DailySlot), Is.EqualTo(today), "아직 쓰기 전");
            Assert.That(Entry(DailyRun).Count, Is.EqualTo(0), "읽기는 굴린 값");
            Assert.That(Meta.SetMuted(true).Ok, Is.True, "퀘스트와 무관한 쓰기");
            Assert.That(Store.Quest.Period(MvQuestRules.DailySlot), Is.EqualTo(MvQuestRules.DayKey(Clock.NowMs)));
            Assert.That(Store.Quest.Period(MvQuestRules.MonthlySlot), Is.EqualTo(MvQuestRules.MonthKey(Clock.NowMs)));
            Assert.That(Store.Quest.Count(MvQuestRules.DailySlot, "1"), Is.EqualTo(0));
            Assert.That(Store.Quest.Count(MvQuestRules.AchieveSlot, "1"), Is.EqualTo(2), "평생 카운터는 굴리지 않는다");
        }

        // ───────── 표 확장(12 §12.6-C) ─────────

        static MvTables WithExtraRow(string table, Func<string[], string[], string[]> edit)
        {
            string dir = MvTableLoader.TsvDirectory;
            var lines = File.ReadAllText(Path.Combine(dir, table + ".txt"), new UTF8Encoding(false)).Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).ToList();
            string[] head = lines[0].Split('\t');
            string[] add = edit(head, (string[])lines[lines.Count - 1].Split('\t').Clone());
            var t = new MvTables();
            t.LoadAll(new MvMemoryTableSource(new MvTsvDirectorySource(dir)).Set(table, string.Join("\n", lines) + "\n" + string.Join("\t", add) + "\n"));
            return t;
        }

        static string[] SetCols(string[] head, string[] row, params (string col, string value)[] values)
        {
            foreach (var (col, value) in values) row[Array.IndexOf(head, col)] = value;
            return row;
        }

        [Test]
        public void TableRowAdded_AddsEntryAndStep()
        {
            var snap = MvQuestRules.Empty(T0);
            int Daily(MvTables t) => MvQuestRules.Board(t, snap, T0).First(g => g.Row.group == 1).Entries.Count;
            int Steps(MvTables t) => MvQuestRules.Board(t, snap, T0).First(g => g.Row.group == 300).Entries.Single().StepCount;

            var daily = WithExtraRow(RepeatQuestData.Table, (h, r) => SetCols(h, r, ("id", "413001007"), ("questGroup", "1"), ("sort", "7"),
                ("periodType", "2"), ("clearType", "12"), ("clearValue", "1"), ("clearValueStep", "0")));
            Assert.That(Daily(daily), Is.EqualTo(Daily(Tables) + 1));
            Assert.That(MvQuestRules.Board(daily, snap, T0).First(g => g.Row.group == 1).Entries.Last().ClearType, Is.EqualTo(QuestClearType.Rebirth));

            var ach = WithExtraRow(AchievementData.Table, (h, r) => SetCols(h, r, ("id", "420300006"), ("questGroup", "300"), ("progress", "6"),
                ("clearType", "1"), ("prevClearValue", "1000"), ("clearValue", "3000")));
            Assert.That(Steps(ach), Is.EqualTo(Steps(Tables) + 1));
        }
    }
}
