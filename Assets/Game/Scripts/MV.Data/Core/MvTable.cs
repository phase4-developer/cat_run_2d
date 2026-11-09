using System;
using System.Collections.Generic;
using System.Globalization;
using jjevol;

namespace MV.Data
{
    /// <summary>MvTables가 묶는 표 1개. 검증기는 이 인터페이스로 원문 행과 열 정의를 본다.</summary>
    public interface IMvTable
    {
        string TableName { get; }
        MvSchema Schema { get; }
        /// <summary>마지막으로 성공한 로드의 원문 행(<see cref="TSVRow"/>의 조용한 기본값을 믿지 않기 위해).</summary>
        IReadOnlyList<TSVRow> RawRows { get; }
        TableBase Table { get; }
        bool HasId(int id);
        bool HasGroup(int group);
        bool HasKey(string key);
    }

    /// <summary>
    /// 행 공통(07 §7.1.4~7.1.5). <see cref="TableData"/>가 id·sort·grade·nameKey를 읽고, 여기서 key·active를 읽는다.
    /// UnityEngine 타입을 쓰지 않는다(색은 문자열).
    /// </summary>
    public abstract class MvRow : TableData
    {
        public string key { get; private set; }
        /// <summary>active 열이 없거나 비면 1.</summary>
        public bool active { get; private set; }

        protected MvRow(TSVRow row) : base(row) { }

        public override void Init(TSVRow row)
        {
            base.Init(row);
            key = row.GetString("key");
            active = Bool(row, "active", true);
        }

        // ---- 읽기 도우미. 형식 확인은 검증기가 원문으로 한다. ----

        protected static int I(TSVRow r, string c, int d = 0) => r.GetInt(c, d);
        protected static long L(TSVRow r, string c, long d = 0) => r.GetLong(c, d);
        protected static double D(TSVRow r, string c, double d = 0) => r.GetDouble(c, d);
        protected static string S(TSVRow r, string c) => r.GetString(c) ?? string.Empty;

        /// <summary>bool01은 `GetInt != 0`(07 §7.1.2). Framework GetBoolean은 `1`을 못 읽는다.</summary>
        protected static bool Bool(TSVRow r, string c, bool d = false)
        {
            var text = r.Get(c);
            if (string.IsNullOrEmpty(text)) return d;
            return r.GetInt(c, d ? 1 : 0) != 0;
        }

        protected static T E<T>(TSVRow r, string c, T d = default) where T : struct, Enum =>
            ConsumeData.TryParseEnum(r.Get(c), out T value) ? value : d;

        protected static IReadOnlyList<int> Ints(TSVRow r, string c) => r.GetIntList(c);
        protected static IReadOnlyList<double> Doubles(TSVRow r, string c) => r.GetDoubleList(c);
        protected static IReadOnlyList<string> Strs(TSVRow r, string c) => r.GetStringList(c);

        protected static IReadOnlyList<ConsumeData> Consume(TSVRow r, string prefix)
        {
            // 검증기가 형식 오류를 문구로 낸다. 로드는 실패시키지 않고 빈 목록으로 둔다.
            return ConsumeData.TryParse(r.Get(prefix + "Type"), r.Get(prefix + "Id"), r.Get(prefix + "Value"),
                r.Get(prefix + "ValuePerZone"), r.Get(prefix + "ValueRule"), out var list, out _)
                ? list
                : Array.Empty<ConsumeData>();
        }
    }

    internal sealed class RawStore
    {
        List<TSVRow> _rows = new List<TSVRow>();
        public IReadOnlyList<TSVRow> Rows => _rows;
        public void Set(List<TSVRow> rows) => _rows = rows == null ? new List<TSVRow>() : new List<TSVRow>(rows);
    }

    /// <summary>id 표(07 §7.1.5). key·group 사전을 같은 로드에서 만든다(같은 TSV를 두 번 읽지 않음).</summary>
    public class MvTable<T> : IndexedTable<T>, IMvTable where T : MvRow
    {
        readonly Func<TSVRow, T> _parse;
        readonly Func<T, int> _group;
        readonly RawStore _raw = new RawStore();
        readonly Dictionary<string, T> _byKey = new Dictionary<string, T>(StringComparer.Ordinal);
        readonly Dictionary<int, List<T>> _groups = new Dictionary<int, List<T>>();
        readonly List<T> _sorted = new List<T>();

        public MvSchema Schema { get; }
        public IReadOnlyList<TSVRow> RawRows => _raw.Rows;
        public TableBase Table => this;

        public MvTable(MvSchema schema, Func<TSVRow, T> parse, Func<T, int> group = null) : base(schema.Table)
        {
            Schema = schema;
            _parse = parse;
            _group = group;
        }

        protected override T ParseJson(TSVRow data) => _parse(data);

        protected override void InsertData(List<TSVRow> datas)
        {
            base.InsertData(datas);
            _raw.Set(datas);
            Reindex();
        }

        protected override void _DataClear()
        {
            base._DataClear();
            _byKey.Clear();
            _groups.Clear();
            _sorted.Clear();
        }

        protected override void RestoreData(object captured)
        {
            base.RestoreData(captured);
            Reindex();
        }

        void Reindex()
        {
            _byKey.Clear();
            _groups.Clear();
            _sorted.Clear();
            _sorted.AddRange(Dict.Values);
            _sorted.Sort((a, b) => a.sort != b.sort ? a.sort.CompareTo(b.sort) : a.id.CompareTo(b.id));
            var byId = new List<T>(Dict.Values);
            byId.Sort((a, b) => a.id.CompareTo(b.id));
            foreach (var row in byId)
            {
                if (!string.IsNullOrEmpty(row.key) && !_byKey.ContainsKey(row.key)) _byKey.Add(row.key, row);
                if (_group == null) continue;
                int g = _group(row);
                if (!_groups.TryGetValue(g, out var list)) _groups.Add(g, list = new List<T>());
                list.Add(row);
            }
        }

        /// <summary>sort 오름차순, 같으면 id 순.</summary>
        public IReadOnlyList<T> Sorted => _sorted;

        public T ByKey(string key) => key != null && _byKey.TryGetValue(key, out var row) ? row : null;

        /// <summary>묶음 행(id 오름차순). 없으면 빈 목록.</summary>
        public IReadOnlyList<T> GetGroup(int group) =>
            _groups.TryGetValue(group, out var list) ? (IReadOnlyList<T>)list : Array.Empty<T>();

        public IEnumerable<int> Groups => _groups.Keys;

        public bool HasId(int id) => Dict.ContainsKey(id);
        public bool HasGroup(int group) => _groups.ContainsKey(group);
        public bool HasKey(string key) => key != null && _byKey.ContainsKey(key);
    }

    // ---------------- 07_consumeType (id 열 없음, typeId 키) ----------------

    public enum IdKind { none, row, group }
    public enum ValueKind { Int, Double }
    public enum ApplyMul { None, GoldMul }

    public sealed class ConsumeTypeData : MvRow
    {
        public int typeId { get; private set; }
        public string codeName { get; private set; }
        /// <summary>consumeId가 가리키는 표 파일 이름. `-` = id 안 씀.</summary>
        public string idTable { get; private set; }
        public IdKind idKind { get; private set; }
        public ValueKind valueKind { get; private set; }
        public string iconAtlas { get; private set; }
        public string iconColumn { get; private set; }
        public string worldBadge { get; private set; }
        public string gainSound { get; private set; }
        public string gainEffect { get; private set; }
        public ApplyMul applyMul { get; private set; }
        public string settleTo { get; private set; }
        public bool collectMul { get; private set; }

        public ConsumeTypeData(TSVRow row) : base(row) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            typeId = I(r, "typeId");
            codeName = S(r, "codeName");
            idTable = S(r, "idTable");
            idKind = E(r, "idKind", IdKind.none);
            valueKind = E(r, "valueKind", ValueKind.Int);
            iconAtlas = S(r, "iconAtlas");
            iconColumn = S(r, "iconColumn");
            worldBadge = S(r, "worldBadge");
            gainSound = S(r, "gainSound");
            gainEffect = S(r, "gainEffect");
            applyMul = E(r, "applyMul", ApplyMul.None);
            settleTo = S(r, "settleTo");
            collectMul = Bool(r, "collectMul");
        }

        public bool UsesId => !string.IsNullOrEmpty(idTable) && idTable != "-";
        public ConsumeType Type => (ConsumeType)typeId;
        /// <summary>`iconColumn`이 `=이름`이면 고정 스프라이트 이름.</summary>
        public string FixedIcon => iconColumn != null && iconColumn.StartsWith("=") ? iconColumn.Substring(1) : null;
    }

    public sealed class ConsumeTypeTable : KeyedTable<int, ConsumeTypeData>, IMvTable
    {
        public const string Name = "07_consumeType";
        readonly RawStore _raw = new RawStore();

        public MvSchema Schema { get; }
        public IReadOnlyList<TSVRow> RawRows => _raw.Rows;
        public TableBase Table => this;

        public ConsumeTypeTable() : base(Name)
        {
            Schema = new MvSchema(Name).Id("typeId").FreeIdRule().Str("codeName", true).Str("idTable", true).Enum<IdKind>("idKind", true)
                .Str("valueMeaning").Enum<ValueKind>("valueKind", true).Str("iconAtlas").Str("iconColumn")
                .Res("worldBadge", MvAtlas.Theme).Sound("gainSound").Effect("gainEffect").Enum<ApplyMul>("applyMul")
                .Str("settleTo").Bool("collectMul").Bool("active");
        }

        protected override ConsumeTypeData ParseJson(TSVRow json) => new ConsumeTypeData(json);
        protected override int GetKey(ConsumeTypeData data) => data.typeId;

        protected override void InsertData(List<TSVRow> datas)
        {
            base.InsertData(datas);
            _raw.Set(datas);
        }

        public ConsumeTypeData Get(ConsumeType type) => Get((int)type);
        public bool HasId(int id) => Dict.ContainsKey(id);
        public bool HasGroup(int group) => false;
        public bool HasKey(string key) => false;
    }

    // ---------------- 01_wealth (Framework WealthTable) ----------------

    /// <summary>Framework <see cref="WealthTable"/>에 원문 보관만 더한다(07 §7.8.10). ResourceService가 이 객체를 그대로 받는다.</summary>
    public sealed class MvWealthTable : WealthTable, IMvTable
    {
        public const string Name = "01_wealth";
        readonly RawStore _raw = new RawStore();

        public MvSchema Schema { get; }
        public IReadOnlyList<TSVRow> RawRows => _raw.Rows;
        public TableBase Table => this;

        public MvWealthTable() : base(Name)
        {
            Schema = new MvSchema(Name).Id().Str("type", true).Int("sort").Bool("active").Res("icon", MvAtlas.UiIcon)
                .Long("maxValue").Int("chargeShopSectionId");
        }

        protected override void InsertData(List<TSVRow> datas)
        {
            base.InsertData(datas);
            _raw.Set(datas);
        }

        public bool HasId(int id) => Dict.ContainsKey(id);
        public bool HasGroup(int group) => false;
        public bool HasKey(string key) => key != null && GetId(key) != 0;
    }

    /// <summary>기본 아틀라스 이름(07 §7.10.3).</summary>
    public static class MvAtlas
    {
        public const string Theme = "@theme";
        public const string UiIcon = "Atlas_UI_Icon";
        public const string UiCard = "Atlas_UI_Card";
        public const string UiLobby = "Atlas_UI_Lobby";
        public const string UiHud = "Atlas_UI_Hud";
        public const string WorldLobby = "Atlas_World_Lobby";
        /// <summary>Addressables 단독 주소(`Tex_*`, `Sfx_*`).</summary>
        public const string Address = "address";
        /// <summary>아틀라스 이름 자체.</summary>
        public const string AtlasName = "atlas";
    }

    internal static class Inv
    {
        public static readonly CultureInfo C = CultureInfo.InvariantCulture;
    }
}
