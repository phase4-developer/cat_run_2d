using System.Collections.Generic;

namespace MV.Data
{
    // 4xx 모드·퀘스트·콘텐츠 상수 표(기획서 08 §8.4, §8.5.1, §8.5.6, §8.5.13; 07 §7.9.1~7.9.2).

    /// <summary>`400_content.contentType`(08 §8.4.2). IModeRule 구현 선택.</summary>
    public enum ContentType { Main = 0, Defence = 1, FixedLoadout = 2, Course = 3, Boss = 4 }

    /// <summary>`402_modeRule.layout`(08 §8.4.4).</summary>
    public enum ModeLayout { Main = 0, Arena = 1, Course = 2 }

    /// <summary>`402_modeRule.hpSource`.</summary>
    public enum ModeHpSource { Area = 0, Stage = 1 }

    /// <summary>`402_modeRule.runRewardToAccount`.</summary>
    public enum ModeRunReward { None = 0, GoldDia = 1, All = 2 }

    /// <summary>퀘스트 카운터(08 §8.5.1 clearType). 값 = 표 숫자.</summary>
    public enum QuestClearType
    {
        RunComplete = 1, StairClimb = 2, WallBreak = 3, GoldEarn = 4, SoulEarn = 5, WeaponCraft = 6, AreaUpgrade = 7,
        LobbyUpgrade = 8, AreaOpen = 9, ModeClear = 10, AdWatch = 11, Rebirth = 12, PetSummon = 13, FriendUpgrade = 14,
        BoxOpen = 15, GodLevelUp = 16, ResearchDone = 17, LoginDay = 18, DailyPoint = 20,
    }

    /// <summary>`410.periodType`: 0 없음 / 2 일일(UTC 날짜) / 3 월간(UTC yyyyMM) / 5 이벤트 주기(499 event.*).</summary>
    public enum QuestPeriodType { None = 0, Daily = 2, Monthly = 3, Event = 5 }

    /// <summary>`410.category`.</summary>
    public enum QuestCategory { Daily = 1, Monthly = 2, Achievement = 3, Pass = 100 }

    /// <summary>`410.groupType`: 1 목록형 / 2 단계형(업적) / 3 포인트·레벨형(일일 종합·패스).</summary>
    public enum QuestGroupType { List = 1, Step = 2, PointLevel = 3 }

    /// <summary>`410.progressScope`: 1 평생 누적 / 2 기간 누적.</summary>
    public enum QuestProgressScope { Lifetime = 1, Period = 2 }

    // ---------------- 400_content ----------------

    public sealed class ContentData : MvRow
    {
        public const string Table = "400_content";
        public const string DefaultHud = "Run";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("contentType", true).Bool("active").Int("sort")
            .Res("icon", MvAtlas.UiIcon).Res("bg", MvAtlas.Address).Str("banner").Sound("bgm")
            .Fk("modeRuleId", ModeRuleData.Table).FkGroup("stageGroup", ModeStageData.Table).Fk("openId", ContentOpenData.Table)
            .Consume("spendResource").Bool("sweepActive").Str("hudId");

        public ContentType contentType { get; private set; }
        public string icon { get; private set; }
        public string bg { get; private set; }
        public string banner { get; private set; }
        public string bgm { get; private set; }
        public int modeRuleId { get; private set; }
        public int stageGroup { get; private set; }
        public int openId { get; private set; }
        /// <summary>입장 비용(보통 `3 / 입장권 id / 1`). 비면 없음.</summary>
        public IReadOnlyList<ConsumeData> spendResource { get; private set; }
        public bool sweepActive { get; private set; }
        /// <summary>런 HUD 이름. 빈칸 = <see cref="DefaultHud"/>.</summary>
        public string hudId { get; private set; }

        public ContentData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            contentType = (ContentType)I(r, "contentType");
            icon = S(r, "icon");
            bg = S(r, "bg");
            banner = S(r, "banner");
            bgm = S(r, "bgm");
            modeRuleId = I(r, "modeRuleId");
            stageGroup = I(r, "stageGroup");
            openId = I(r, "openId");
            spendResource = Consume(r, "spendResource");
            sweepActive = Bool(r, "sweepActive");
            var hud = S(r, "hudId");
            hudId = hud.Length > 0 ? hud : DefaultHud;
        }
    }

    // ---------------- 401_modeStage ----------------

    public sealed class ModeStageData : MvRow
    {
        public const string Table = "401_modeStage";
        /// <summary>baseHp 빈칸 기본(정본 Area 1 hp, 08 §8.4.9).</summary>
        public const int DefaultBaseHp = 100;
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Group().Int("stage", true).Bool("active")
            .Fk("templateId", TemplateData.Table).FkGroup("floorGroup", AreaFloorData.Table).Int("baseHp").Float("hpMul").Float("rewardMul")
            .Int("survivalSec").Int("minKills").Int("maxAlive").Int("spawnMs").Long("enemyHp").Float("enemySpeed").Int("boxMs").Long("boxHp")
            .Long("bossHp").Float("bossSpeed").Int("summonMs").Int("summonCount").Fk("enemyId", EnemyData.Table)
            .Consume("clearReward");

        public int group { get; private set; }
        public int stage { get; private set; }
        public int templateId { get; private set; }
        public int floorGroup { get; private set; }
        public int baseHp { get; private set; }
        public double hpMul { get; private set; }
        public double rewardMul { get; private set; }
        public int survivalSec { get; private set; }
        public int minKills { get; private set; }
        public int maxAlive { get; private set; }
        public int spawnMs { get; private set; }
        public long enemyHp { get; private set; }
        public double enemySpeed { get; private set; }
        public int boxMs { get; private set; }
        public long boxHp { get; private set; }
        public long bossHp { get; private set; }
        public double bossSpeed { get; private set; }
        public int summonMs { get; private set; }
        public int summonCount { get; private set; }
        public int enemyId { get; private set; }
        /// <summary>클리어·소탕 보상.</summary>
        public IReadOnlyList<ConsumeData> clearReward { get; private set; }

        public ModeStageData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            group = I(r, "group");
            stage = I(r, "stage");
            templateId = I(r, "templateId");
            floorGroup = I(r, "floorGroup");
            baseHp = I(r, "baseHp", DefaultBaseHp);
            hpMul = D(r, "hpMul", 1);
            rewardMul = D(r, "rewardMul", 1);
            survivalSec = I(r, "survivalSec");
            minKills = I(r, "minKills");
            maxAlive = I(r, "maxAlive");
            spawnMs = I(r, "spawnMs");
            enemyHp = L(r, "enemyHp");
            enemySpeed = D(r, "enemySpeed");
            boxMs = I(r, "boxMs");
            boxHp = L(r, "boxHp");
            bossHp = L(r, "bossHp");
            bossSpeed = D(r, "bossSpeed");
            summonMs = I(r, "summonMs");
            summonCount = I(r, "summonCount");
            enemyId = I(r, "enemyId");
            clearReward = Consume(r, "clearReward");
        }
    }

    public sealed class ModeStageTable : MvTable<ModeStageData>
    {
        public ModeStageTable() : base(ModeStageData.Schema, r => new ModeStageData(r), d => d.group) { }

        /// <summary>묶음의 단계 행(stage 0부터). 없으면 null.</summary>
        public ModeStageData Get(int group, int stage)
        {
            foreach (var row in GetGroup(group))
                if (row.stage == stage) return row;
            return null;
        }

        /// <summary>묶음의 마지막 stage(행이 없으면 -1).</summary>
        public int LastStage(int group)
        {
            int last = -1;
            foreach (var row in GetGroup(group))
                if (row.stage > last) last = row.stage;
            return last;
        }
    }

    // ---------------- 402_modeRule ----------------

    public sealed class ModeRuleData : MvRow
    {
        public const string Table = "402_modeRule";
        public const string DefenceRule = "DefenceRule";
        public const string FixedLoadoutRule = "FixedLoadoutRule";
        public const string CourseRule = "CourseRule";
        public const string BossRule = "BossRule";
        public const string DefaultKey = "default";

        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Str("ruleClass", true).Int("layout")
            .Float("forwardSpeed").Bool("failOnWall").Bool("useLobbyDeck").Int("fixedWeaponTier").Int("craftPerMade")
            .Bool("useLobbyUpgrade").Bool("useAreaUpgrade").Bool("useRebirthPerk").Bool("useMetaStats")
            .FkKey("cardPoolId", CardPoolData.Table, true).Int("cycleGroup").FkKey("fieldGenKey", FieldGenData.Table).FkKey("climbKey", ClimbData.Table)
            .Int("hpSource").Int("runRewardToAccount").Bool("keepBars").Bool("useCheckpoint").Fk("themeId", ThemeData.Table)
            .Float("arenaSpawnZ").Floats("arenaLaneX").Float("arenaJitter").Float("arenaLineZ")
            .Int("baseHpMax").Int("xpPerKill").Int("xpBase").Int("xpStep").Int("cardChoices").Ints("cardTierWeight");

        public string ruleClass { get; private set; }
        public ModeLayout layout { get; private set; }
        public double forwardSpeed { get; private set; }
        public bool failOnWall { get; private set; }
        public bool useLobbyDeck { get; private set; }
        /// <summary>0 = 없음. 1~8이면 시작 덱·제작 게이트 무기 등급.</summary>
        public int fixedWeaponTier { get; private set; }
        public int craftPerMade { get; private set; }
        public bool useLobbyUpgrade { get; private set; }
        public bool useAreaUpgrade { get; private set; }
        public bool useRebirthPerk { get; private set; }
        public bool useMetaStats { get; private set; }
        /// <summary>`111_cardPool.key`.</summary>
        public string cardPoolId { get; private set; }
        /// <summary>`312_templateCycle.cycleGroup`. 0 = 본편 순환.</summary>
        public int cycleGroup { get; private set; }
        /// <summary>`313_fieldGen.key`. 빈칸 = default.</summary>
        public string fieldGenKey { get; private set; }
        /// <summary>`330_climb` 설정 행 key. 빈칸 = default.</summary>
        public string climbKey { get; private set; }
        public ModeHpSource hpSource { get; private set; }
        public ModeRunReward runRewardToAccount { get; private set; }
        public bool keepBars { get; private set; }
        public bool useCheckpoint { get; private set; }
        /// <summary>304 id. 0 = 현재 Area 테마.</summary>
        public int themeId { get; private set; }
        public double arenaSpawnZ { get; private set; }
        public IReadOnlyList<double> arenaLaneX { get; private set; }
        public double arenaJitter { get; private set; }
        public double arenaLineZ { get; private set; }
        public int baseHpMax { get; private set; }
        public int xpPerKill { get; private set; }
        public int xpBase { get; private set; }
        public int xpStep { get; private set; }
        public int cardChoices { get; private set; }
        /// <summary>카드 3택 tier 0~n 가중치.</summary>
        public IReadOnlyList<int> cardTierWeight { get; private set; }

        public ModeRuleData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            ruleClass = S(r, "ruleClass");
            layout = (ModeLayout)I(r, "layout");
            forwardSpeed = D(r, "forwardSpeed");
            failOnWall = Bool(r, "failOnWall");
            useLobbyDeck = Bool(r, "useLobbyDeck");
            fixedWeaponTier = I(r, "fixedWeaponTier");
            craftPerMade = I(r, "craftPerMade", 1);
            useLobbyUpgrade = Bool(r, "useLobbyUpgrade");
            useAreaUpgrade = Bool(r, "useAreaUpgrade");
            useRebirthPerk = Bool(r, "useRebirthPerk");
            useMetaStats = Bool(r, "useMetaStats");
            cardPoolId = S(r, "cardPoolId");
            cycleGroup = I(r, "cycleGroup");
            var field = S(r, "fieldGenKey");
            fieldGenKey = field.Length > 0 ? field : DefaultKey;
            var climb = S(r, "climbKey");
            climbKey = climb.Length > 0 ? climb : DefaultKey;
            hpSource = (ModeHpSource)I(r, "hpSource");
            runRewardToAccount = (ModeRunReward)I(r, "runRewardToAccount");
            keepBars = Bool(r, "keepBars");
            useCheckpoint = Bool(r, "useCheckpoint");
            themeId = I(r, "themeId");
            arenaSpawnZ = D(r, "arenaSpawnZ");
            arenaLaneX = Doubles(r, "arenaLaneX");
            arenaJitter = D(r, "arenaJitter");
            arenaLineZ = D(r, "arenaLineZ");
            baseHpMax = I(r, "baseHpMax");
            xpPerKill = I(r, "xpPerKill");
            xpBase = I(r, "xpBase");
            xpStep = I(r, "xpStep");
            cardChoices = I(r, "cardChoices");
            cardTierWeight = Ints(r, "cardTierWeight");
        }
    }

    // ---------------- 410_questGroup ----------------

    public sealed class QuestGroupData : MvRow
    {
        public const string Table = "410_questGroup";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Group().Str("name").Int("periodType").Int("category", true)
            .Int("groupType", true).Int("progressScope", true).Int("selectionType").Int("selectionCount");

        public int group { get; private set; }
        /// <summary>작성용 이름(화면 문구는 Localization `name:{id}`).</summary>
        public string name { get; private set; }
        public QuestPeriodType periodType { get; private set; }
        public QuestCategory category { get; private set; }
        public QuestGroupType groupType { get; private set; }
        public QuestProgressScope progressScope { get; private set; }
        public int selectionType { get; private set; }
        public int selectionCount { get; private set; }

        public QuestGroupData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            group = I(r, "group");
            name = S(r, "name");
            periodType = (QuestPeriodType)I(r, "periodType");
            category = (QuestCategory)I(r, "category");
            groupType = (QuestGroupType)I(r, "groupType");
            progressScope = (QuestProgressScope)I(r, "progressScope");
            selectionType = I(r, "selectionType");
            selectionCount = I(r, "selectionCount");
        }
    }

    /// <summary>413·414·420 공통(정크버스터즈 퀘스트 열).</summary>
    public abstract class QuestRowData : MvRow
    {
        public int questGroup { get; private set; }
        public QuestClearType clearType { get; private set; }
        /// <summary>0 = 전체. lobbyUpgrade는 1~3 시설, modeClear는 contentType.</summary>
        public int clearCondition { get; private set; }
        public long clearValue { get; private set; }
        public IReadOnlyList<ConsumeData> reward { get; private set; }

        protected QuestRowData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            questGroup = I(r, "questGroup");
            clearType = (QuestClearType)I(r, "clearType");
            clearCondition = I(r, "clearCondition");
            clearValue = L(r, "clearValue");
            reward = Consume(r, "reward");
        }
    }

    // ---------------- 413_repeatQuest ----------------

    public sealed class RepeatQuestData : QuestRowData
    {
        public const string Table = "413_repeatQuest";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().FkGroup("questGroup", QuestGroupData.Table, true).Int("sort")
            .Int("periodType").Int("clearType", true).Int("clearCondition").Long("clearValue", true).Long("clearValueStep").Consume("reward");

        public QuestPeriodType periodType { get; private set; }
        /// <summary>월간 반복: 목표 = clearValue + clearValueStep × 받은 횟수.</summary>
        public long clearValueStep { get; private set; }

        public RepeatQuestData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            periodType = (QuestPeriodType)I(r, "periodType");
            clearValueStep = L(r, "clearValueStep");
        }

        /// <summary>n번 받은 뒤의 목표.</summary>
        public long TargetAfter(int claimed) => clearValue + clearValueStep * claimed;
    }

    // ---------------- 414_passQuest ----------------

    public sealed class PassQuestData : QuestRowData
    {
        public const string Table = "414_passQuest";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().FkGroup("questGroup", QuestGroupData.Table, true).Str("banner")
            .Int("step", true).Int("sort").Int("periodType").Int("clearType", true).Int("clearCondition").Long("clearValue", true)
            .Consume("reward").Consume("premiumReward");

        public string banner { get; private set; }
        /// <summary>패스 레벨 L(1부터).</summary>
        public int step { get; private set; }
        public QuestPeriodType periodType { get; private set; }
        public IReadOnlyList<ConsumeData> premiumReward { get; private set; }

        public PassQuestData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            banner = S(r, "banner");
            step = I(r, "step");
            periodType = (QuestPeriodType)I(r, "periodType");
            premiumReward = Consume(r, "premiumReward");
        }
    }

    // ---------------- 420_achievement ----------------

    public sealed class AchievementData : QuestRowData
    {
        public const string Table = "420_achievement";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().FkGroup("questGroup", QuestGroupData.Table, true)
            .Int("progress", true).Int("clearType", true).Int("clearCondition").Long("prevClearValue").Long("clearValue", true).Consume("reward");

        /// <summary>단계(1부터, 차례로 깬다).</summary>
        public int progress { get; private set; }
        public long prevClearValue { get; private set; }

        public AchievementData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            progress = I(r, "progress");
            prevClearValue = L(r, "prevClearValue");
        }
    }

    // ---------------- 499_contentConfig (세로 키/값) ----------------

    public sealed class ContentConfigData : MvRow
    {
        public const string Table = "499_contentConfig";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Str("value").Str("unit");

        /// <summary>원문. `unit = consume`이면 `type/id/value`, 그 밖은 숫자. 묶음 이름 행(순번 000)은 빈칸.</summary>
        public string value { get; private set; }
        public string unit { get; private set; }

        public ContentConfigData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            value = S(r, "value");
            unit = S(r, "unit");
        }

        /// <summary>순번 000 = 묶음 이름 행(07 §7.9.2).</summary>
        public bool IsGroupName => id % 1000 == 0;
        public int GroupNo => id / 1000 % 1000;
    }

    public sealed class ContentConfigTable : MvTable<ContentConfigData>
    {
        public const string ConsumeUnit = "consume";

        public ContentConfigTable() : base(ContentConfigData.Schema, r => new ContentConfigData(r)) { }

        public bool Has(string key) => ByKey(key) is { IsGroupName: false };

        public string Raw(string key)
        {
            var row = ByKey(key);
            if (row == null || row.IsGroupName) throw new KeyNotFoundException($"{ContentConfigData.Table}: {key}");
            return row.value;
        }

        public double D(string key) => double.Parse(Raw(key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

        public int I(string key) => (int)System.Math.Floor(D(key) + 0.5);

        public bool B(string key) => I(key) != 0;

        /// <summary>`unit = consume` 값(`type/id/value`).</summary>
        public ConsumeData Consume(string key)
        {
            if (!TryConsume(Raw(key), out var data)) throw new System.FormatException($"{ContentConfigData.Table}: {key} = `{Raw(key)}`는 type/id/value가 아닙니다");
            return data;
        }

        internal static bool TryConsume(string raw, out ConsumeData data)
        {
            data = default;
            var parts = (raw ?? string.Empty).Split('/');
            if (parts.Length != 3) return false;
            if (!ConsumeData.TryParse(parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), null, null, out var list, out _) || list.Count != 1) return false;
            data = list[0];
            return true;
        }
    }
}
