using MV.Data;
using MV.Meta;
using MV.Save;

namespace MV.UI
{
    /// <summary>
    /// Area 강화 화면 코드(05 §5.8). 행 = 보유 캣닢(광고 무료 버튼) + <c>13_areaUpgrade</c>(sort) 행마다 1칸.
    /// 구매는 <see cref="IMetaUiPort.Buy"/>(AreaUp) 1번, 무료는 광고 보상(<c>08_adPlacement dia</c>).
    /// 디자이너 겉(<see cref="AreaUpgradeDesignView"/>, MV-UI2-AREAUP)은 <see cref="MetaPanelModel.Data"/>·<see cref="MetaRow.Data"/>를 읽는다.
    /// </summary>
    public sealed class AreaUpgradePresenter : MetaPanelPresenter
    {
        public const string AdKey = "dia";
        public const string FreeId = "free";
        public const string BuyPrefix = "buy:";
        /// <summary>시안 재화 칸(노션: 골드와 다이아). 값은 <c>01_wealth.type</c>.</summary>
        public static readonly string[] CurrencyTypes = { "gold", "dia" };
        public const string MaxKey = "ui.common.max";

        protected override void Build(MetaPanelModel m, MvTables tables)
        {
            m.Title = "장난감 바구니 · Area " + (Port.AreaIndex + 1); // ui.aup.title
            m.Sub = "이 Area에서만 유지되는 강화 · 캣닢 사용 · Area가 바뀌면 초기화"; // ui.aup.sub
            long dia = Port.Wealth(MvWealthIds.Dia);
            m.Currencies = "캣닢 " + MvUiFormat.Fmt(dia);

            var ad = tables.AdPlacement.ByKey(AdKey);
            int left = Port.AdsLeft(AdKey);
            double gain = ad != null && ad.adReward.Count > 0 ? ad.adReward[0].Value : 0;
            m.Add(new MetaRow("have", MetaRowKind.Row, "보유 " + MvUiFormat.Fmt(dia)) // ui.aup.have
            {
                Value = "광고 보고 캣닢 ×" + MvUiFormat.Fmt(gain) + " · 오늘 " + (left < 0 ? "∞" : left.ToString()) + "회 남음", // ui.aup.adInfo
            }).Act(FreeId, "▶ 무료", ad != null && ad.active && left != 0); // ui.aup.free

            m.Data = BuildViewData(tables, gain);

            string maxText = MvUiText.Get("ui", MaxKey, "MAX");
            foreach (var row in tables.AreaUpgrade.Sorted)
            {
                if (!row.active) continue;
                int lv = Port.Level(MvGrowth.AreaUp, row.key);
                bool max = Port.IsMax(MvGrowth.AreaUp, row.key);
                bool locked = Port.IsRebirthLocked(MvGrowth.AreaUp, row.key);
                bool canBuy = !max && !locked && Port.CanBuy(MvGrowth.AreaUp, row.key);
                string cur = GrowthValue(row.valueFormat, row.valueBase, row.valueStep, lv);
                string next = max ? null : GrowthValue(row.valueFormat, row.valueBase, row.valueStep, lv + 1);
                string name = NameOf(row.id, row.key);
                string actionId = BuyPrefix + row.key;
                var price = max ? null : Port.NextPrice(MvGrowth.AreaUp, row.key);
                string val = "Lv " + lv + " · " + cur + (max ? " (MAX)" : " → " + next); // ui.aup.val / ui.aup.max
                var state = locked ? UpgradeElementData.LockState(lv)
                    : max ? UpgradeElementState.Max
                    : canBuy ? UpgradeElementState.Ready : UpgradeElementState.Disabled;
                m.Add(new MetaRow(row.key, MetaRowKind.Row, name)
                {
                    Icon = row.icon,
                    Value = val,
                    Locked = locked,
                    Data = new UpgradeElementData(actionId, row.icon, name, lv, cur, next,
                        max ? maxText : PriceNumber(price), state, Port.NeedRebirth(MvGrowth.AreaUp, row.key), price, Port.Wallet),
                }).Act(actionId, max ? "MAX" : PriceText(price, tables), canBuy);
            }
        }

        AreaUpgradeViewData BuildViewData(MvTables tables, double adGain)
        {
            var d = new AreaUpgradeViewData { Tables = tables, AdActionId = FreeId };
            int areaNo = Port.AreaIndex + 1;
            d.AreaName = "Area " + areaNo;
            foreach (var a in tables.Area.Datas)
                if (a.areaNo == areaNo) { d.AreaName = MvUiText.Name(a.id, d.AreaName); break; }
            d.AreaIcon = HudAreaLabel.AreaIconOf(tables, areaNo);
            d.ZoneNo = MvMetaRules.ZoneIndex(tables, Port.AreaIndex) + 1;
            foreach (var type in CurrencyTypes)
            {
                int id = tables.Wealth.GetId(type);
                d.Currencies.Add(new AreaUpgradeCurrency(type, MvUiFormat.Fmt(id == 0 ? 0 : Port.Wealth(id))));
            }
            d.AdGainText = string.Format(MvUiText.Get("ui", AreaUpgradeDesignView.AdGainKey, AreaUpgradeDesignView.AdGainFallback), MvUiFormat.Fmt(adGain));
            return d;
        }

        /// <summary>디자이너 가격 칸은 아이콘이 프리팹에 있어 숫자만 넣는다.</summary>
        static string PriceNumber(System.Collections.Generic.IReadOnlyList<ConsumeData> price)
            => price == null || price.Count == 0 ? "" : MvUiFormat.Fmt(price[0].Value);

        protected override bool OnAction(string actionId)
        {
            if (actionId == FreeId)
            {
                Port.WatchAdForReward(AdKey, r =>
                {
                    if (!r.Ok) Say(r.Code);
                    Draw();
                });
                return true;
            }
            if (!actionId.StartsWith(BuyPrefix)) return false;
            var res = Port.Buy(MvGrowth.AreaUp, actionId.Substring(BuyPrefix.Length));
            if (!res.Ok) { Say(res.Code); return false; }
            Play(MetaSound.AreaBuy); // 소리 buy(L2530). 시설 반짝임 basket은 로비(MV-UI-LOBBY) 몫.
            return true;
        }
    }
}
