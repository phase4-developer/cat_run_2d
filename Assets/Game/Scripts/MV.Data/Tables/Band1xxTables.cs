using System.Collections.Generic;

namespace MV.Data
{
    // 1xx 개체·전투 표(기획서 07 §7.5, §7.7).

    public enum Hittable { Always, BeforePass, Never }
    public enum RewardSource { Row, FieldCell, Enemy }
    public enum SpritePick { Single, JunkCycle, Anim }
    /// <summary>
    /// 오브젝트 그림 모양(07 §7.5.2 `viewShape`). 이름은 MV.View `ObjectShape`와 1:1 —
    /// MV.View가 `ObjectViewFactory.ShapeOf`의 key·컴포넌트 분기 대신 이 값을 읽는다(교체는 MV-VIEW).
    /// </summary>
    public enum ObjectViewShape { Single, Junk, RollGate, WarpGate, Well, Stair, CoinPile, CatnipPot, Mouse }
    public enum LabelKind { None, WellText, StairText, StairGate, CraftGate, Arch, WallCell, EnemyHp, BoxHp }
    public enum HitReactionType { None, Damage, ChargeStairGate, ChargeCraftGate, ChargeWell }
    public enum BreakStat { None, Walls, Kills, Boxes }
    public enum GateType { StairGate, CraftGate, Well }
    public enum Growth { None, Add, Pow }
    public enum PerPass { GaugeLv, One, Level }
    public enum ReceiveType { OnBreak, OnPass, OnFlush, OnArrive, OnTouch }
    public enum ClockKind { Real, Game }
    public enum ArriveAction { None, StartClimb, FloorBanner, FinishGate }
    public enum MoveAiType { Static, Chase, Advance }
    public enum TouchAction { None, Steal }
    public enum HpRuleType { None, AreaHpMul, FieldLerp, Fixed, ModeStage }
    public enum CardEffectType
    {
        DmgMul, FireRateMul, CloneAdd, GoldMul, Orbit, ProcExtraHit, ProcBurn, ChainJump, ChainSplash, Storm, Blast,
        PierceEveryN, CritChance, DoubleChance, Pierce, Spin, Beam, Meteor, ProcChill, Shield, CloneDmgMul, DropBonus,
    }
    public enum CardPoolEntryType { Card, GodCards, CommonActive, Exclude }

    // ---------------- 101_cat ----------------

    public sealed class CatData : MvRow
    {
        public const string Table = "101_cat";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Res("spritePrefix", MvAtlas.Theme)
            .Str("atlasPattern").Int("tierCount", true).Int("patternCount", true).Res("lobbySprite", MvAtlas.WorldLobby);

        public string spritePrefix { get; private set; }
        public string atlasPattern { get; private set; }
        public int tierCount { get; private set; }
        public int patternCount { get; private set; }
        public string lobbySprite { get; private set; }

        public CatData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            spritePrefix = S(r, "spritePrefix");
            atlasPattern = S(r, "atlasPattern");
            tierCount = I(r, "tierCount");
            patternCount = I(r, "patternCount");
            lobbySprite = S(r, "lobbySprite");
        }
    }

    // ---------------- 102_enemy ----------------

    public sealed class EnemyData : MvRow
    {
        public const string Table = "102_enemy";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("sort").Bool("active").RunConsume()
            .Fk("hpRuleId", HpRuleData.Table, true).Fk("moveAiId", MoveAiData.Table, true).Consume("reward", runExtension: true)
            .FkGroup("dropId", DropData.Table).Res("sprite", MvAtlas.Theme).Int("frameCount").Float("fps")
            .Sound("hitSound").Sound("breakSound").Effect("hitEffect").Effect("breakEffect")
            .Float("hw").Float("hd").Float("scale").Res("icon", MvAtlas.UiIcon);

        public int hpRuleId { get; private set; }
        public int moveAiId { get; private set; }
        public IReadOnlyList<ConsumeData> reward { get; private set; }
        public int dropId { get; private set; }
        public string sprite { get; private set; }
        public int frameCount { get; private set; }
        public double fps { get; private set; }
        public string hitSound { get; private set; }
        public string breakSound { get; private set; }
        public string hitEffect { get; private set; }
        public string breakEffect { get; private set; }
        /// <summary>빈칸 = 105_objectKind 값.</summary>
        public double? hw { get; private set; }
        public double? hd { get; private set; }
        public double scale { get; private set; }
        public string icon { get; private set; }

        public EnemyData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            hpRuleId = I(r, "hpRuleId");
            moveAiId = I(r, "moveAiId");
            reward = Consume(r, "reward");
            dropId = I(r, "dropId");
            sprite = S(r, "sprite");
            frameCount = I(r, "frameCount", 16);
            fps = D(r, "fps", 12.7);
            hitSound = S(r, "hitSound");
            breakSound = S(r, "breakSound");
            hitEffect = S(r, "hitEffect");
            breakEffect = S(r, "breakEffect");
            hw = Opt(r, "hw");
            hd = Opt(r, "hd");
            scale = D(r, "scale", 1);
            icon = S(r, "icon");
        }

        static double? Opt(TSVRow r, string c) => string.IsNullOrEmpty(r.Get(c)) ? (double?)null : r.GetDouble(c);
    }

    // ---------------- 103_item ----------------

    public sealed class ItemData : MvRow
    {
        public const string Table = "103_item";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("tier", true).Int("sort").Bool("active")
            .Int("dmg", true).Color("col").Color("gradeCol").Bool("spin").Res("icon", MvAtlas.UiIcon)
            .Res("projSprite", MvAtlas.Theme).Str("lookName");

        public int tier { get; private set; }
        public int dmg { get; private set; }
        public string col { get; private set; }
        public string gradeCol { get; private set; }
        public bool spin { get; private set; }
        public string icon { get; private set; }
        public string projSprite { get; private set; }
        public string lookName { get; private set; }

        public ItemData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            tier = I(r, "tier");
            dmg = I(r, "dmg");
            col = S(r, "col");
            gradeCol = S(r, "gradeCol");
            spin = Bool(r, "spin");
            icon = S(r, "icon");
            projSprite = S(r, "projSprite");
            lookName = S(r, "lookName");
        }
    }

    // ---------------- 105_objectKind ----------------

    public sealed class ObjectKindData : MvRow
    {
        public const string Table = "105_objectKind";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("sort").Bool("active").RunConsume()
            .Int("protoType").Float("hw", true).Float("hd", true).Enum<Hittable>("hittable").Bool("breakable").Bool("blocks")
            .Fk("hpRuleId", HpRuleData.Table).Fk("hitReactionId", HitReactionData.Table).Fk("chargeGateId", ChargeGateData.Table)
            .Fk("receiveId", ReceiveConditionData.Table).Fk("moveAiId", MoveAiData.Table).Fk("enemyId", EnemyData.Table)
            .Enum<RewardSource>("rewardSource").Consume("reward", runExtension: true).Consume("hitReward")
            .Res("sprite", MvAtlas.Theme).Enum<SpritePick>("spritePick").Enum<ObjectViewShape>("viewShape").Res("spriteSoul", MvAtlas.Theme).Colors("tint")
            .Enum<LabelKind>("labelKind").Str("labelKey").Float("sortBias")
            .Sound("hitSound").Sound("breakSound").Effect("hitEffect").Effect("breakEffect");

        public int protoType { get; private set; }
        public double hw { get; private set; }
        public double hd { get; private set; }
        public Hittable hittable { get; private set; }
        public bool breakable { get; private set; }
        public bool blocks { get; private set; }
        public int hpRuleId { get; private set; }
        public int hitReactionId { get; private set; }
        public int chargeGateId { get; private set; }
        public int receiveId { get; private set; }
        public int moveAiId { get; private set; }
        public int enemyId { get; private set; }
        public RewardSource rewardSource { get; private set; }
        public IReadOnlyList<ConsumeData> reward { get; private set; }
        public IReadOnlyList<ConsumeData> hitReward { get; private set; }
        /// <summary>변형은 `/` 목록.</summary>
        public IReadOnlyList<string> sprite { get; private set; }
        public SpritePick spritePick { get; private set; }
        /// <summary>그림 모양. 빈칸 = Single.</summary>
        public ObjectViewShape viewShape { get; private set; }
        public string spriteSoul { get; private set; }
        public IReadOnlyList<string> tint { get; private set; }
        public LabelKind labelKind { get; private set; }
        public string labelKey { get; private set; }
        public double sortBias { get; private set; }
        public string hitSound { get; private set; }
        public string breakSound { get; private set; }
        public string hitEffect { get; private set; }
        public string breakEffect { get; private set; }

        public ObjectKindData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            protoType = I(r, "protoType");
            hw = D(r, "hw");
            hd = D(r, "hd");
            hittable = E(r, "hittable", Hittable.Always);
            breakable = Bool(r, "breakable");
            blocks = Bool(r, "blocks");
            hpRuleId = I(r, "hpRuleId");
            hitReactionId = I(r, "hitReactionId");
            chargeGateId = I(r, "chargeGateId");
            receiveId = I(r, "receiveId");
            moveAiId = I(r, "moveAiId");
            enemyId = I(r, "enemyId");
            rewardSource = E(r, "rewardSource", RewardSource.Row);
            reward = Consume(r, "reward");
            hitReward = Consume(r, "hitReward");
            sprite = Strs(r, "sprite");
            spritePick = E(r, "spritePick", SpritePick.Single);
            viewShape = E(r, "viewShape", ObjectViewShape.Single);
            spriteSoul = S(r, "spriteSoul");
            tint = Strs(r, "tint");
            labelKind = E(r, "labelKind", LabelKind.None);
            labelKey = S(r, "labelKey");
            sortBias = D(r, "sortBias");
            hitSound = S(r, "hitSound");
            breakSound = S(r, "breakSound");
            hitEffect = S(r, "hitEffect");
            breakEffect = S(r, "breakEffect");
        }
    }

    // ---------------- 106_hitReaction ----------------

    public sealed class HitReactionData : MvRow
    {
        public const string Table = "106_hitReaction";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Enum<HitReactionType>("type", true)
            .Bool("flash").Float("punch").Float("objShake").ConfigKey("breakHitstop").ConfigKey("breakShake", prefix: true)
            .Enum<BreakStat>("breakStat");

        public HitReactionType type { get; private set; }
        public bool flash { get; private set; }
        public double punch { get; private set; }
        public double objShake { get; private set; }
        public string breakHitstop { get; private set; }
        public string breakShake { get; private set; }
        public BreakStat breakStat { get; private set; }

        public HitReactionData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            type = E(r, "type", HitReactionType.None);
            flash = Bool(r, "flash");
            punch = D(r, "punch");
            objShake = D(r, "objShake");
            breakHitstop = S(r, "breakHitstop");
            breakShake = S(r, "breakShake");
            breakStat = E(r, "breakStat", BreakStat.None);
        }
    }

    // ---------------- 107_chargeGate ----------------

    public sealed class ChargeGateData : MvRow
    {
        public const string Table = "107_chargeGate";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Enum<GateType>("gateType", true)
            .Float("base", true).Float("step").Enum<Growth>("growth").Int("cap").Float("mul").Bool("useScale").Bool("useGauge")
            .Bool("useAreaHp").Float("diffMul").Enum<PerPass>("perPass", true);

        public GateType gateType { get; private set; }
        public double @base { get; private set; }
        public double step { get; private set; }
        public Growth growth { get; private set; }
        public int cap { get; private set; }
        public double mul { get; private set; }
        public bool useScale { get; private set; }
        public bool useGauge { get; private set; }
        public bool useAreaHp { get; private set; }
        public double diffMul { get; private set; }
        public PerPass perPass { get; private set; }

        public ChargeGateData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            gateType = E(r, "gateType", GateType.StairGate);
            @base = D(r, "base");
            step = D(r, "step");
            growth = E(r, "growth", Growth.None);
            cap = I(r, "cap");
            mul = D(r, "mul", 1);
            useScale = Bool(r, "useScale");
            useGauge = Bool(r, "useGauge");
            useAreaHp = Bool(r, "useAreaHp");
            diffMul = D(r, "diffMul");
            perPass = E(r, "perPass", PerPass.One);
        }
    }

    // ---------------- 108_receiveCondition ----------------

    public sealed class ReceiveConditionData : MvRow
    {
        public const string Table = "108_receiveCondition";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Enum<ReceiveType>("type", true)
            .Float("margin").Float("insideX").Int("preDelayMs").Int("delayMs").Enum<ClockKind>("clock")
            .Enum<ArriveAction>("arriveAction").Effect("passEffect").Sound("passSound");

        public ReceiveType type { get; private set; }
        public double margin { get; private set; }
        public double insideX { get; private set; }
        public int preDelayMs { get; private set; }
        public int delayMs { get; private set; }
        public ClockKind clock { get; private set; }
        public ArriveAction arriveAction { get; private set; }
        public string passEffect { get; private set; }
        public string passSound { get; private set; }

        public ReceiveConditionData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            type = E(r, "type", ReceiveType.OnBreak);
            margin = D(r, "margin");
            insideX = D(r, "insideX");
            preDelayMs = I(r, "preDelayMs");
            delayMs = I(r, "delayMs");
            clock = E(r, "clock", ClockKind.Game);
            arriveAction = E(r, "arriveAction", ArriveAction.None);
            passEffect = S(r, "passEffect");
            passSound = S(r, "passSound");
        }
    }

    // ---------------- 109_moveAI ----------------

    public sealed class MoveAiData : MvRow
    {
        public const string Table = "109_moveAI";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Enum<MoveAiType>("type", true)
            .Float("aggro").Float("speedMin").Float("speedMax").Float("xFollow").Float("touchX").Float("touchZ")
            .Enum<TouchAction>("touchAction").Float("stealBase").Float("stealPerZone").ConfigKey("stealShake", prefix: true)
            .Sound("stealSound").Effect("stealEffect").Float("despawnBehind");

        public MoveAiType type { get; private set; }
        public double aggro { get; private set; }
        public double speedMin { get; private set; }
        public double speedMax { get; private set; }
        public double xFollow { get; private set; }
        public double touchX { get; private set; }
        public double touchZ { get; private set; }
        public TouchAction touchAction { get; private set; }
        public double stealBase { get; private set; }
        public double stealPerZone { get; private set; }
        public string stealShake { get; private set; }
        public string stealSound { get; private set; }
        public string stealEffect { get; private set; }
        public double despawnBehind { get; private set; }

        public MoveAiData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            type = E(r, "type", MoveAiType.Static);
            aggro = D(r, "aggro");
            speedMin = D(r, "speedMin");
            speedMax = D(r, "speedMax");
            xFollow = D(r, "xFollow");
            touchX = D(r, "touchX");
            touchZ = D(r, "touchZ");
            touchAction = E(r, "touchAction", TouchAction.None);
            stealBase = D(r, "stealBase");
            stealPerZone = D(r, "stealPerZone");
            stealShake = S(r, "stealShake");
            stealSound = S(r, "stealSound");
            stealEffect = S(r, "stealEffect");
            despawnBehind = D(r, "despawnBehind");
        }
    }

    // ---------------- 110_card ----------------

    public sealed class CardData : MvRow
    {
        public const string Table = "110_card";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Group().Key().Int("tier", true)
            .Fk("effectId", CardEffectData.Table, true).Int("sort").Bool("active").Res("icon", MvAtlas.UiCard)
            .Res("projSprite", MvAtlas.Theme).KeyUniqueInGroup();

        /// <summary>0 = 공용, n = 113_god.cardGroup.</summary>
        public int group { get; private set; }
        public int tier { get; private set; }
        public int effectId { get; private set; }
        public string icon { get; private set; }
        public string projSprite { get; private set; }

        public CardData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            group = I(r, "group");
            tier = I(r, "tier");
            effectId = I(r, "effectId");
            icon = S(r, "icon");
            projSprite = S(r, "projSprite");
        }
    }

    // ---------------- 111_cardPool ----------------

    public sealed class CardPoolData : MvRow
    {
        public const string Table = "111_cardPool";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Group().Str("key", true)
            .Enum<CardPoolEntryType>("entryType", true).Fk("cardId", CardData.Table).Int("weight");

        public int group { get; private set; }
        public CardPoolEntryType entryType { get; private set; }
        public int cardId { get; private set; }
        public int weight { get; private set; }

        public CardPoolData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            group = I(r, "group");
            entryType = E(r, "entryType", CardPoolEntryType.Card);
            cardId = I(r, "cardId");
            weight = I(r, "weight", 1);
        }
    }

    // ---------------- 112_cardEffect ----------------

    public sealed class CardEffectData : MvRow
    {
        public const string Table = "112_cardEffect";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Enum<CardEffectType>("type", true)
            .Float("p1").Float("p2").Float("p3").Float("p4").Bool("active").Effect("procEffect").Sound("procSound");

        public CardEffectType type { get; private set; }
        public double p1 { get; private set; }
        public double p2 { get; private set; }
        public double p3 { get; private set; }
        public double p4 { get; private set; }
        public string procEffect { get; private set; }
        public string procSound { get; private set; }

        public CardEffectData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            type = E(r, "type", CardEffectType.DmgMul);
            p1 = D(r, "p1");
            p2 = D(r, "p2");
            p3 = D(r, "p3");
            p4 = D(r, "p4");
            procEffect = S(r, "procEffect");
            procSound = S(r, "procSound");
        }
    }

    // ---------------- 113_god ----------------

    public sealed class GodData : MvRow
    {
        public const string Table = "113_god";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("sort").Bool("active").Str("element", true)
            .Color("color", true).Color("projTint").Int("cardGroup", true).Int("unlockType").Fk("openId", ContentOpenData.Table)
            .Consume("unlockPrice").Str("passiveStat").Float("passivePerLv").Bool("gearSet")
            .Res("portrait", MvAtlas.Address).Res("icon", MvAtlas.UiIcon);

        public string element { get; private set; }
        public string color { get; private set; }
        /// <summary>빈칸 = color.</summary>
        public string projTint { get; private set; }
        public int cardGroup { get; private set; }
        /// <summary>0 무료 / 1 contentOpen / 2 구매.</summary>
        public int unlockType { get; private set; }
        public int openId { get; private set; }
        public IReadOnlyList<ConsumeData> unlockPrice { get; private set; }
        /// <summary>08 §8.3.5 값 목록이 확정되면 enum으로 바꾼다.</summary>
        public string passiveStat { get; private set; }
        public double passivePerLv { get; private set; }
        public bool gearSet { get; private set; }
        public string portrait { get; private set; }
        public string icon { get; private set; }

        public GodData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            element = S(r, "element");
            color = S(r, "color");
            projTint = S(r, "projTint");
            if (string.IsNullOrEmpty(projTint)) projTint = color;
            cardGroup = I(r, "cardGroup");
            unlockType = I(r, "unlockType");
            openId = I(r, "openId");
            unlockPrice = Consume(r, "unlockPrice");
            passiveStat = S(r, "passiveStat");
            passivePerLv = D(r, "passivePerLv");
            gearSet = Bool(r, "gearSet");
            portrait = S(r, "portrait");
            icon = S(r, "icon");
        }
    }

    // ---------------- 114_hpRule ----------------

    public sealed class HpRuleData : MvRow
    {
        public const string Table = "114_hpRule";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Enum<HpRuleType>("type", true)
            .Float("mul").Int("fixed").Bool("useScale").Bool("useObstMul");

        public HpRuleType type { get; private set; }
        public double mul { get; private set; }
        public int @fixed { get; private set; }
        public bool useScale { get; private set; }
        public bool useObstMul { get; private set; }

        public HpRuleData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            type = E(r, "type", HpRuleType.None);
            mul = D(r, "mul", 1);
            @fixed = I(r, "fixed");
            useScale = Bool(r, "useScale");
            useObstMul = Bool(r, "useObstMul");
        }
    }

    // ---------------- 115_cardTier ----------------

    public sealed class CardTierData : MvRow
    {
        public const string Table = "115_cardTier";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("tierIndex", true).Int("need", true)
            .Color("color", true).Res("frameSprite", MvAtlas.UiCard).Sound("cardSound");

        public int tierIndex { get; private set; }
        public int need { get; private set; }
        public string color { get; private set; }
        public string frameSprite { get; private set; }
        public string cardSound { get; private set; }

        public CardTierData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            tierIndex = I(r, "tierIndex");
            need = I(r, "need");
            color = S(r, "color");
            frameSprite = S(r, "frameSprite");
            cardSound = S(r, "cardSound");
        }
    }
}
