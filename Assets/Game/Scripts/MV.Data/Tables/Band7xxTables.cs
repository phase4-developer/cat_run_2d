using System.Collections.Generic;

namespace MV.Data
{
    // 7xx 상점 표(기획서 08 §8.5.7, 07 §7.9.4). 결제 연결은 MV-PLAT-IAP 몫이다.
    // Framework 대체: platform.purchase 고정본이 없어 표 클래스만 둔다. storeProductId(google/apple)는 PurchaseManager 스텁이 읽는다.

    /// <summary>`701_product.chargeType`: 0 무제한 / 1 평생 n회 / 2 일일 n회.</summary>
    public enum ProductChargeType { Unlimited = 0, Lifetime = 1, Daily = 2 }

    // ---------------- 700_productTab ----------------

    public sealed class ProductTabData : MvRow
    {
        public const string Table = "700_productTab";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Int("sort").Bool("active").Res("icon", MvAtlas.UiIcon);

        public string icon { get; private set; }

        public ProductTabData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            icon = S(r, "icon");
        }
    }

    // ---------------- 702_shopSection ----------------

    public sealed class ShopSectionData : MvRow
    {
        public const string Table = "702_shopSection";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Bool("active").Int("sort").Int("sectionType").Bool("hideIfEmpty")
            .FkList("tabIds", ProductTabData.Table, true);

        public int sectionType { get; private set; }
        public bool hideIfEmpty { get; private set; }
        public IReadOnlyList<int> tabIds { get; private set; }

        public ShopSectionData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            sectionType = I(r, "sectionType");
            hideIfEmpty = Bool(r, "hideIfEmpty");
            tabIds = Ints(r, "tabIds");
        }
    }

    // ---------------- 701_product ----------------

    public sealed class ProductData : MvRow
    {
        public const string Table = "701_product";
        /// <summary>스토어 SKU 접두(08 §8.5.7 가칭).</summary>
        public const string SkuPrefix = "mv_";
        public static readonly MvSchema Schema = new MvSchema(Table).Id().Fk("tabId", ProductTabData.Table, true).Bool("active").Int("sort")
            .Int("popupType").Str("banner").Res("icon", MvAtlas.UiIcon).Str("displayImage").Int("badgeType").Str("badgeValue")
            .Int("chargeType").Int("chargedQuantity").Consume("reward").Consume("price")
            .Str("googleProductId").Str("appleProductId").Str("startTime").Str("endTime")
            .Int("minArea").Int("minLevel").Int("cooldownHours").Bool("perArea");

        public int tabId { get; private set; }
        public int popupType { get; private set; }
        public string banner { get; private set; }
        public string icon { get; private set; }
        public string displayImage { get; private set; }
        public int badgeType { get; private set; }
        public string badgeValue { get; private set; }
        public ProductChargeType chargeType { get; private set; }
        public int chargedQuantity { get; private set; }
        public IReadOnlyList<ConsumeData> reward { get; private set; }
        /// <summary>정확히 1항목: `100 AD` / `101 Inapp`(원화 표시) / `1 Wealth`.</summary>
        public IReadOnlyList<ConsumeData> price { get; private set; }
        public string googleProductId { get; private set; }
        public string appleProductId { get; private set; }
        public string startTime { get; private set; }
        public string endTime { get; private set; }
        public int minArea { get; private set; }
        public int minLevel { get; private set; }
        public int cooldownHours { get; private set; }
        /// <summary>Area마다 1회.</summary>
        public bool perArea { get; private set; }

        public ProductData(TSVRow r) : base(r) { }

        public override void Init(TSVRow r)
        {
            base.Init(r);
            tabId = I(r, "tabId");
            popupType = I(r, "popupType");
            banner = S(r, "banner");
            icon = S(r, "icon");
            displayImage = S(r, "displayImage");
            badgeType = I(r, "badgeType");
            badgeValue = S(r, "badgeValue");
            chargeType = (ProductChargeType)I(r, "chargeType");
            chargedQuantity = I(r, "chargedQuantity");
            reward = Consume(r, "reward");
            price = Consume(r, "price");
            googleProductId = S(r, "googleProductId");
            appleProductId = S(r, "appleProductId");
            startTime = S(r, "startTime");
            endTime = S(r, "endTime");
            minArea = I(r, "minArea");
            minLevel = I(r, "minLevel");
            cooldownHours = I(r, "cooldownHours");
            perArea = Bool(r, "perArea");
        }

        public ConsumeType PriceType => price.Count == 1 ? (ConsumeType)price[0].TypeId : ConsumeType.None;
    }
}
