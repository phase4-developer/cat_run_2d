using System.Collections.Generic;

namespace MV.Data
{
    // 1번대 성장 표(기획서 07 §7.8.1~7.8.6).

    public enum LobbyEffectType { StartStairLv, DeckAdd, GoldMul }
    public enum CostFormula { Pow, LinearNext }
    /// <summary>뒤 3개(PierceChance·DiaPerLevel·GoldPerLevel)는 MV-SPEC-02 Q-28 더미 행. 런 적용은 MV-RUN-SPEC2.</summary>
    public enum AreaStatKey { FireRate, Range, Clone, CritChance, DoubleChance, PierceChance, DiaPerLevel, GoldPerLevel }
    public enum ResetOn { AreaChange, Never }
    /// <summary>뒤 5개(GoldCap~StairDouble)는 MV-SPEC-02 Q-27 임시 값 행. 런 적용은 MV-RUN-SPEC2.</summary>
    public enum RebirthStatKey { DmgMul, CritMul, ObstMul, DiaCap, GoldCap, StartGold, StartDia, CraftDouble, StairDouble }

    /// <summary>levelPriceGroup·max를 가진 성장 표(검증기가 16_levelPrice 빈틈을 본다).</summary>
    public interface ILevelPriced
    {
        int id { get; }
        string key { get; }
        int max { get; }
        int levelPriceGroup { get; }
    }

    // ---------------- 10_lobbyUpgrade ----------------

    public sealed class LobbyUpgradeData : MvRow, ILevelPriced
    {
        public const string Table = "10_lobbyUpgrade";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("sort").Bool("active").Int("max")
            .FkGroup("levelPriceGroup", LevelPriceData.Table, true).Enum<LobbyEffectType>("effectType", true)
            .Res("icon", MvAtlas.UiIcon).Res("facSprite", MvAtlas.WorldLobby)
            .Enum<CostFormula>("costFormula").Float("costBase").Float("costGrowth");

        /// <summary>표의 max 칸. 계단(StartStairLv)·지팡이(DeckAdd)는 비워 두고(0) <see cref="MvTables.LevelMax"/>로 계산한다. 읽을 때는 항상 LevelMax.</summary>
        public int max { get; private set; }
        public int levelPriceGroup { get; private set; }
        public LobbyEffectType effectType { get; private set; }
        public string icon { get; private set; }
        public string facSprite { get; private set; }
        /// <summary>행 생성 도구 전용(런타임 무시).</summary>
        public CostFormula costFormula { get; private set; }
        public double costBase { get; private set; }
        public double costGrowth { get; private set; }

        public LobbyUpgradeData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            max = I(r, "max");
            levelPriceGroup = I(r, "levelPriceGroup");
            effectType = E(r, "effectType", LobbyEffectType.StartStairLv);
            icon = S(r, "icon");
            facSprite = S(r, "facSprite");
            costFormula = E(r, "costFormula", CostFormula.Pow);
            costBase = D(r, "costBase");
            costGrowth = D(r, "costGrowth");
        }
    }

    // ---------------- 11_stairLevel ----------------

    public sealed class StairLevelData : MvRow
    {
        public const string Table = "11_stairLevel";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("lv", true).Int("need", true);

        public int lv { get; private set; }
        public int need { get; private set; }

        public StairLevelData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            lv = I(r, "lv");
            need = I(r, "need");
        }
    }

    // ---------------- 12_weaponLevel ----------------

    public sealed class WeaponLevelData : MvRow
    {
        public const string Table = "12_weaponLevel";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("lv", true).Int("need", true)
            .Fk("itemId", ItemData.Table, true);

        public int lv { get; private set; }
        public int need { get; private set; }
        public int itemId { get; private set; }

        public WeaponLevelData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            lv = I(r, "lv");
            need = I(r, "need");
            itemId = I(r, "itemId");
        }
    }

    // ---------------- 13_areaUpgrade ----------------

    public sealed class AreaUpgradeData : MvRow, ILevelPriced
    {
        public const string Table = "13_areaUpgrade";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("sort").Bool("active").Int("max", true)
            .Float("perLv", true).Enum<AreaStatKey>("statKey", true).Str("valueFormat", true).Float("valueBase").Float("valueStep")
            .Enum<ResetOn>("resetOn").FkGroup("levelPriceGroup", LevelPriceData.Table, true).Res("icon", MvAtlas.UiIcon)
            .Float("costBase").Float("costGrowth");

        public int max { get; private set; }
        public double perLv { get; private set; }
        public AreaStatKey statKey { get; private set; }
        public string valueFormat { get; private set; }
        public double valueBase { get; private set; }
        public double valueStep { get; private set; }
        public ResetOn resetOn { get; private set; }
        public int levelPriceGroup { get; private set; }
        public string icon { get; private set; }
        public double costBase { get; private set; }
        public double costGrowth { get; private set; }

        public AreaUpgradeData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            max = I(r, "max");
            perLv = D(r, "perLv");
            statKey = E(r, "statKey", AreaStatKey.FireRate);
            valueFormat = S(r, "valueFormat");
            valueBase = D(r, "valueBase");
            valueStep = D(r, "valueStep");
            resetOn = E(r, "resetOn", ResetOn.AreaChange);
            levelPriceGroup = I(r, "levelPriceGroup");
            icon = S(r, "icon");
            costBase = D(r, "costBase");
            costGrowth = D(r, "costGrowth", 1.6);
        }
    }

    // ---------------- 14_rebirthPerk ----------------

    public sealed class RebirthPerkData : MvRow, ILevelPriced
    {
        public const string Table = "14_rebirthPerk";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("sort").Bool("active").Int("max", true)
            .Float("base", true).Float("perLv", true).Float("floor").Enum<RebirthStatKey>("statKey", true)
            .Str("valueFormat", true).Float("valueBase").Float("valueStep")
            .FkGroup("levelPriceGroup", LevelPriceData.Table, true).Res("icon", MvAtlas.UiIcon)
            .Float("costBase").Float("costGrowth");

        public int max { get; private set; }
        public double @base { get; private set; }
        public double perLv { get; private set; }
        /// <summary>빈칸 = 하한 없음.</summary>
        public double? floor { get; private set; }
        public RebirthStatKey statKey { get; private set; }
        public string valueFormat { get; private set; }
        public double valueBase { get; private set; }
        public double valueStep { get; private set; }
        public int levelPriceGroup { get; private set; }
        public string icon { get; private set; }
        public double costBase { get; private set; }
        public double costGrowth { get; private set; }

        public RebirthPerkData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            max = I(r, "max");
            @base = D(r, "base");
            perLv = D(r, "perLv");
            floor = string.IsNullOrEmpty(r.Get("floor")) ? (double?)null : D(r, "floor");
            statKey = E(r, "statKey", RebirthStatKey.DmgMul);
            valueFormat = S(r, "valueFormat");
            valueBase = D(r, "valueBase");
            valueStep = D(r, "valueStep");
            levelPriceGroup = I(r, "levelPriceGroup");
            icon = S(r, "icon");
            costBase = D(r, "costBase");
            costGrowth = D(r, "costGrowth", 1.5);
        }

        /// <summary>값 = max(floor, base + perLv × lv).</summary>
        public double ValueAt(int lv)
        {
            double v = @base + perLv * lv;
            return floor.HasValue && v < floor.Value ? floor.Value : v;
        }
    }

    // ---------------- 16_levelPrice ----------------

    public sealed class LevelPriceData : MvRow
    {
        public const string Table = "16_levelPrice";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Group().Int("targetLevel", true).Consume("price").Int("needPiece").Int("needRebirth");

        public int group { get; private set; }
        public int targetLevel { get; private set; }
        public IReadOnlyList<ConsumeData> price { get; private set; }
        /// <summary>함께 드는 카드 수(그룹 301 펫, 08 §8.5.2). 빈칸 = 0.</summary>
        public int needPiece { get; private set; }
        /// <summary>이 단계(targetLevel)를 사는 데 필요한 부활 레벨(<c>save.rb.lv</c>). 빈칸 = 0 = 잠김 없음(MV-UI2-AREAUP, 사용자 2026-11-19 레벨별 잠금).</summary>
        public int needRebirth { get; private set; }

        public LevelPriceData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            group = I(r, "group");
            targetLevel = I(r, "targetLevel");
            price = Consume(r, "price");
            needPiece = I(r, "needPiece");
            needRebirth = I(r, "needRebirth");
        }
    }

    public sealed class LevelPriceTable : MvTable<LevelPriceData>
    {
        public LevelPriceTable() : base(LevelPriceData.Schema, r => new LevelPriceData(r), d => d.group) { }

        /// <summary>현재 단계 P에서 다음 구매 비용 = targetLevel P+1 행. 없으면 null(MAX).</summary>
        public LevelPriceData Get(int group, int targetLevel)
        {
            foreach (var row in GetGroup(group))
                if (row.targetLevel == targetLevel) return row;
            return null;
        }
    }

    // ---------------- 15_grade ----------------

    /// <summary>
    /// 15_grade — 보상·아이템 칸 등급(1 Common ~ 12 Transcendent, 노션 「[참고] 프로젝트 내 등급 관련 리소스 표」, 결정 D-GRADE-TABLE).
    /// 이름은 언어팩 <c>name:&lt;id&gt;</c>(없으면 <c>name</c> 열). 틀 그림은 <c>09_uiResource</c> 「등급 틀」 묶음의 sort = <see cref="grade"/> 행(<see cref="MvUiGrade.Frame"/>)이라 이 표에 그림 열을 다시 두지 않는다.
    /// 등급을 가진 표는 이 표 id를 가리킨다(첫 적용 <c>01_wealth.gradeId</c>).
    /// </summary>
    public sealed class GradeData : MvRow
    {
        public const string Table = "15_grade";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("grade", true);

        /// <summary>등급 번호(1부터). 틀 그림 = 09_uiResource 「등급 틀」 sort.</summary>
        public int grade { get; private set; }

        public GradeData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            grade = I(r, "grade");
        }
    }
}
