using System.Collections.Generic;

namespace MV.Data
{
    // 0번대 공통 표(기획서 07 §7.3.1, §7.3.2, §7.8.8~7.8.13, §7.11).

    // ---------------- 00_config ----------------

    public sealed class ConfigData : MvRow
    {
        public const string Table = "00_config";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().FreeIdRule().OneRow()
            .Int("dataVersion", true).Ints("startDeck", true).FkKey("startGodKey", "113_god", true).Int("collectAdMul", true)
            .Int("interstitialMinArea", true).Float("rebirthArmSec", true).Int("rebirthCooldownSec", true)
            .Int("adDayUtcOffsetHour", true).Float("progressResetArmSec", true).Bool("defaultMute", true);

        public int dataVersion { get; private set; }
        /// <summary>인덱스 = 무기 등급(0 미사용).</summary>
        public IReadOnlyList<int> startDeck { get; private set; }
        public string startGodKey { get; private set; }
        public int collectAdMul { get; private set; }
        public int interstitialMinArea { get; private set; }
        public double rebirthArmSec { get; private set; }
        public int rebirthCooldownSec { get; private set; }
        public int adDayUtcOffsetHour { get; private set; }
        public double progressResetArmSec { get; private set; }
        public bool defaultMute { get; private set; }

        public ConfigData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            dataVersion = I(r, "dataVersion");
            startDeck = Ints(r, "startDeck");
            startGodKey = S(r, "startGodKey");
            collectAdMul = I(r, "collectAdMul");
            interstitialMinArea = I(r, "interstitialMinArea");
            rebirthArmSec = D(r, "rebirthArmSec");
            rebirthCooldownSec = I(r, "rebirthCooldownSec");
            adDayUtcOffsetHour = I(r, "adDayUtcOffsetHour");
            progressResetArmSec = D(r, "progressResetArmSec");
            defaultMute = Bool(r, "defaultMute");
        }
    }

    public sealed class ConfigTable : MvTable<ConfigData>
    {
        public ConfigTable() : base(ConfigData.Schema, r => new ConfigData(r)) { }
        /// <summary>단일 행(검증기가 정확히 1행인지 본다).</summary>
        public ConfigData Value => Datas.Count > 0 ? Datas[0] : null;
    }

    // ---------------- 02_playResource ----------------

    public sealed class PlayResourceData : MvRow
    {
        public const string Table = "02_playResource";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Str("type").Int("chargeType", true)
            .Int("chargeSec").Int("chargeAmount").Int("chargeMax").Int("sort").Bool("active").Res("icon", MvAtlas.UiIcon);

        public string type { get; private set; }
        /// <summary>1 시간 회복 / 2 일일 초기화.</summary>
        public int chargeType { get; private set; }
        public int chargeSec { get; private set; }
        public int chargeAmount { get; private set; }
        public int chargeMax { get; private set; }
        public string icon { get; private set; }

        public PlayResourceData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            type = S(r, "type");
            chargeType = I(r, "chargeType");
            chargeSec = I(r, "chargeSec");
            chargeAmount = I(r, "chargeAmount");
            chargeMax = I(r, "chargeMax");
            icon = S(r, "icon");
        }
    }

    // ---------------- 03_sound ----------------

    public enum SoundType { Sfx, Music }

    public sealed class SoundData : MvRow
    {
        public const string Table = "03_sound";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Enum<SoundType>("soundType", true)
            .Res("soundName", MvAtlas.Address, true).Float("volume").Float("pitchRand").Int("gapMs");

        public SoundType soundType { get; private set; }
        public string soundName { get; private set; }
        public double volume { get; private set; }
        public double pitchRand { get; private set; }
        public int gapMs { get; private set; }

        public SoundData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            soundType = E(r, "soundType", SoundType.Sfx);
            soundName = S(r, "soundName");
            volume = D(r, "volume", 1);
            pitchRand = D(r, "pitchRand");
            gapMs = I(r, "gapMs");
        }
    }

    // ---------------- 04_effect ----------------

    /// <summary>이펙트 위치 기준(07 §7.11.2, 06 §6.4). 빈칸 = 호출 함수 기본(<see cref="EffectData.anchor"/> null).</summary>
    public enum EffectAnchor { hit, obj, player, climbTop }

    public sealed class EffectData : MvRow
    {
        public const string Table = "04_effect";
        /// <summary>`type` 값: 묶음(children을 함께 재생) / 묶음 안 자식 1개의 인자 행(07 §7.11.2).</summary>
        public const string GroupType = "group", PartType = "part";
        /// <summary>`when` 조건 이름(호출부가 지금 성립하는 이름을 넘긴다. 행의 이름이 모두 성립해야 재생).</summary>
        public static readonly IReadOnlyList<string> WhenTokens = new[] { "normal", "crit", "coin", "gem", "fire", "bolt" };
        // 열 정의 정본은 06 §6.4. type·blend 값 목록이 06에서 자라므로 문자열로 둔다. anchor는 07 §7.11.2 enum.
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Str("type").Res("prefab", MvAtlas.Address)
            .Int("poolSize").Str("blend").Float("sortBias").Str("color").Float("count").Float("speed").Float("size")
            .Float("life").Float("grav").Float("radius").Str("flag").Strs("children").Float("y").Strs("when").Float("dz").Float("jx").Float("jz").Enum<EffectAnchor>("anchor")
            .Float("chance").ConfigKey("shake", prefix: true).ConfigKey("hitStop");

        public string type { get; private set; }
        public string prefab { get; private set; }
        public int poolSize { get; private set; }
        public string blend { get; private set; }
        public double sortBias { get; private set; }
        public string color { get; private set; }
        public double count { get; private set; }
        public double speed { get; private set; }
        public double size { get; private set; }
        public double life { get; private set; }
        public double grav { get; private set; }
        /// <summary>grav 칸이 채워져 있는지(0 = 중력 없음과 빈칸 = 종류 기본값을 가른다).</summary>
        public bool hasGrav { get; private set; }
        public double radius { get; private set; }
        public string flag { get; private set; }
        public IReadOnlyList<string> children { get; private set; }
        public double y { get; private set; }
        /// <summary>part 행 재생 조건(`/` 목록, 모두 성립해야 재생). 빈칸 = 늘 재생.</summary>
        public IReadOnlyList<string> when { get; private set; }
        /// <summary>z 오프셋(m).</summary>
        public double dz { get; private set; }
        /// <summary>x·z 무작위 흔들기 반폭(m, 균등 ±).</summary>
        public double jx { get; private set; }
        public double jz { get; private set; }
        /// <summary>null = 빈칸(함수 기본값, 06 §6.4).</summary>
        public EffectAnchor? anchor { get; private set; }
        public double chance { get; private set; }
        public string shake { get; private set; }
        public string hitStop { get; private set; }

        public EffectData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            type = S(r, "type");
            prefab = S(r, "prefab");
            poolSize = I(r, "poolSize");
            blend = S(r, "blend");
            sortBias = D(r, "sortBias");
            color = S(r, "color");
            count = D(r, "count");
            speed = D(r, "speed");
            size = D(r, "size");
            life = D(r, "life");
            grav = D(r, "grav");
            hasGrav = S(r, "grav").Trim().Length > 0;
            radius = D(r, "radius");
            flag = S(r, "flag");
            children = Strs(r, "children");
            y = D(r, "y");
            when = Strs(r, "when");
            dz = D(r, "dz");
            jx = D(r, "jx");
            jz = D(r, "jz");
            anchor = ConsumeData.TryParseEnum(r.Get("anchor"), out EffectAnchor a) ? a : (EffectAnchor?)null;
            chance = D(r, "chance", 1);
            shake = S(r, "shake");
            hitStop = S(r, "hitStop");
        }
    }

    // ---------------- 05_contentOpen ----------------

    public sealed class ContentOpenData : MvRow
    {
        public const string Table = "05_contentOpen";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Fk("contentId", ContentData.Table).Int("sort").Bool("active")
            .Res("icon", MvAtlas.UiIcon).Int("conditionType", true).Int("conditionValue").Bool("sticky").Bool("showOnMap");

        public int contentId { get; private set; }
        public string icon { get; private set; }
        /// <summary>0 항상 / 1 Area 도달(areaNo ≥ 값) / 2 본편 런 정산 수 ≥ 값.</summary>
        public int conditionType { get; private set; }
        public int conditionValue { get; private set; }
        public bool sticky { get; private set; }
        public bool showOnMap { get; private set; }

        public ContentOpenData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            contentId = I(r, "contentId");
            icon = S(r, "icon");
            conditionType = I(r, "conditionType");
            conditionValue = I(r, "conditionValue");
            sticky = Bool(r, "sticky");
            showOnMap = Bool(r, "showOnMap");
        }
    }

    // ---------------- 06_balancePreset ----------------

    public sealed class BalancePresetData : MvRow
    {
        public const string Table = "06_balancePreset";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("sort").Bool("active")
            .Float("rate", true).Float("scale", true).Float("diff", true).Float("gauge", true).Bool("isDefault", true);

        public double rate { get; private set; }
        public double scale { get; private set; }
        public double diff { get; private set; }
        public double gauge { get; private set; }
        public bool isDefault { get; private set; }

        public BalancePresetData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            rate = D(r, "rate");
            scale = D(r, "scale");
            diff = D(r, "diff");
            gauge = D(r, "gauge");
            isDefault = Bool(r, "isDefault");
        }
    }

    // ---------------- 08_adPlacement ----------------

    public enum AdType { Reward, Interstitial }

    public sealed class AdPlacementData : MvRow
    {
        public const string Table = "08_adPlacement";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Int("sort").Bool("active")
            .Enum<AdType>("adType", true).Str("placement", true).Int("dailyLimit").Int("chargeMax").Int("chargeSec").Int("mul").Int("minArea").Bool("noAdsSkip")
            .Consume("adReward").Res("icon", MvAtlas.UiIcon);

        /// <summary>화면 코드가 찾는 key(07 §7.13.3).</summary>
        public static readonly string[] RequiredKeys = { "dia", "skip", "collect3", "interstitial", "modeTicket" };

        public AdType adType { get; private set; }
        public string placement { get; private set; }
        public int dailyLimit { get; private set; }
        /// <summary>충전형 최대 횟수·1회 충전 초(MV-SPEC-02 11-2, Q-29). 0 = 충전형 아님. 적용은 MV-RUN-SPEC2(mvDaily → 충전 시각).</summary>
        public int chargeMax { get; private set; }
        public int chargeSec { get; private set; }
        public int mul { get; private set; }
        public int minArea { get; private set; }
        public bool noAdsSkip { get; private set; }
        public IReadOnlyList<ConsumeData> adReward { get; private set; }
        public string icon { get; private set; }

        public AdPlacementData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            adType = E(r, "adType", AdType.Reward);
            placement = S(r, "placement");
            dailyLimit = I(r, "dailyLimit");
            chargeMax = I(r, "chargeMax");
            chargeSec = I(r, "chargeSec");
            mul = I(r, "mul");
            minArea = I(r, "minArea");
            noAdsSkip = Bool(r, "noAdsSkip");
            adReward = Consume(r, "adReward");
            icon = S(r, "icon");
        }
    }

    // ---------------- 09_uiResource ----------------

    public sealed class UiResourceData : MvRow
    {
        public const string Table = "09_uiResource";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Key().Group().Int("sort").Bool("active")
            .Str("atlas").Res("sprite", MvAtlas.AtlasName).Res("address", MvAtlas.Address);

        public int group { get; private set; }
        public string atlas { get; private set; }
        public string sprite { get; private set; }
        public string address { get; private set; }

        public UiResourceData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            group = I(r, "group");
            atlas = S(r, "atlas");
            sprite = S(r, "sprite");
            address = S(r, "address");
        }
    }
}
