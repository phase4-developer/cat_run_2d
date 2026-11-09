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

        // 3xx
        public RunConfigTable RunConfig { get; } = new RunConfigTable();
        public MvTable<ZoneData> Zone { get; } = new MvTable<ZoneData>(ZoneData.Schema, r => new ZoneData(r));
        public MvTable<AreaData> Area { get; } = new MvTable<AreaData>(AreaData.Schema, r => new AreaData(r));
        public MvTable<AreaFloorData> AreaFloor { get; } = new MvTable<AreaFloorData>(AreaFloorData.Schema, r => new AreaFloorData(r), d => d.group);
        public MvTable<ThemeData> Theme { get; } = new MvTable<ThemeData>(ThemeData.Schema, r => new ThemeData(r));
        public MvTable<TemplateData> Template { get; } = new MvTable<TemplateData>(TemplateData.Schema, r => new TemplateData(r));
        public TemplateObjectTable TemplateObject { get; } = new TemplateObjectTable();
        public TemplateCycleTable TemplateCycle { get; } = new TemplateCycleTable();
        public MvTable<FieldGenData> FieldGen { get; } = new MvTable<FieldGenData>(FieldGenData.Schema, r => new FieldGenData(r));
        public MvTable<SpawnGroupData> SpawnGroup { get; } = new MvTable<SpawnGroupData>(SpawnGroupData.Schema, r => new SpawnGroupData(r), d => d.group);
        public MvTable<CameraData> Camera { get; } = new MvTable<CameraData>(CameraData.Schema, r => new CameraData(r));
        public ClimbTable Climb { get; } = new ClimbTable();

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
                God, HpRule, CardTier,
                RunConfig, Zone, Area, AreaFloor, Theme, Template, TemplateObject, TemplateCycle, FieldGen, SpawnGroup, Camera,
                Climb,
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
        }

        /// <summary>Zone 순서(0기반 zoneIndex, 07 §7.4.1) — sort 순 위치.</summary>
        public int ZoneIndexOfArea(int areaNo)
        {
            var zones = Zone.Sorted;
            for (int i = 0; i < zones.Count; i++)
                if (areaNo >= zones[i].areaFrom && areaNo <= zones[i].areaTo) return i;
            return -1;
        }
    }
}
