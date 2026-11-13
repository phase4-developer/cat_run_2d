using System;
using System.Collections.Generic;
using jjevol;
using jjevol.Data;

namespace MV.Data
{
    /// <summary>
    /// 모든 표 참조 모음(기획서 07 §7.1.5). 런타임은 <see cref="Current"/>만 본다.
    /// 로드 순서: <see cref="RegisterAll"/> → TableRepository.LoadAllAsync → <see cref="MvTableValidator.ValidateRuntime"/> → <see cref="BuildIndexes"/>.
    /// 시험·핫 리로드는 <see cref="LoadAll(ITableSource)"/>로 새 인스턴스를 채운다.
    /// </summary>
    public sealed class MvTables
    {
        /// <summary>검증을 통과한 현재 표. 부팅(MV.Boot) 또는 핫 리로드가 교체한다.</summary>
        public static MvTables Current { get; set; }

        // 0
        public ConfigTable Config { get; } = new ConfigTable();
        public MvWealthTable Wealth { get; } = new MvWealthTable();
        public MvTable<PlayResourceData> PlayResource { get; } = new MvTable<PlayResourceData>(PlayResourceData.Schema, r => new PlayResourceData(r));
        public MvTable<SoundData> Sound { get; } = new MvTable<SoundData>(SoundData.Schema, r => new SoundData(r));
        public MvTable<EffectData> Effect { get; } = new MvTable<EffectData>(EffectData.Schema, r => new EffectData(r));
        public MvTable<ContentOpenData> ContentOpen { get; } = new MvTable<ContentOpenData>(ContentOpenData.Schema, r => new ContentOpenData(r));
        public MvTable<BalancePresetData> BalancePreset { get; } = new MvTable<BalancePresetData>(BalancePresetData.Schema, r => new BalancePresetData(r));
        public ConsumeTypeTable ConsumeType { get; } = new ConsumeTypeTable();
        public MvTable<AdPlacementData> AdPlacement { get; } = new MvTable<AdPlacementData>(AdPlacementData.Schema, r => new AdPlacementData(r));
        public MvTable<UiResourceData> UiResource { get; } = new MvTable<UiResourceData>(UiResourceData.Schema, r => new UiResourceData(r), d => d.group);

        // 1
        public MvTable<LobbyUpgradeData> LobbyUpgrade { get; } = new MvTable<LobbyUpgradeData>(LobbyUpgradeData.Schema, r => new LobbyUpgradeData(r));
        public MvTable<StairLevelData> StairLevel { get; } = new MvTable<StairLevelData>(StairLevelData.Schema, r => new StairLevelData(r));
        public MvTable<WeaponLevelData> WeaponLevel { get; } = new MvTable<WeaponLevelData>(WeaponLevelData.Schema, r => new WeaponLevelData(r));
        public MvTable<AreaUpgradeData> AreaUpgrade { get; } = new MvTable<AreaUpgradeData>(AreaUpgradeData.Schema, r => new AreaUpgradeData(r));
        public MvTable<RebirthPerkData> RebirthPerk { get; } = new MvTable<RebirthPerkData>(RebirthPerkData.Schema, r => new RebirthPerkData(r));
        public LevelPriceTable LevelPrice { get; } = new LevelPriceTable();

        // 1xx
        public MvTable<CatData> Cat { get; } = new MvTable<CatData>(CatData.Schema, r => new CatData(r));
        public MvTable<EnemyData> Enemy { get; } = new MvTable<EnemyData>(EnemyData.Schema, r => new EnemyData(r));
        public MvTable<ItemData> Item { get; } = new MvTable<ItemData>(ItemData.Schema, r => new ItemData(r));
        public MvTable<ObjectKindData> ObjectKind { get; } = new MvTable<ObjectKindData>(ObjectKindData.Schema, r => new ObjectKindData(r));
        public MvTable<HitReactionData> HitReaction { get; } = new MvTable<HitReactionData>(HitReactionData.Schema, r => new HitReactionData(r));
        public MvTable<ChargeGateData> ChargeGate { get; } = new MvTable<ChargeGateData>(ChargeGateData.Schema, r => new ChargeGateData(r));
        public MvTable<ReceiveConditionData> ReceiveCondition { get; } = new MvTable<ReceiveConditionData>(ReceiveConditionData.Schema, r => new ReceiveConditionData(r));
        public MvTable<MoveAiData> MoveAi { get; } = new MvTable<MoveAiData>(MoveAiData.Schema, r => new MoveAiData(r));
        public MvTable<CardData> Card { get; } = new MvTable<CardData>(CardData.Schema, r => new CardData(r), d => d.group);
        public MvTable<CardPoolData> CardPool { get; } = new MvTable<CardPoolData>(CardPoolData.Schema, r => new CardPoolData(r), d => d.group);
        public MvTable<CardEffectData> CardEffect { get; } = new MvTable<CardEffectData>(CardEffectData.Schema, r => new CardEffectData(r));
        public MvTable<GodData> God { get; } = new MvTable<GodData>(GodData.Schema, r => new GodData(r));
        public MvTable<HpRuleData> HpRule { get; } = new MvTable<HpRuleData>(HpRuleData.Schema, r => new HpRuleData(r));
        public MvTable<CardTierData> CardTier { get; } = new MvTable<CardTierData>(CardTierData.Schema, r => new CardTierData(r));
        public MvTable<CharLookData> CharLook { get; } = new MvTable<CharLookData>(CharLookData.Schema, r => new CharLookData(r));

        // 3xx
        public RunConfigTable RunConfig { get; } = new RunConfigTable();
        public MvTable<ZoneData> Zone { get; } = new MvTable<ZoneData>(ZoneData.Schema, r => new ZoneData(r));
        public MvTable<AreaData> Area { get; } = new MvTable<AreaData>(AreaData.Schema, r => new AreaData(r));
        public MvTable<AreaFloorData> AreaFloor { get; } = new MvTable<AreaFloorData>(AreaFloorData.Schema, r => new AreaFloorData(r), d => d.group);
        public MvTable<ThemeData> Theme { get; } = new MvTable<ThemeData>(ThemeData.Schema, r => new ThemeData(r));
        public MvTable<StageObjectData> StageObject { get; } = new MvTable<StageObjectData>(StageObjectData.Schema, r => new StageObjectData(r));
        public MvTable<TemplateData> Template { get; } = new MvTable<TemplateData>(TemplateData.Schema, r => new TemplateData(r));
        public TemplateObjectTable TemplateObject { get; } = new TemplateObjectTable();
        public TemplateCycleTable TemplateCycle { get; } = new TemplateCycleTable();
        public MvTable<FieldGenData> FieldGen { get; } = new MvTable<FieldGenData>(FieldGenData.Schema, r => new FieldGenData(r));
        public MvTable<SpawnGroupData> SpawnGroup { get; } = new MvTable<SpawnGroupData>(SpawnGroupData.Schema, r => new SpawnGroupData(r), d => d.group);
        public MvTable<CameraData> Camera { get; } = new MvTable<CameraData>(CameraData.Schema, r => new CameraData(r));
        public ClimbTable Climb { get; } = new ClimbTable();

        // 4xx 모드·퀘스트·콘텐츠 상수(08 §8.4, §8.5.1, §8.5.6, §8.5.13)
        public MvTable<ContentData> Content { get; } = new MvTable<ContentData>(ContentData.Schema, r => new ContentData(r));
        public ModeStageTable ModeStage { get; } = new ModeStageTable();
        public MvTable<ModeRuleData> ModeRule { get; } = new MvTable<ModeRuleData>(ModeRuleData.Schema, r => new ModeRuleData(r));
        public MvTable<QuestGroupData> QuestGroup { get; } = new MvTable<QuestGroupData>(QuestGroupData.Schema, r => new QuestGroupData(r), d => d.group);
        public MvTable<RepeatQuestData> RepeatQuest { get; } = new MvTable<RepeatQuestData>(RepeatQuestData.Schema, r => new RepeatQuestData(r), d => d.questGroup);
        public MvTable<PassQuestData> PassQuest { get; } = new MvTable<PassQuestData>(PassQuestData.Schema, r => new PassQuestData(r), d => d.questGroup);
        public MvTable<AchievementData> Achievement { get; } = new MvTable<AchievementData>(AchievementData.Schema, r => new AchievementData(r), d => d.questGroup);
        public ContentConfigTable ContentConfig { get; } = new ContentConfigTable();

        // 4xx 메타 시스템(08 §8.3.5, §8.5)
        public MvTable<PetData> Pet { get; } = new MvTable<PetData>(PetData.Schema, r => new PetData(r));
        public MvTable<PetBuildData> PetBuild { get; } = new MvTable<PetBuildData>(PetBuildData.Schema, r => new PetBuildData(r), d => d.tier);
        public ResearchTable Research { get; } = new ResearchTable();
        public MvTable<FriendData> Friend { get; } = new MvTable<FriendData>(FriendData.Schema, r => new FriendData(r));
        public MvTable<FriendPowerData> FriendPower { get; } = new MvTable<FriendPowerData>(FriendPowerData.Schema, r => new FriendPowerData(r));
        public MvTable<RuneData> Rune { get; } = new MvTable<RuneData>(RuneData.Schema, r => new RuneData(r));
        public MvTable<GearPartData> GearPart { get; } = new MvTable<GearPartData>(GearPartData.Schema, r => new GearPartData(r));
        public MvTable<GearRarityData> GearRarity { get; } = new MvTable<GearRarityData>(GearRarityData.Schema, r => new GearRarityData(r));
        public MvTable<BlessingData> Blessing { get; } = new MvTable<BlessingData>(BlessingData.Schema, r => new BlessingData(r));
        public MvTable<AttendanceData> Attendance { get; } = new MvTable<AttendanceData>(AttendanceData.Schema, r => new AttendanceData(r));
        public MvTable<RoadMapData> RoadMap { get; } = new MvTable<RoadMapData>(RoadMapData.Schema, r => new RoadMapData(r));
        public MvTable<GodLevelData> GodLevel { get; } = new MvTable<GodLevelData>(GodLevelData.Schema, r => new GodLevelData(r));

        // 7xx 상점(08 §8.5.7, 결제 연결은 MV-PLAT-IAP)
        public MvTable<ProductTabData> ProductTab { get; } = new MvTable<ProductTabData>(ProductTabData.Schema, r => new ProductTabData(r));
        public MvTable<ProductData> Product { get; } = new MvTable<ProductData>(ProductData.Schema, r => new ProductData(r), d => d.tabId);
        public MvTable<ShopSectionData> ShopSection { get; } = new MvTable<ShopSectionData>(ShopSectionData.Schema, r => new ShopSectionData(r));

        // 8xx
        public MvTable<DropData> Drop { get; } = new MvTable<DropData>(DropData.Schema, r => new DropData(r), d => d.dropGroup);
        public MvTable<RandomBoxData> RandomBox { get; } = new MvTable<RandomBoxData>(RandomBoxData.Schema, r => new RandomBoxData(r), d => d.group);
        public MvTable<GuaranteeLinkData> GuaranteeLink { get; } = new MvTable<GuaranteeLinkData>(GuaranteeLinkData.Schema, r => new GuaranteeLinkData(r));
        public MvTable<GuaranteeData> Guarantee { get; } = new MvTable<GuaranteeData>(GuaranteeData.Schema, r => new GuaranteeData(r));

        readonly List<IMvTable> _all;
        readonly Dictionary<string, IMvTable> _byName;

        public MvTables()
        {
            _all = new List<IMvTable>
            {
                Config, Wealth, PlayResource, Sound, Effect, ContentOpen, BalancePreset, ConsumeType, AdPlacement, UiResource,
                LobbyUpgrade, StairLevel, WeaponLevel, AreaUpgrade, RebirthPerk, LevelPrice,
                Cat, Enemy, Item, ObjectKind, HitReaction, ChargeGate, ReceiveCondition, MoveAi, Card, CardPool, CardEffect,
                God, HpRule, CardTier, CharLook,
                RunConfig, Zone, Area, AreaFloor, Theme, StageObject, Template, TemplateObject, TemplateCycle, FieldGen, SpawnGroup, Camera,
                Climb,
                Content, ModeStage, ModeRule, QuestGroup, RepeatQuest, PassQuest, Achievement, ContentConfig,
                Pet, PetBuild, Research, Friend, FriendPower, Rune, GearPart, GearRarity, Blessing, Attendance, RoadMap, GodLevel,
                ProductTab, Product, ShopSection,
                Drop, RandomBox, GuaranteeLink, Guarantee,
            };
            _byName = new Dictionary<string, IMvTable>(StringComparer.Ordinal);
            foreach (var t in _all) _byName.Add(t.TableName, t);
        }

        /// <summary>등록 순서 그대로의 모든 표.</summary>
        public IReadOnlyList<IMvTable> All => _all;

        /// <summary>표 파일 이름(`105_objectKind`)으로 찾는다. 없으면 null.</summary>
        public IMvTable Find(string tableName) => tableName != null && _byName.TryGetValue(tableName, out var t) ? t : null;

        /// <summary>모든 표를 Framework 저장소에 등록한다(10 §10.7 4단계). 로드는 호출자가 TableRepository.LoadAllAsync로 1회.</summary>
        public void RegisterAll()
        {
            foreach (var t in _all) TableRepository.Register(t.Table);
        }

        /// <summary>저장소를 거치지 않고 지정 원본에서 모든 표를 읽는다(시험·핫 리로드, 07 §7.1.9).</summary>
        public void LoadAll(ITableSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var t in _all) t.Table.LoadDatas(source);
        }

        /// <summary>검증 뒤 표끼리 엮는 캐시를 만든다. 핫 리로드 뒤에도 다시 부른다.</summary>
        public void BuildIndexes()
        {
            TemplateObject.BuildZOrder();
            Drops = new MvDropRoller(this);
        }

        /// <summary>801·802 추첨·확률 표시. <see cref="BuildIndexes"/>가 만든다(D-85: 패키지가 나오면 MvDropSource로 교체).</summary>
        public IMvDropSource Drops { get; private set; }

        /// <summary>Zone 순서(0기반 zoneIndex, 07 §7.4.1) — 그 Area 행의 <c>302_area.zoneId</c>가 301 sort 순에서 몇 번째인지. 없으면 -1.</summary>
        public int ZoneIndexOfArea(int areaNo)
        {
            int zoneId = 0;
            foreach (var a in Area.Datas) if (a.areaNo == areaNo) { zoneId = a.zoneId; break; }
            if (zoneId == 0) return -1;
            var zones = Zone.Sorted;
            for (int i = 0; i < zones.Count; i++)
                if (zones[i].id == zoneId) return i;
            return -1;
        }

        /// <summary>Zone의 첫 Area 번호 = <c>302_area</c>에서 그 zoneId 행의 areaNo 최소(301 areaFrom 열 대신 계산). 행이 없으면 0.</summary>
        public int ZoneFirstArea(int zoneId)
        {
            int first = 0;
            foreach (var a in Area.Datas)
                if (a.zoneId == zoneId && (first == 0 || a.areaNo < first)) first = a.areaNo;
            return first;
        }

        /// <summary>Zone의 마지막 Area 번호 = <c>302_area</c>에서 그 zoneId 행의 areaNo 최대(301 areaTo 열 대신 계산). 행이 없으면 0.</summary>
        public int ZoneLastArea(int zoneId)
        {
            int last = 0;
            foreach (var a in Area.Datas)
                if (a.zoneId == zoneId && a.areaNo > last) last = a.areaNo;
            return last;
        }

        /// <summary>
        /// Area당 Floor 수 = 302가 쓰는 <c>303_areaFloor</c> 묶음의 floor 최댓값(옛 300 FLOOR_COUNT를 계산으로 바꿈).
        /// 모드 전용 묶음(302가 쓰지 않는 묶음)은 세지 않는다. 행이 없으면 1.
        /// </summary>
        public int FloorCount()
        {
            var groups = new HashSet<int>();
            foreach (var a in Area.Datas) groups.Add(a.floorGroup);
            int max = 0;
            foreach (var f in AreaFloor.Datas)
                if (groups.Contains(f.group) && f.floor > max) max = f.floor;
            return Math.Max(1, max);
        }

        /// <summary>로비 캣타워로 올리는 시작 계단 Lv 상한 = <c>11_stairLevel</c> lv 최댓값(옛 300 LOBBY_STAIR_MAX_LV를 계산으로 바꿈). 행이 없으면 1.</summary>
        public int StairMaxLv()
        {
            int max = 0;
            foreach (var r in StairLevel.Datas) if (r.lv > max) max = r.lv;
            return Math.Max(1, max);
        }

        /// <summary>
        /// 성장 행의 최대 구매 수. 로비 강화 계단(StartStairLv)·지팡이(DeckAdd)는 표에 적지 않고 계산한다(사용자 결정 「행 수·합계로 계산되는 값은 계산」):
        /// 계단 = Σ<c>11_stairLevel.need</c>(lv &lt; 상한 Lv), 지팡이 = Σ<c>12_weaponLevel.need</c>(lv &lt; 마지막 lv) + <c>WEAPON_EXTRA_BUYS</c>.
        /// 그 밖 행은 표의 <c>max</c> 열.
        /// </summary>
        public int LevelMax(ILevelPriced row)
        {
            if (row is LobbyUpgradeData lobby)
            {
                if (lobby.effectType == LobbyEffectType.StartStairLv)
                {
                    int top = StairMaxLv(), sum = 0;
                    foreach (var r in StairLevel.Datas) if (r.lv < top) sum += r.need;
                    return sum;
                }
                if (lobby.effectType == LobbyEffectType.DeckAdd)
                {
                    int last = 0, sum = 0;
                    foreach (var r in WeaponLevel.Datas) if (r.lv > last) last = r.lv;
                    foreach (var r in WeaponLevel.Datas) if (r.lv >= 1 && r.lv < last) sum += r.need;
                    return sum + RunConfig.I(RunConfigKeys.WEAPON_EXTRA_BUYS);
                }
            }
            return row?.max ?? 0;
        }

        /// <summary>로비 강화 행이 최대 구매 수를 계산하는지(계단·지팡이). 이 행의 <c>max</c> 칸은 비운다.</summary>
        public static bool IsComputedMax(LobbyUpgradeData row) =>
            row != null && (row.effectType == LobbyEffectType.StartStairLv || row.effectType == LobbyEffectType.DeckAdd);

        /// <summary><c>16_levelPrice</c> 묶음의 targetLevel 최댓값(펫 최대 Lv처럼 표에 따로 열이 없는 상한). 행이 없으면 0.</summary>
        public int LevelPriceMaxLevel(int group)
        {
            int max = 0;
            foreach (var r in LevelPrice.GetGroup(group)) if (r.targetLevel > max) max = r.targetLevel;
            return max;
        }

        /// <summary>출석 한 바퀴 일수 = <c>485_attendance</c> day 최댓값(옛 코드 상수 7). 행이 없으면 0.</summary>
        public int AttendanceCycleDays()
        {
            int max = 0;
            foreach (var r in Attendance.Datas) if (r.day > max) max = r.day;
            return max;
        }

        /// <summary>장비 강화 상한 = <c>499_contentConfig gear.maxUpgrade</c>(옛 코드 상수 30).</summary>
        public int GearMaxLevel() => ContentConfig.I(ContentConfigKeys.Gear_MaxUpgrade);

        /// <summary>런 동행 최대 수 = <c>499 pet.slots</c> + <c>friend.slotsMax</c>(04 §4.20.5 「펫 2 + 친구 3」, 옛 코드 상수 5).</summary>
        public int MaxCompanions() => ContentConfig.I(ContentConfigKeys.Pet_Slots) + ContentConfig.I(ContentConfigKeys.Friend_SlotsMax);

        /// <summary>
        /// 무기 상한 등급 = <c>103_item</c> active 행의 tier 최대(MV-SPEC-02 Q-08). 런 W·덱 길이 = 이 값 + 1.
        /// 등급 수는 표 설정만으로 바뀐다(최소 1).
        /// </summary>
        public int WeaponTierMax()
        {
            int max = 0;
            foreach (var it in Item.Datas) if (it.active && it.tier > max) max = it.tier;
            return Math.Max(1, max);
        }
    }
}
