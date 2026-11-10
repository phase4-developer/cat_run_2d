// 499_contentConfig 키 상수(08 §8.5.13). 키를 더하면 TSV와 이 목록을 같이 고친다. 검증기가 TSV와 대조한다(누락·남음 = 오류).
namespace MV.Data
{
    /// <summary>`499_contentConfig` 키 상수. 코드는 이 상수로만 키를 쓴다(묶음 이름 행은 넣지 않는다).</summary>
    public static class ContentConfigKeys
    {
        public const string Mode_AdTicketDaily = "mode.adTicketDaily";
        public const string Mode_SweepChuruRatio = "mode.sweepChuruRatio";
        public const string Box_FallbackDia_Paper = "box.fallbackDia.paper";
        public const string Box_FallbackDia_Wood = "box.fallbackDia.wood";
        public const string Box_FallbackDia_Gold = "box.fallbackDia.gold";
        public const string Pet_SummonPrice = "pet.summonPrice";
        public const string Pet_SummonFood = "pet.summonFood";
        public const string Pet_FoodPerClear = "pet.foodPerClear";
        public const string Pet_CardsToUnlock = "pet.cardsToUnlock";
        public const string Pet_Slots = "pet.slots";
        public const string Pet_LevelPct = "pet.levelPct";
        public const string Pet_BuildSlotsMax = "pet.buildSlotsMax";
        public const string Pet_BuildSlotEvery = "pet.buildSlotEvery";
        public const string Pet_BuildCost0 = "pet.buildCost0";
        public const string Pet_BuildCost1 = "pet.buildCost1";
        public const string Pet_BuildCost2 = "pet.buildCost2";
        public const string Friend_SlotsMax = "friend.slotsMax";
        public const string Friend_SlotPriceDia = "friend.slotPriceDia";
        public const string Friend_CoinPerClear = "friend.coinPerClear";
        public const string God_ShardPerClear = "god.shardPerClear";
        public const string Gear_SetPieces = "gear.setPieces";
        public const string Gear_SetDmg = "gear.setDmg";
        public const string Gear_MaxUpgrade = "gear.maxUpgrade";
        public const string Blessing_ShopRuns = "blessing.shopRuns";
        public const string Event_CycleDays = "event.cycleDays";
        public const string Event_ActiveDays = "event.activeDays";
        public const string Rebirth_FastTravelPerRebirth = "rebirth.fastTravelPerRebirth";
        public const string Rebirth_SkipDiaPer10Min = "rebirth.skipDiaPer10Min";
        /// <summary>냥환생 쿨타임 보라 마법석 스킵(MV-SPEC-02 Q-30: 꺼 둠, BM은 05 탭 뒤).</summary>
        public const string Rebirth_SkipEnabled = "rebirth.skipEnabled";
        public const string Energy_Enabled = "energy.enabled";
        public const string Horde_Enabled = "horde.enabled";

        /// <summary>모든 값 키(31개).</summary>
        public static readonly string[] All =
        {
            Mode_AdTicketDaily,
            Mode_SweepChuruRatio,
            Box_FallbackDia_Paper,
            Box_FallbackDia_Wood,
            Box_FallbackDia_Gold,
            Pet_SummonPrice,
            Pet_SummonFood,
            Pet_FoodPerClear,
            Pet_CardsToUnlock,
            Pet_Slots,
            Pet_LevelPct,
            Pet_BuildSlotsMax,
            Pet_BuildSlotEvery,
            Pet_BuildCost0,
            Pet_BuildCost1,
            Pet_BuildCost2,
            Friend_SlotsMax,
            Friend_SlotPriceDia,
            Friend_CoinPerClear,
            God_ShardPerClear,
            Gear_SetPieces,
            Gear_SetDmg,
            Gear_MaxUpgrade,
            Blessing_ShopRuns,
            Event_CycleDays,
            Event_ActiveDays,
            Rebirth_FastTravelPerRebirth,
            Rebirth_SkipDiaPer10Min,
            Rebirth_SkipEnabled,
            Energy_Enabled,
            Horde_Enabled,
        };
    }
}
