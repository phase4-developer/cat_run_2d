using System;
using System.Collections.Generic;
using System.Globalization;

namespace MV.Data
{
    // 3xx 런·세계·배치 표(기획서 07 §7.3.3~7.3.5, §7.4, §7.6).

    public enum CellMode { Hash, Baked }
    public enum CycleMode { Direct, Loop }
    public enum CameraAnchor { Player, World }
    public enum ClimbRowType { Config, WarpStage }

    // ---------------- 300_runConfig (세로 키/값) ----------------

    public sealed class RunConfigData : MvRow
    {
        public const string Table = "300_runConfig";
        /// <summary>unit이 이 값이면 value는 숫자가 아니라 Addressables 주소(= 파일 이름)다.</summary>
        public const string AddressUnit = "address";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Str("value", true).Str("unit");

        /// <summary>원문 문자열. `unit = color`면 색, `/`가 있으면 리스트, 그 밖은 double.</summary>
        public string value { get; private set; }
        public string unit { get; private set; }

        public RunConfigData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            value = S(r, "value");
            unit = S(r, "unit");
        }
    }

    public sealed class RunConfigTable : MvTable<RunConfigData>
    {
        public RunConfigTable() : base(RunConfigData.Schema, r => new RunConfigData(r)) { }

        public bool Has(string key) => ByKey(key) != null;

        public string Raw(string key) => ByKey(key)?.value ?? throw new KeyNotFoundException($"300_runConfig: {key}");

        public double D(string key) => double.Parse(Raw(key), NumberStyles.Float, CultureInfo.InvariantCulture);

        public int I(string key) => (int)Math.Floor(D(key) + 0.5);

        public IReadOnlyList<double> List(string key)
        {
            var parts = Raw(key).Split('/');
            var list = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                list[i] = double.Parse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
            return list;
        }
    }

    // ---------------- 301_zone ----------------

    public sealed class ZoneData : MvRow
    {
        public const string Table = "301_zone";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("sort").Bool("active")
            .Res("mapIcon", MvAtlas.UiLobby).Color("mapCol1").Color("mapCol2")
            .Sound("bgm").Str("weaponLookSet");

        // Zone의 Area 범위는 302_area.zoneId로 계산한다(MvTables.ZoneFirstArea·ZoneLastArea, 옛 areaFrom·areaTo 열 삭제).
        public string mapIcon { get; private set; }
        public string mapCol1 { get; private set; }
        public string mapCol2 { get; private set; }
        public string bgm { get; private set; }
        /// <summary>지팡이 외형 세트 이름(MV-SPEC-02 9-1·6-4, Q-33). 그림이 오기 전까지 모든 Zone이 같은 세트.</summary>
        public string weaponLookSet { get; private set; }

        public ZoneData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            mapIcon = S(r, "mapIcon");
            mapCol1 = S(r, "mapCol1");
            mapCol2 = S(r, "mapCol2");
            bgm = S(r, "bgm");
            weaponLookSet = S(r, "weaponLookSet");
        }
    }

    // ---------------- 302_area ----------------

    public sealed class AreaData : MvRow
    {
        public const string Table = "302_area";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("areaNo", true).Fk("zoneId", ZoneData.Table, true)
            .Int("sort").Bool("active").Fk("themeId", ThemeData.Table, true).Int("hp", true).Int("needSouls", true)
            .Floats("sgateM").Int("startGold")
            .FkGroup("floorGroup", AreaFloorData.Table).Int("fieldSeedArea").Sound("bgm").Res("mapIcon", MvAtlas.UiLobby)
            .Res("bg", MvAtlas.Address).Res("stageMap", MvAtlas.Address, true).Consume("clearReward").Consume("firstClearReward");

        public int areaNo { get; private set; }
        public int zoneId { get; private set; }
        public int themeId { get; private set; }
        public int hp { get; private set; }
        public int needSouls { get; private set; }
        /// <summary>계단 게이트 1회 충전 필요 피해 M, 계단 Lv 1부터(MV-SPEC-02 6-2 표, Q-06·Q-19). 칸 수 = Lv 상한. 런 적용은 MV-RUN-SPEC2.</summary>
        public IReadOnlyList<double> sgateM { get; private set; }
        /// <summary>이 Area 시작 골드(MV-SPEC-02 11-1, Q-31: Area 2부터 150). 정산 적용은 MV-RUN-SPEC2.</summary>
        public int startGold { get; private set; }
        /// <summary>빈칸 = areaNo.</summary>
        public int floorGroup { get; private set; }
        /// <summary>빈칸 = areaNo − 1(클램프 전 A).</summary>
        public int fieldSeedArea { get; private set; }
        public string bgm { get; private set; }
        public string mapIcon { get; private set; }
        public string bg { get; private set; }
        /// <summary>런 월드 스테이지 맵 프리팹 주소(`Local/BG/Prefab/Stage{n}/Stage{n}`, 노션 「신작 배경 리소스 관리」). 맵이 아직 없는 지역은 있는 맵을 다시 쓴다(사용자 카드 결정 2026-11-13).</summary>
        public string stageMap { get; private set; }
        public IReadOnlyList<ConsumeData> clearReward { get; private set; }
        public IReadOnlyList<ConsumeData> firstClearReward { get; private set; }

        public AreaData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            areaNo = I(r, "areaNo");
            zoneId = I(r, "zoneId");
            themeId = I(r, "themeId");
            hp = I(r, "hp");
            needSouls = I(r, "needSouls");
            sgateM = Doubles(r, "sgateM");
            startGold = I(r, "startGold");
            floorGroup = string.IsNullOrEmpty(r.Get("floorGroup")) ? areaNo : I(r, "floorGroup");
            fieldSeedArea = string.IsNullOrEmpty(r.Get("fieldSeedArea")) ? areaNo - 1 : I(r, "fieldSeedArea");
            bgm = S(r, "bgm");
            mapIcon = S(r, "mapIcon");
            bg = S(r, "bg");
            stageMap = S(r, "stageMap");
            clearReward = Consume(r, "clearReward");
            firstClearReward = Consume(r, "firstClearReward");
        }
    }

    // ---------------- 303_areaFloor ----------------

    public sealed class AreaFloorData : MvRow
    {
        public const string Table = "303_areaFloor";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Group().Int("floor", true).Long("h0", true)
            .Long("h1", true).Int("rows", true).Int("souls", true).Enum<CellMode>("cellMode").Ints("soulCells")
            .Ints("soulAmts").Ints("diaCells");

        public int group { get; private set; }
        public int floor { get; private set; }
        public long h0 { get; private set; }
        public long h1 { get; private set; }
        public int rows { get; private set; }
        public int souls { get; private set; }
        public CellMode cellMode { get; private set; }
        public IReadOnlyList<int> soulCells { get; private set; }
        public IReadOnlyList<int> soulAmts { get; private set; }
        public IReadOnlyList<int> diaCells { get; private set; }

        public AreaFloorData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            group = I(r, "group");
            floor = I(r, "floor");
            h0 = L(r, "h0");
            h1 = L(r, "h1");
            rows = I(r, "rows");
            souls = I(r, "souls");
            cellMode = E(r, "cellMode", CellMode.Hash);
            soulCells = Ints(r, "soulCells");
            soulAmts = Ints(r, "soulAmts");
            diaCells = Ints(r, "diaCells");
        }
    }

    // ---------------- 305_stageObject ----------------

    /// <summary>스테이지 오브젝트가 고르는 보상 종류(105 wall의 칸 보상). Any = 보상과 상관없음.</summary>
    public enum StageObjectReward { Any, Gold, Soul, Dia }

    /// <summary>
    /// 스테이지(맵)마다 다른 오브젝트 프리팹(노션 「신작 배경 리소스 관리」 「스테이지 개별 디자인 오브젝트」, 사용자 카드 결정 2026-11-13).
    /// 행 = 맵 주소(`302_area.stageMap`) + 105 종류 key + 보상 종류 → 프리팹 주소. 고를 때 이 표가 `105_objectKind.prefab`보다 먼저다.
    /// </summary>
    public sealed class StageObjectData : MvRow
    {
        public const string Table = "305_stageObject";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Str("stageMap", true).FkKey("kind", ObjectKindData.Table, true)
            .Enum<StageObjectReward>("reward").Res("prefab", MvAtlas.Address, true);

        public string stageMap { get; private set; }
        public string kind { get; private set; }
        public StageObjectReward reward { get; private set; }
        public string prefab { get; private set; }

        public StageObjectData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            stageMap = S(r, "stageMap");
            kind = S(r, "kind");
            reward = E(r, "reward", StageObjectReward.Any);
            prefab = S(r, "prefab");
        }

        /// <summary>맵·종류·보상에 맞는 행의 프리팹. 보상이 맞는 행이 Any 행보다 먼저. 없으면 null.</summary>
        public static string Find(IReadOnlyList<StageObjectData> rows, string stageMap, string kind, StageObjectReward reward)
        {
            if (rows == null || string.IsNullOrEmpty(stageMap) || string.IsNullOrEmpty(kind)) return null;
            string any = null;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.stageMap != stageMap || row.kind != kind) continue;
                if (row.reward == reward && reward != StageObjectReward.Any) return row.prefab;
                if (row.reward == StageObjectReward.Any && any == null) any = row.prefab;
            }
            return any;
        }
    }

    // ---------------- 304_theme ----------------

    public sealed class ThemeData : MvRow
    {
        public const string Table = "304_theme";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("sort").Bool("active")
            .Res("worldAtlas", MvAtlas.AtlasName, true).Strs("roadsideProps").Str("lineKind")
            .Color("skyTop").Color("skyBot").Color("fog").Color("grass").Color("grass2").Color("road").Color("road2")
            .Color("stone").Color("hillFar").Color("hillNear").Colors("walls").Colors("roofs").Color("wallTop").Color("door")
            .Str("prop").Str("leafSet").Str("deco").Color("tint").Float("night").Sound("bgm");

        public string worldAtlas { get; private set; }
        public IReadOnlyList<string> roadsideProps { get; private set; }
        public string lineKind { get; private set; }
        public string skyTop { get; private set; }
        public string skyBot { get; private set; }
        public string fog { get; private set; }
        public string grass { get; private set; }
        public string grass2 { get; private set; }
        public string road { get; private set; }
        public string road2 { get; private set; }
        public string stone { get; private set; }
        public string hillFar { get; private set; }
        public string hillNear { get; private set; }
        public IReadOnlyList<string> walls { get; private set; }
        public IReadOnlyList<string> roofs { get; private set; }
        public string wallTop { get; private set; }
        public string door { get; private set; }
        public string prop { get; private set; }
        public string leafSet { get; private set; }
        public string deco { get; private set; }
        public string tint { get; private set; }
        public double night { get; private set; }
        public string bgm { get; private set; }

        public ThemeData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            worldAtlas = S(r, "worldAtlas");
            roadsideProps = Strs(r, "roadsideProps");
            lineKind = S(r, "lineKind");
            skyTop = S(r, "skyTop");
            skyBot = S(r, "skyBot");
            fog = S(r, "fog");
            grass = S(r, "grass");
            grass2 = S(r, "grass2");
            road = S(r, "road");
            road2 = S(r, "road2");
            stone = S(r, "stone");
            hillFar = S(r, "hillFar");
            hillNear = S(r, "hillNear");
            walls = Strs(r, "walls");
            roofs = Strs(r, "roofs");
            wallTop = S(r, "wallTop");
            door = S(r, "door");
            prop = S(r, "prop");
            leafSet = S(r, "leafSet");
            deco = S(r, "deco");
            tint = S(r, "tint");
            night = D(r, "night");
            bgm = S(r, "bgm");
        }
    }

    // ---------------- 310_template ----------------

    public sealed class TemplateData : MvRow
    {
        public const string Table = "310_template";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("sort").Bool("active").Float("endZ", true)
            .Float("midFromZ").Float("midToZ");

        public double endZ { get; private set; }
        /// <summary>중간 구역(감속 <c>RUN_SPEED_MID</c>) 시작·끝 z(MV-SPEC-02 4-1, Q-15). 빈칸 = 감속 구간 없음. 위치는 원작 확인 뒤.</summary>
        public double? midFromZ { get; private set; }
        public double? midToZ { get; private set; }

        public TemplateData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            endZ = D(r, "endZ");
            midFromZ = string.IsNullOrEmpty(r.Get("midFromZ")) ? (double?)null : D(r, "midFromZ");
            midToZ = string.IsNullOrEmpty(r.Get("midToZ")) ? (double?)null : D(r, "midToZ");
        }
    }

    // ---------------- 311_templateObject ----------------

    public sealed class TemplateObjectData : MvRow
    {
        public const string Table = "311_templateObject";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Fk("templateId", TemplateData.Table, true)
            .Int("seq", true).Fk("kindId", ObjectKindData.Table, true).Float("x", true).Float("z", true)
            .FkGroup("groupId", SpawnGroupData.Table).Bool("active").Fk("hpRuleId", HpRuleData.Table)
            .Consume("reward").RunConsume();

        public int templateId { get; private set; }
        public int seq { get; private set; }
        public int kindId { get; private set; }
        public double x { get; private set; }
        public double z { get; private set; }
        public int groupId { get; private set; }
        /// <summary>0 = 종류 기본.</summary>
        public int hpRuleId { get; private set; }
        /// <summary>비면 종류 기본(덮어쓰기 없음).</summary>
        public IReadOnlyList<ConsumeData> reward { get; private set; }

        public TemplateObjectData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            templateId = I(r, "templateId");
            seq = I(r, "seq");
            kindId = I(r, "kindId");
            x = D(r, "x");
            z = D(r, "z");
            groupId = I(r, "groupId");
            hpRuleId = I(r, "hpRuleId");
            reward = Consume(r, "reward");
        }
    }

    public sealed class TemplateObjectTable : MvTable<TemplateObjectData>
    {
        readonly Dictionary<int, List<TemplateObjectData>> _byZ = new Dictionary<int, List<TemplateObjectData>>();

        public TemplateObjectTable() : base(TemplateObjectData.Schema, r => new TemplateObjectData(r), d => d.templateId) { }

        /// <summary>템플릿 행을 z 오름차순, 같은 z는 seq 순으로(07 §7.6.2). <see cref="MvTables.BuildIndexes"/>에서 만든다.</summary>
        public IReadOnlyList<TemplateObjectData> InZOrder(int templateId) =>
            _byZ.TryGetValue(templateId, out var list) ? (IReadOnlyList<TemplateObjectData>)list : Array.Empty<TemplateObjectData>();

        internal void BuildZOrder()
        {
            _byZ.Clear();
            foreach (int g in Groups)
            {
                var list = new List<TemplateObjectData>(GetGroup(g));
                list.Sort((a, b) => a.z != b.z ? a.z.CompareTo(b.z) : a.seq.CompareTo(b.seq));
                _byZ.Add(g, list);
            }
        }
    }

    // ---------------- 312_templateCycle ----------------

    public sealed class TemplateCycleData : MvRow
    {
        public const string Table = "312_templateCycle";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("cycleGroup", true).Int("levelFrom", true)
            .Int("levelTo", true).FkList("templates", TemplateData.Table, true).Enum<CycleMode>("mode", true);

        public int cycleGroup { get; private set; }
        public int levelFrom { get; private set; }
        /// <summary>0 = 끝없음.</summary>
        public int levelTo { get; private set; }
        public IReadOnlyList<int> templates { get; private set; }
        public CycleMode mode { get; private set; }

        public TemplateCycleData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            cycleGroup = I(r, "cycleGroup");
            levelFrom = I(r, "levelFrom");
            levelTo = I(r, "levelTo");
            templates = Ints(r, "templates");
            mode = E(r, "mode", CycleMode.Direct);
        }

        public bool Covers(int level) => level >= levelFrom && (levelTo == 0 || level <= levelTo);

        public int TemplateFor(int level)
        {
            int i = level - levelFrom;
            if (mode == CycleMode.Loop) i %= templates.Count;
            return templates[Math.Min(i, templates.Count - 1)];
        }
    }

    public sealed class TemplateCycleTable : MvTable<TemplateCycleData>
    {
        public TemplateCycleTable() : base(TemplateCycleData.Schema, r => new TemplateCycleData(r), d => d.cycleGroup) { }

        /// <summary>정본 `tplFor`(L857): Level → 310 id. 덮는 행이 없으면 0.</summary>
        public int TemplateFor(int cycleGroup, int level)
        {
            foreach (var row in GetGroup(cycleGroup))
                if (row.Covers(level)) return row.TemplateFor(level);
            return 0;
        }
    }

    // ---------------- 313_fieldGen ----------------

    public sealed class FieldGenData : MvRow
    {
        public const string Table = "313_fieldGen";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key()
            .Int("lanes", true).Float("laneX0", true).Float("laneStep", true).Float("rowStep", true)
            .Fk("wallKindId", ObjectKindData.Table, true).Fk("archKindId", ObjectKindData.Table, true)
            .Fk("archGoalKindId", ObjectKindData.Table, true).Int("soulPerRows", true).Int("soulMinIdx", true)
            .Float("soulHashStep", true).Int("soulTryMax", true).Float("diaHashStep", true).Float("diaHashAdd", true)
            .Float("diaChance", true).Int("seedA", true).Int("seedF", true).Int("seedC", true).Float("goldBase", true)
            .Float("goldPerZone", true).Float("goldPerRow", true).Int("soulRewardType", true).Int("diaRewardType", true)
            .Enum<RewardValueRule>("diaRewardRule", true).Int("goldRewardType", true).Int("bonusRows", true)
            .Floats("bonusLaneX", true).Float("bonusZ0", true).Float("bonusRowStep", true).Int("bonusGemRow", true)
            .Int("bonusGemLane", true).Fk("bonusCoinKindId", ObjectKindData.Table, true)
            .Fk("bonusGemKindId", ObjectKindData.Table, true).Float("bonusToArch", true).Float("archToField", true)
            .Float("fieldToNext", true);

        public int lanes { get; private set; }
        public double laneX0 { get; private set; }
        public double laneStep { get; private set; }
        public double rowStep { get; private set; }
        public int wallKindId { get; private set; }
        public int archKindId { get; private set; }
        public int archGoalKindId { get; private set; }
        public int soulPerRows { get; private set; }
        public int soulMinIdx { get; private set; }
        public double soulHashStep { get; private set; }
        public int soulTryMax { get; private set; }
        public double diaHashStep { get; private set; }
        public double diaHashAdd { get; private set; }
        public double diaChance { get; private set; }
        public int seedA { get; private set; }
        public int seedF { get; private set; }
        public int seedC { get; private set; }
        public double goldBase { get; private set; }
        public double goldPerZone { get; private set; }
        public double goldPerRow { get; private set; }
        public int soulRewardType { get; private set; }
        public int diaRewardType { get; private set; }
        public RewardValueRule diaRewardRule { get; private set; }
        public int goldRewardType { get; private set; }
        public int bonusRows { get; private set; }
        public IReadOnlyList<double> bonusLaneX { get; private set; }
        public double bonusZ0 { get; private set; }
        public double bonusRowStep { get; private set; }
        public int bonusGemRow { get; private set; }
        public int bonusGemLane { get; private set; }
        public int bonusCoinKindId { get; private set; }
        public int bonusGemKindId { get; private set; }
        public double bonusToArch { get; private set; }
        public double archToField { get; private set; }
        public double fieldToNext { get; private set; }

        public FieldGenData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            lanes = I(r, "lanes");
            laneX0 = D(r, "laneX0");
            laneStep = D(r, "laneStep");
            rowStep = D(r, "rowStep");
            wallKindId = I(r, "wallKindId");
            archKindId = I(r, "archKindId");
            archGoalKindId = I(r, "archGoalKindId");
            soulPerRows = I(r, "soulPerRows");
            soulMinIdx = I(r, "soulMinIdx");
            soulHashStep = D(r, "soulHashStep");
            soulTryMax = I(r, "soulTryMax");
            diaHashStep = D(r, "diaHashStep");
            diaHashAdd = D(r, "diaHashAdd");
            diaChance = D(r, "diaChance");
            seedA = I(r, "seedA");
            seedF = I(r, "seedF");
            seedC = I(r, "seedC");
            goldBase = D(r, "goldBase");
            goldPerZone = D(r, "goldPerZone");
            goldPerRow = D(r, "goldPerRow");
            soulRewardType = I(r, "soulRewardType");
            diaRewardType = I(r, "diaRewardType");
            diaRewardRule = E(r, "diaRewardRule", RewardValueRule.DiaCap);
            goldRewardType = I(r, "goldRewardType");
            bonusRows = I(r, "bonusRows");
            bonusLaneX = Doubles(r, "bonusLaneX");
            bonusZ0 = D(r, "bonusZ0");
            bonusRowStep = D(r, "bonusRowStep");
            bonusGemRow = I(r, "bonusGemRow");
            bonusGemLane = I(r, "bonusGemLane");
            bonusCoinKindId = I(r, "bonusCoinKindId");
            bonusGemKindId = I(r, "bonusGemKindId");
            bonusToArch = D(r, "bonusToArch");
            archToField = D(r, "archToField");
            fieldToNext = D(r, "fieldToNext");
        }
    }

    // ---------------- 314_spawnGroup ----------------

    public sealed class SpawnGroupData : MvRow
    {
        public const string Table = "314_spawnGroup";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Group().Str("key", true).Int("idx", true)
            .Fk("kindId", ObjectKindData.Table).Float("dx", true).Float("dz", true);

        public int group { get; private set; }
        public int idx { get; private set; }
        /// <summary>분신 그룹은 0.</summary>
        public int kindId { get; private set; }
        public double dx { get; private set; }
        /// <summary>정본 +6을 합쳐 저장한 값.</summary>
        public double dz { get; private set; }

        public SpawnGroupData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            group = I(r, "group");
            idx = I(r, "idx");
            kindId = I(r, "kindId");
            dx = D(r, "dx");
            dz = D(r, "dz");
        }
    }

    // ---------------- 320_camera ----------------

    public sealed class CameraData : MvRow
    {
        public const string Table = "320_camera";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Enum<CameraAnchor>("anchor", true)
            .Float("posXMul").Float("posY").Float("posZ").Float("lookXMul").Float("lookY").Float("lookZ").Float("posLerp")
            .Bool("snap").Float("liftLerp").Float("blendLerp").Float("fovV", true).Float("near", true).Float("far", true)
            .Float("posX").Float("lookX").Float("turnSec");

        public CameraAnchor anchor { get; private set; }
        public double posXMul { get; private set; }
        public double posY { get; private set; }
        public double posZ { get; private set; }
        public double lookXMul { get; private set; }
        public double lookY { get; private set; }
        public double lookZ { get; private set; }
        public double posLerp { get; private set; }
        public bool snap { get; private set; }
        public double liftLerp { get; private set; }
        public double blendLerp { get; private set; }
        public double fovV { get; private set; }
        public double near { get; private set; }
        public double far { get; private set; }
        /// <summary>x 더하기(m): 목표 x = 기준 x·posXMul + posX. 계단 옆 시점(side)은 고양이 오른쪽 거리.</summary>
        public double posX { get; private set; }
        public double lookX { get; private set; }
        /// <summary>이 시점으로 돌아 들어가는 시간(초, side 행). 0이면 즉시.</summary>
        public double turnSec { get; private set; }

        public CameraData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            anchor = E(r, "anchor", CameraAnchor.Player);
            posXMul = D(r, "posXMul");
            posY = D(r, "posY");
            posZ = D(r, "posZ");
            lookXMul = D(r, "lookXMul");
            lookY = D(r, "lookY");
            lookZ = D(r, "lookZ");
            posLerp = D(r, "posLerp");
            snap = Bool(r, "snap");
            liftLerp = D(r, "liftLerp");
            blendLerp = D(r, "blendLerp");
            fovV = D(r, "fovV");
            near = D(r, "near");
            far = D(r, "far");
            posX = D(r, "posX");
            lookX = D(r, "lookX");
            turnSec = D(r, "turnSec");
        }
    }

    // ---------------- 330_climb ----------------

    public sealed class ClimbData : MvRow
    {
        public const string Table = "330_climb";

        static readonly string[] s_configColumns =
        {
            "lap", "maxCards", "lapTier", "kMax", "stepH", "stepD", "stepW", "catLift", "z0Gap", "shiftFrom",
            "shiftExtra", "landGap", "tBase", "tPerStep", "riseAhead", "dropSec",
            "dropH", "tickEvery", "rlEndBase", "rlEndStep", "rlFlipSec", "rlSlowWin", "rlSlowK", "rlFlyDelay", "rlFlyMs",
            "rlEndPad",
            // 새 HTML 계단 정산(2026-11-13, 연출 정본): k = min(kMax, n), T = tBase + tPerStep·k, sideLead 뒤 일정 속도로 오름,
            // 칸은 pos + riseAhead 앞까지 보임, 끝나고 sideOut 멈춤, 오르는 동안 x를 centerK로 가운데로 당김.
            "sideLead", "sideOut", "centerK",
            // stepH·stepD·stepW = 계단 칸 프리팹(stepPrefab) 한 블록의 높이·깊이·폭(디자이너 리소스 기준, 2026-11-13). catLift = 칸 위 고양이 높이(stepH 배수).
        };

        /// <summary>설정 행(rowType = Config)이 채워야 하는 열.</summary>
        public static IReadOnlyList<string> ConfigColumns => s_configColumns;

        public static readonly MvSchema Schema = BuildSchema();

        static MvSchema BuildSchema()
        {
            var s = new MvSchema(Table).Id().Enum<ClimbRowType>("rowType", true).Key(required: false);
            foreach (var c in s_configColumns) s.Float(c);
            return s.Res("stepPrefab", MvAtlas.Address).Int("at").Color("accent").Color("sky1").Color("sky2").Color("sky3").Float("night")
                .Effect("enterEffect").Sound("enterSound");
        }

        readonly Dictionary<string, double> _config = new Dictionary<string, double>(StringComparer.Ordinal);

        public ClimbRowType rowType { get; private set; }
        /// <summary>계단 칸 한 블록 프리팹 주소(설정 행만, 예 `Obj_ClimbStep`). 칸 하나 = 블록을 높이만큼 쌓은 기둥.</summary>
        public string stepPrefab { get; private set; }
        public int at { get; private set; }
        public string accent { get; private set; }
        public string sky1 { get; private set; }
        public string sky2 { get; private set; }
        public string sky3 { get; private set; }
        public double night { get; private set; }
        public string enterEffect { get; private set; }
        public string enterSound { get; private set; }

        public ClimbData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            rowType = E(r, "rowType", ClimbRowType.Config);
            _config.Clear();
            if (rowType == ClimbRowType.Config)
                foreach (var c in s_configColumns)
                    _config[c] = D(r, c);
            stepPrefab = S(r, "stepPrefab");
            at = I(r, "at");
            accent = S(r, "accent");
            sky1 = S(r, "sky1");
            sky2 = S(r, "sky2");
            sky3 = S(r, "sky3");
            night = D(r, "night");
            enterEffect = S(r, "enterEffect");
            enterSound = S(r, "enterSound");
        }

        /// <summary>설정 행 값(`lap`, `rlFlyMs` …).</summary>
        public double this[string column] => _config.TryGetValue(column, out var v) ? v : throw new KeyNotFoundException($"330_climb {id}: {column}");
    }

    public sealed class ClimbTable : MvTable<ClimbData>
    {
        public ClimbTable() : base(ClimbData.Schema, r => new ClimbData(r)) { }

        public ClimbData Config(string key = "default")
        {
            var row = ByKey(key);
            return row != null && row.rowType == ClimbRowType.Config ? row : null;
        }

        /// <summary>워프 단계 행(at 오름차순).</summary>
        public IReadOnlyList<ClimbData> WarpStages()
        {
            var list = new List<ClimbData>();
            foreach (var row in Datas)
                if (row.rowType == ClimbRowType.WarpStage) list.Add(row);
            list.Sort((a, b) => a.at.CompareTo(b.at));
            return list;
        }
    }
}
