namespace MV.Data
{
    // 8xx 확률 표(기획서 07 §7.9.3, JJEvol 표 규격 v1 §6~9).

    /// <summary>801·802 공통 결과 행.</summary>
    public abstract class LootRowData : MvRow
    {
        public int rewardType { get; private set; }
        public int rewardId { get; private set; }
        public long rewardValueMin { get; private set; }
        public long rewardValueMax { get; private set; }
        public int weight { get; private set; }
        public string icon { get; private set; }

        protected LootRowData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            rewardType = I(r, "rewardType");
            rewardId = I(r, "rewardId");
            rewardValueMin = L(r, "rewardValue_min");
            rewardValueMax = L(r, "rewardValue_max");
            weight = I(r, "weight");
            icon = S(r, "icon");
        }

        /// <summary>꽝(rewardType 0).</summary>
        public bool IsEmpty => rewardType == 0;
        public ConsumeType Type => (ConsumeType)rewardType;
        /// <summary>묶음 ID(801 dropGroup, 802 group).</summary>
        public abstract int LootGroup { get; }
        /// <summary>칸(802 subGroup, 801은 1).</summary>
        public virtual int Slot => 1;
    }

    // ---------------- 801_drop ----------------

    public sealed class DropData : LootRowData
    {
        public const string Table = "801_drop";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Group("dropGroup").RunConsume()
            .Int("rewardType", true).Int("rewardId", true).Long("rewardValue_min", true).Long("rewardValue_max", true)
            .Enum<RewardValueRule>("rewardValueRule").Int("weight", true).Res("icon", MvAtlas.UiIcon);

        public int dropGroup { get; private set; }
        /// <summary>게임 확장 열. Flat이 아니면 min·max는 0이고 추첨 뒤 호출자가 수량을 계산한다.</summary>
        public RewardValueRule rewardValueRule { get; private set; }

        public DropData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            dropGroup = I(r, "dropGroup");
            rewardValueRule = E(r, "rewardValueRule", RewardValueRule.Flat);
        }

        public override int LootGroup => dropGroup;
    }

    // ---------------- 802_randomBox ----------------

    public sealed class RandomBoxData : LootRowData
    {
        public const string Table = "802_randomBox";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Group().RunConsume().Int("subGroup", true)
            .Int("rewardType", true).Int("rewardId", true).Long("rewardValue_min", true).Long("rewardValue_max", true)
            .Int("weight", true).Res("icon", MvAtlas.UiIcon).Str("nameKey");

        public int group { get; private set; }
        public int subGroup { get; private set; }

        public RandomBoxData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            group = I(r, "group");
            subGroup = I(r, "subGroup");
        }

        public override int LootGroup => group;
        public override int Slot => subGroup;
    }

    // ---------------- 803_randomGuaranteeLink ----------------

    public sealed class GuaranteeLinkData : MvRow
    {
        public const string Table = "803_randomGuaranteeLink";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Fk("guaranteeId", GuaranteeData.Table, true)
            .FkGroup("sourceBoxGroup", RandomBoxData.Table, true).Int("sourceBoxSubGroup");

        public int guaranteeId { get; private set; }
        public int sourceBoxGroup { get; private set; }
        /// <summary>0 = 상자 1회 = 1번 셈.</summary>
        public int sourceBoxSubGroup { get; private set; }

        public GuaranteeLinkData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            guaranteeId = I(r, "guaranteeId");
            sourceBoxGroup = I(r, "sourceBoxGroup");
            sourceBoxSubGroup = I(r, "sourceBoxSubGroup");
        }
    }

    // ---------------- 804_randomGuarantee ----------------

    public sealed class GuaranteeData : MvRow
    {
        public const string Table = "804_randomGuarantee";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Bool("active").Int("countType", true)
            .Int("triggerCount", true).Int("rewardMode", true).FkGroup("rewardBoxGroup", RandomBoxData.Table, true)
            .Int("rewardBoxSubGroup").Int("priority");

        /// <summary>1 FailStreak / 2 AttemptCycle.</summary>
        public int countType { get; private set; }
        public int triggerCount { get; private set; }
        /// <summary>1 Replace / 2 Add.</summary>
        public int rewardMode { get; private set; }
        public int rewardBoxGroup { get; private set; }
        public int rewardBoxSubGroup { get; private set; }
        public int priority { get; private set; }

        public GuaranteeData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            countType = I(r, "countType");
            triggerCount = I(r, "triggerCount");
            rewardMode = I(r, "rewardMode");
            rewardBoxGroup = I(r, "rewardBoxGroup");
            rewardBoxSubGroup = I(r, "rewardBoxSubGroup");
            priority = I(r, "priority");
        }
    }
}
