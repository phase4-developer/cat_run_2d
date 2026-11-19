using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MV.Data;
using MV.Meta;

namespace MV.UI.Tests
{
    /// <summary>메타 화면 시험 공통: TSV 정본 표(메모리에서만 행 추가) + 호출을 세는 가짜 메타 입구.</summary>
    internal static class MetaUiTestKit
    {
        static MvTables _base;

        public static MvTables Base()
        {
            if (_base != null) return _base;
            var t = new MvTables();
            t.LoadAll(new MvTsvDirectorySource(MvTableLoader.TsvDirectory));
            return _base = t;
        }

        /// <summary>
        /// <paramref name="table"/>의 마지막 행을 복사해 한 행 더한 표(파일은 바꾸지 않는다). id·areaNo는 최댓값 + 1, sort는 최댓값 + 1, key는 뒤에 「_t」.
        /// </summary>
        public static MvTables WithExtraRow(string table)
        {
            string dir = MvTableLoader.TsvDirectory;
            string text = File.ReadAllText(Path.Combine(dir, table + ".txt"), new UTF8Encoding(false));
            var lines = text.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).ToList();
            var head = lines[0].Split('\t');
            var rows = lines.Skip(1).Select(l => l.Split('\t')).ToList();
            var add = (string[])rows[rows.Count - 1].Clone();
            for (int c = 0; c < head.Length && c < add.Length; c++)
            {
                switch (head[c])
                {
                    case "id":
                    case "areaNo":
                    case "sort":
                        add[c] = (rows.Max(r => long.TryParse(r[c], out var v) ? v : 0) + 1).ToString();
                        break;
                    case "key":
                        add[c] = add[c] + "_t";
                        break;
                    case "showInMenu": // 113_god: 마지막 행(11·12번)은 메뉴에 숨김이라, 더한 행은 보이게 둔다.
                        add[c] = "1";
                        break;
                }
            }
            var src = new MvMemoryTableSource(new MvTsvDirectorySource(dir))
                .Set(table, string.Join("\n", lines) + "\n" + string.Join("\t", add) + "\n");
            var t = new MvTables();
            t.LoadAll(src);
            return t;
        }

        public static MetaPanelPresenter Make(string screen)
        {
            switch (screen)
            {
                case "god": return new GodSelectPresenter();
                case "area": return new AreaUpgradePresenter();
                case "rebirth": return new RebirthPresenter();
                case "unlock": return new FeatureUnlockPresenter();
                case "debug": return new DebugPanelPresenter();
                default: throw new ArgumentException(screen);
            }
        }
    }

    /// <summary>호출 수를 세는 가짜 메타 입구.</summary>
    internal sealed class FakeMetaPort : IMetaUiPort
    {
        public bool Ready { get; set; } = true;
        public MvTables Tables { get; set; }
        public int AreaIndex { get; set; }
        public int Cp { get; set; }
        public long Wealth(int wealthId) => 1000;

        public bool CanBuyResult = true;
        public int BuyCalls;
        public (MvGrowth growth, string key) LastBuy;
        public int SelectGodCalls;
        public string LastGod;
        public readonly List<string> Seen = new List<string>();
        public int PressRebirthCalls;
        public readonly List<(string name, string key, long amount)> Debug = new List<(string, string, long)>();
        public List<string> Pending = new List<string>();

        public FakeMetaPort(MvTables tables) { Tables = tables; }

        /// <summary>키별 잠김 필요 부활 레벨(없으면 0). <see cref="RebirthLevel"/>은 0.</summary>
        public readonly Dictionary<string, int> NeedRebirthByKey = new Dictionary<string, int>();
        /// <summary>최대 단계로 볼 키.</summary>
        public readonly HashSet<string> MaxKeys = new HashSet<string>();

        /// <summary>키별 현재 레벨(없으면 0).</summary>
        public readonly Dictionary<string, int> LevelByKey = new Dictionary<string, int>();

        public int Level(MvGrowth growth, string key) => LevelByKey.TryGetValue(key, out int lv) ? lv : 0;
        public bool IsMax(MvGrowth growth, string key) => MaxKeys.Contains(key);
        public int NeedRebirth(MvGrowth growth, string key) => NeedRebirthByKey.TryGetValue(key, out int n) ? n : 0;
        public IReadOnlyList<ConsumeData> NextPrice(MvGrowth growth, string key) => new[] { new ConsumeData(ConsumeType.Wealth, 1001002, 40) };
        public bool CanBuy(MvGrowth growth, string key) => CanBuyResult;
        public MetaResult Buy(MvGrowth growth, string key) { BuyCalls++; LastBuy = (growth, key); return MetaResult.Success(); }

        public string SelectedGod { get; set; } = "thunder";
        public bool GodUnlocked(string key) => true;
        public MetaResult SelectGod(string key) { SelectGodCalls++; LastGod = key; return MetaResult.Success(); }

        public int RebirthLevel { get; set; }
        public int RebirthPending { get; set; } = 10;
        public bool RebirthOpen { get; set; } = true;
        public long RebirthCooldownLeftMs { get; set; }
        public bool RebirthArmed => false;
        public MetaResult? PressRebirth() { PressRebirthCalls++; return null; }
        public int RebirthNowCalls;
        public MetaResult RebirthNow() { RebirthNowCalls++; return MetaResult.Success(RebirthPending); }
        public long RebirthCooldownTotalMs { get; set; } = 6L * 3600L * 1000L;
        /// <summary>캣닢 스킵 가격(쿨타임이 있으면 이 값).</summary>
        public long SkipDiaCost = 180;
        public IReadOnlyList<ConsumeData> RebirthSkipDiaPrice => RebirthCooldownLeftMs > 0
            ? new[] { new ConsumeData(ConsumeType.Wealth, 1001002, SkipDiaCost) } : System.Array.Empty<ConsumeData>();
        public int SkipDiaCalls;
        public MetaResult SkipRebirthCooldownDia() { SkipDiaCalls++; RebirthCooldownLeftMs = 0; return MetaResult.Success(SkipDiaCost); }
        public MvUnlockProgress UnlockProgress { get; set; }
        /// <summary>해금 단계를 정해 둔다(없으면 열림/잠김).</summary>
        public MvUnlockState? UnlockStateOverride;
        public MvUnlockState UnlockState(string key) => UnlockStateOverride ?? (IsOpen(key) ? MvUnlockState.Open : MvUnlockState.Locked);

        public int AdsLeft(string key) => 3;
        public void WatchAdForReward(string key, Action<MetaResult> done) => done?.Invoke(MetaResult.Success());

        public bool IsOpen(string key) => false;
        public IReadOnlyList<string> PendingUnlockShows() => Pending;
        public MetaResult MarkUnlockSeen(string key) { Seen.Add(key); return MetaResult.Success(); }

        public MetaResult SendDebug(string requestName, string key = null, long amount = 0)
        {
            Debug.Add((requestName, key, amount));
            return MetaResult.Success();
        }

        public IMvAdService Ads => null;
    }
}
