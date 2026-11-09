using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using jjevol;
using MV.Data;
using MV.Save;
using NUnit.Framework;

namespace MV.Meta.Tests
{
    /// <summary>합성 시험 키(실제 빌드 키를 쓰지 않는다).</summary>
    internal sealed class MetaTestKey : IMvSaveKeyProvider
    {
        public string KeyId => "mv-meta-test-1";
        public byte[] GetMasterKey()
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes("MoonVillage.Meta.Test.Key"));
        }
    }

    /// <summary>
    /// 메타 시험 공통 준비: TSV 정본 표 + 메모리 저장소(실제 PlayerPrefs·persistentDataPath를 쓰지 않는다) + 재화 서비스 + 고정 시계.
    /// </summary>
    public abstract class MvMetaTestBase
    {
        /// <summary>2023-11-14T22:13:20Z.</summary>
        protected const long T0 = 1_700_000_000_000L;
        private static int _serial;
        private static MvTables _tables;

        protected MvTables Tables;
        protected MvMemorySavePrefs Prefs;
        protected MvSaveStore Store;
        protected ResourceService Res;
        protected MvManualClock Clock;
        protected MvFakeAdService Ads;
        protected MvMeta Meta;

        protected static MvTables LoadTables()
        {
            if (_tables != null) return _tables;
            var tables = new MvTables();
            tables.LoadAll(new MvTsvDirectorySource(MvTableLoader.TsvDirectory));
            return _tables = tables;
        }

        [SetUp]
        public void SetUpMeta()
        {
            Tables = LoadTables();
            Clock = new MvManualClock(T0);
            Prefs = new MvMemorySavePrefs();
            var config = Tables.Config.Value;
            var options = new MvSaveOptions
            {
                ProfileId = "mvmeta" + (++_serial),
                KeyNamespace = "mv.test.meta",
                KeyProvider = new MetaTestKey(),
                Preferences = Prefs,
                NowMs = () => Clock.NowMs,
                Config = new MvSaveConfig
                {
                    StartDeck = config.startDeck.ToArray(),
                    StartGodKey = config.startGodKey,
                    CpMax = Tables.RunConfig.I(RunConfigKeys.CP_MAX),
                    FloorCount = Tables.RunConfig.I(RunConfigKeys.FLOOR_COUNT),
                    DayUtcOffsetHour = config.adDayUtcOffsetHour,
                },
            };
            MvSaveOpenResult opened = MvSaveStore.Open(options);
            Assert.That(opened.IsOpen, Is.True, opened.Status + " " + opened.Error);
            Store = opened.Store;
            Res = new ResourceService(Store.Repository, Tables.Wealth, new ConsumeTypeId((int)ConsumeType.Wealth));
            Ads = new MvFakeAdService();
            Meta = new MvMeta(() => Store, () => Res, Tables, Clock, Ads);
        }

        [TearDown]
        public void TearDownMeta()
        {
            Res?.Dispose();
            Store?.Dispose();
            Res = null;
            Store = null;
            Meta = null;
        }

        // ───────── 준비 도우미(시험 전용: 저장 상태를 직접 놓는다) ─────────

        protected void SetWealth(params (int id, long amount)[] values)
        {
            string rows = string.Join(",", values.Select(v =>
                "{\"wealthType\":\"" + MvWealthIds.TypeOf(v.id) + "\",\"quantity\":" + v.amount.ToString(CultureInfo.InvariantCulture) + "}"));
            Assert.That(Store.ApplyAndSaveJson("{\"wealthInfos\":[" + rows + "]}", false, MvSaveKeys.Wealth), Is.True);
        }

        /// <summary>
        /// 컨테이너 하나의 필드를 병합한다. <paramref name="fieldsJson"/>은 <c>"area":2,"runs":5</c> 식.
        /// 값이 바뀌지 않으면 저장소가 false를 돌려주므로 기본값과 같은 값은 넣지 않는다.
        /// </summary>
        protected void SetContainer(string containerKey, string fieldsJson)
            => Assert.That(Store.ApplyAndSaveJson("{\"" + containerKey + "\":{" + fieldsJson + "}}", false, containerKey), Is.True);

        protected long W(int id) => Res.GetRaw(id);

        protected static MvRunOutcome Outcome(long seq, MvRunEnd end, double gold = 0, int souls = 0, int dia = 0, int cpRun = 0,
            IReadOnlyList<IReadOnlyDictionary<int, double>> bars = null, IReadOnlyList<MV.Data.ConsumeData> pending = null)
            => new MvRunOutcome { RunSeq = seq, End = end, Gold = gold, Souls = souls, Dia = dia, CpRun = cpRun, Bars = bars, PendingSettle = pending };

        /// <summary>이번 Area 벽 기록 한 개(Floor 0의 칸 0·2).</summary>
        protected static IReadOnlyList<IReadOnlyDictionary<int, double>> OneBar()
            => new IReadOnlyDictionary<int, double>[] { new Dictionary<int, double> { [0] = 5, [2] = 3.5 } };

        protected static string DayKey(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
