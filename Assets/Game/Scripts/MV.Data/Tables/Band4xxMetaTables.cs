using System.Collections.Generic;

namespace MV.Data
{
    // 4xx 메타 시스템 표(기획서 07 §7.9.2, 열·행 정본 08 §8.5.0~8.5.11, §8.3.5).
    // 01_wealth id는 저장소 규칙(D-80, 1001NNN)으로 적는다: 08의 `1 / 2 / v` = priceType 1, priceId 1001002.

    /// <summary>메타 능력치 7종(08 §8.5.0). 표에는 이름 문자열로 적는다. 값은 % 단위로 합산해 RunModifiers로 넘긴다.</summary>
    public enum MetaStat { Dmg, FireRate, Range, CritDmg, Gold, CritChance, DoubleChance }

    /// <summary>450_friend.skillTrigger(08 §8.5.4). 표에는 숫자로 적는다.</summary>
    public enum FriendSkillTrigger { None = 0, Hits = 1, Chance = 2, Seconds = 3 }

    /// <summary>480_blessing.effectType(08 §8.5.9). 표에는 숫자로 적는다.</summary>
    public enum BlessingEffectType { None = 0, Rebirth = 1, Daily = 2, Stamina = 3, Stat = 4 }

    // ---------------- 430_pet ----------------

    public sealed class PetData : MvRow
    {
        public const string Table = "430_pet";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("rarity", true).Enum<MetaStat>("statKey", true)
            .Float("value", true).Int("skillEveryMs", true).Float("skillDmgPct", true).Int("skillTargets", true).Float("skillLevelPct")
            .FkGroup("levelPriceGroup", LevelPriceData.Table, true).Res("icon", MvAtlas.UiIcon).Res("sprite", MvAtlas.WorldPet).Bool("active");

        /// <summary>0~4.</summary>
        public int rarity { get; private set; }
        public MetaStat statKey { get; private set; }
        /// <summary>Lv1 보너스 %. 레벨 보너스는 `value × (1 + pet.levelPct/100 × (레벨−1))`.</summary>
        public double value { get; private set; }
        public int skillEveryMs { get; private set; }
        public double skillDmgPct { get; private set; }
        public int skillTargets { get; private set; }
        /// <summary>레벨당 피해 %p.</summary>
        public double skillLevelPct { get; private set; }
        public int levelPriceGroup { get; private set; }
        public string icon { get; private set; }
        /// <summary>동행 그림. 기본 아틀라스가 07 §7.10.3에 없어 문자열로 둔다.</summary>
        public string sprite { get; private set; }

        public PetData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            rarity = I(r, "rarity");
            statKey = E(r, "statKey", MetaStat.Dmg);
            value = D(r, "value");
            skillEveryMs = I(r, "skillEveryMs");
            skillDmgPct = D(r, "skillDmgPct");
            skillTargets = I(r, "skillTargets", 1);
            skillLevelPct = D(r, "skillLevelPct");
            levelPriceGroup = I(r, "levelPriceGroup");
            icon = S(r, "icon");
            sprite = S(r, "sprite");
        }

        /// <summary>자동 공격 피해 % = skillDmgPct + skillLevelPct × (레벨−1).</summary>
        public double SkillPctAt(int level) => skillDmgPct + skillLevelPct * (level - 1);
    }

    // ---------------- 431_petBuild ----------------

    public sealed class PetBuildData : MvRow
    {
        public const string Table = "431_petBuild";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("tier", true).Int("tierRate", true)
            .Enum<MetaStat>("statKey", true).Int("valueMin", true).Int("valueMax", true);

        /// <summary>0~3.</summary>
        public int tier { get; private set; }
        /// <summary>tier 선택 비중(10000 기준). 같은 tier 행은 같은 값.</summary>
        public int tierRate { get; private set; }
        public MetaStat statKey { get; private set; }
        public int valueMin { get; private set; }
        public int valueMax { get; private set; }

        public PetBuildData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            tier = I(r, "tier");
            tierRate = I(r, "tierRate");
            statKey = E(r, "statKey", MetaStat.Dmg);
            valueMin = I(r, "valueMin");
            valueMax = I(r, "valueMax");
        }
    }

    // ---------------- 440_research ----------------

    public sealed class ResearchData : MvRow
    {
        public const string Table = "440_research";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Group().Int("level", true).Res("icon", MvAtlas.UiIcon)
            .Enum<MetaStat>("statType", true).Float("statValue", true).Consume("price").Int("seconds", true).Bool("active");

        /// <summary>계열(1~5). 계열마다 타이머가 따로다.</summary>
        public int group { get; private set; }
        public int level { get; private set; }
        public string icon { get; private set; }
        public MetaStat statType { get; private set; }
        /// <summary>이 단계까지의 누적 %.</summary>
        public double statValue { get; private set; }
        public IReadOnlyList<ConsumeData> price { get; private set; }
        public int seconds { get; private set; }

        public ResearchData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            group = I(r, "group");
            level = I(r, "level");
            icon = S(r, "icon");
            statType = E(r, "statType", MetaStat.Dmg);
            statValue = D(r, "statValue");
            price = Consume(r, "price");
            seconds = I(r, "seconds");
        }
    }

    public sealed class ResearchTable : MvTable<ResearchData>
    {
        public ResearchTable() : base(ResearchData.Schema, r => new ResearchData(r), d => d.group) { }

        /// <summary>계열의 그 단계 행. 없으면 null.</summary>
        public ResearchData Get(int group, int level)
        {
            foreach (var row in GetGroup(group))
                if (row.level == level) return row;
            return null;
        }
    }

    // ---------------- 450_friend ----------------

    public sealed class FriendData : MvRow
    {
        public const string Table = "450_friend";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("rarity", true).Enum<MetaStat>("statKey", true)
            .Float("baseValue", true).Float("perLevel", true).Int("maxLevel", true).Consume("unlockPrice")
            .Int("skillTrigger", true).Int("skillEvery", true).Float("skillDmgPct", true).Int("skillTargets", true)
            .FkGroup("levelPriceGroup", LevelPriceData.Table, true).Res("icon", MvAtlas.UiIcon).Res("portrait", MvAtlas.Address).Bool("active");

        public int rarity { get; private set; }
        public MetaStat statKey { get; private set; }
        public double baseValue { get; private set; }
        public double perLevel { get; private set; }
        public int maxLevel { get; private set; }
        public IReadOnlyList<ConsumeData> unlockPrice { get; private set; }
        /// <summary>1 Hits / 2 Chance / 3 Seconds.</summary>
        public int skillTrigger { get; private set; }
        /// <summary>Hits = 볼리 수, Chance = %, Seconds = ms.</summary>
        public int skillEvery { get; private set; }
        public double skillDmgPct { get; private set; }
        public int skillTargets { get; private set; }
        public int levelPriceGroup { get; private set; }
        public string icon { get; private set; }
        /// <summary>HUD 초상. 기본 아틀라스가 07 §7.10.3에 없어 문자열로 둔다.</summary>
        public string portrait { get; private set; }

        public FriendData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            rarity = I(r, "rarity");
            statKey = E(r, "statKey", MetaStat.Dmg);
            baseValue = D(r, "baseValue");
            perLevel = D(r, "perLevel");
            maxLevel = I(r, "maxLevel");
            unlockPrice = Consume(r, "unlockPrice");
            skillTrigger = I(r, "skillTrigger");
            skillEvery = I(r, "skillEvery");
            skillDmgPct = D(r, "skillDmgPct");
            skillTargets = I(r, "skillTargets", 1);
            levelPriceGroup = I(r, "levelPriceGroup");
            icon = S(r, "icon");
            portrait = S(r, "portrait");
        }

        public FriendSkillTrigger Trigger => (FriendSkillTrigger)skillTrigger;

        /// <summary>보너스(우정 단계 제외) = baseValue + perLevel × (레벨−1).</summary>
        public double ValueAt(int level) => baseValue + perLevel * (level - 1);
    }

    // ---------------- 451_friendPower ----------------

    public sealed class FriendPowerData : MvRow
    {
        public const string Table = "451_friendPower";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("unlockLevel", true).Consume("price")
            .Enum<MetaStat>("statKey", true).Float("value", true).Res("icon", MvAtlas.UiIcon);

        public int unlockLevel { get; private set; }
        public IReadOnlyList<ConsumeData> price { get; private set; }
        public MetaStat statKey { get; private set; }
        public double value { get; private set; }
        public string icon { get; private set; }

        public FriendPowerData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            unlockLevel = I(r, "unlockLevel");
            price = Consume(r, "price");
            statKey = E(r, "statKey", MetaStat.Dmg);
            value = D(r, "value");
            icon = S(r, "icon");
        }
    }

    // ---------------- 460_rune ----------------

    /// <summary>별사탕 등급(표시용). 확률·패스 XP 정본은 802 행이다(08 §8.5.5).</summary>
    public sealed class RuneData : MvRow
    {
        public const string Table = "460_rune";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("grade").Color("color", true).Res("icon", MvAtlas.UiIcon)
            .Fk("randomBoxRowId", RandomBoxData.Table, true);

        public string color { get; private set; }
        public string icon { get; private set; }
        /// <summary>결과 표시용 802 행 id.</summary>
        public int randomBoxRowId { get; private set; }

        public RuneData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            color = S(r, "color");
            icon = S(r, "icon");
            randomBoxRowId = I(r, "randomBoxRowId");
        }
    }

    // ---------------- 470_gearPart ----------------

    public sealed class GearPartData : MvRow
    {
        public const string Table = "470_gearPart";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Enum<MetaStat>("statKey", true)
            .Res("icon", MvAtlas.UiIcon).Int("sort");

        public MetaStat statKey { get; private set; }
        public string icon { get; private set; }

        public GearPartData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            statKey = E(r, "statKey", MetaStat.Dmg);
            icon = S(r, "icon");
        }
    }

    // ---------------- 471_gearRarity ----------------

    public sealed class GearRarityData : MvRow
    {
        public const string Table = "471_gearRarity";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("rarity", true).Color("color", true).Float("value", true)
            .Consume("sellPrice").FkGroup("levelPriceGroup", LevelPriceData.Table, true).Str("frame");

        /// <summary>0~7.</summary>
        public int rarity { get; private set; }
        public string color { get; private set; }
        /// <summary>부위 statKey에 더하는 기본 %(강화 레벨당 +1).</summary>
        public double value { get; private set; }
        public IReadOnlyList<ConsumeData> sellPrice { get; private set; }
        public int levelPriceGroup { get; private set; }
        /// <summary>등급 테두리. 기본 아틀라스가 07 §7.10.3에 없어 문자열로 둔다.</summary>
        public string frame { get; private set; }

        public GearRarityData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            rarity = I(r, "rarity");
            color = S(r, "color");
            value = D(r, "value");
            sellPrice = Consume(r, "sellPrice");
            levelPriceGroup = I(r, "levelPriceGroup");
            frame = S(r, "frame");
        }

        /// <summary>장착 효과 % = value + 강화 레벨 × 1.</summary>
        public double ValueAt(int upgradeLevel) => value + upgradeLevel;
    }

    // ---------------- 480_blessing ----------------

    public sealed class BlessingData : MvRow
    {
        public const string Table = "480_blessing";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("effectType", true).Float("value1").Float("value2")
            .Int("adRuns", true).Res("icon", MvAtlas.UiIcon).Bool("active");

        /// <summary>1 부활 / 2 일일 / 3 스태미나 / 4 능력치.</summary>
        public int effectType { get; private set; }
        public double value1 { get; private set; }
        public double value2 { get; private set; }
        /// <summary>광고 1회로 켜지는 런 수.</summary>
        public int adRuns { get; private set; }
        public string icon { get; private set; }

        public BlessingData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            effectType = I(r, "effectType");
            value1 = D(r, "value1");
            value2 = D(r, "value2");
            adRuns = I(r, "adRuns");
            icon = S(r, "icon");
        }

        public BlessingEffectType EffectType => (BlessingEffectType)effectType;
    }

    // ---------------- 485_attendance ----------------

    public sealed class AttendanceData : MvRow
    {
        public const string Table = "485_attendance";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("day", true).Consume("reward").Res("icon", MvAtlas.UiIcon);

        public int day { get; private set; }
        public IReadOnlyList<ConsumeData> reward { get; private set; }
        public string icon { get; private set; }

        public AttendanceData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            day = I(r, "day");
            reward = Consume(r, "reward");
            icon = S(r, "icon");
        }
    }

    // ---------------- 490_roadMap ----------------

    public sealed class RoadMapData : MvRow
    {
        public const string Table = "490_roadMap";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Fk("zoneId", ZoneData.Table, true).Int("gainPerRun", true)
            .Int("maxRange", true).Int("catchUpGain", true).Int("startGap", true).Res("icon", MvAtlas.UiIcon).Bool("active");

        public int zoneId { get; private set; }
        /// <summary>본편 정산마다 구름(생선 단위) 증가.</summary>
        public int gainPerRun { get; private set; }
        /// <summary>내 생선과 차이가 이보다 크면 catchUpGain을 대신 더한다.</summary>
        public int maxRange { get; private set; }
        public int catchUpGain { get; private set; }
        /// <summary>Area 시작 생선 − startGap에서 출발.</summary>
        public int startGap { get; private set; }
        public string icon { get; private set; }

        public RoadMapData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            zoneId = I(r, "zoneId");
            gainPerRun = I(r, "gainPerRun");
            maxRange = I(r, "maxRange");
            catchUpGain = I(r, "catchUpGain");
            startGap = I(r, "startGap");
            icon = S(r, "icon");
        }
    }

    // ---------------- 495_godLevel ----------------

    public sealed class GodLevelData : MvRow
    {
        public const string Table = "495_godLevel";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("level", true).Int("dmgPct").Int("passiveMul")
            .FkGroup("levelPriceGroup", LevelPriceData.Table, true).Res("icon", MvAtlas.UiIcon);

        public int level { get; private set; }
        /// <summary>메타 Dmg에 더하는 %.</summary>
        public int dmgPct { get; private set; }
        /// <summary>113_god.passivePerLv × 이 값 = 패시브 수치.</summary>
        public int passiveMul { get; private set; }
        public int levelPriceGroup { get; private set; }
        public string icon { get; private set; }

        public GodLevelData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            level = I(r, "level");
            dmgPct = I(r, "dmgPct");
            passiveMul = I(r, "passiveMul");
            levelPriceGroup = I(r, "levelPriceGroup");
            icon = S(r, "icon");
        }
    }
}
