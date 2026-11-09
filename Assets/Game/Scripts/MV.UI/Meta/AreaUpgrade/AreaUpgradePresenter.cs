using MV.Data;
using MV.Meta;
using MV.Save;

namespace MV.UI
{
    /// <summary>
    /// Area 강화 화면 코드(05 §5.8). 행 = 보유 캣닢(광고 무료 버튼) + <c>13_areaUpgrade</c>(sort) 행마다 1칸.
    /// 구매는 <see cref="IMetaUiPort.Buy"/>(AreaUp) 1번, 무료는 광고 보상(<c>08_adPlacement dia</c>).
    /// </summary>
    public sealed class AreaUpgradePresenter : MetaPanelPresenter
    {
        public const string AdKey = "dia";
        public const string FreeId = "free";
        public const string BuyPrefix = "buy:";

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

            foreach (var row in tables.AreaUpgrade.Sorted)
            {
                if (!row.active) continue;
                int lv = Port.Level(MvGrowth.AreaUp, row.key);
                bool max = Port.IsMax(MvGrowth.AreaUp, row.key);
                string val = "Lv " + lv + " · " + GrowthValue(row.valueFormat, row.valueBase, row.valueStep, lv)
                    + (max ? " (MAX)" : " → " + GrowthValue(row.valueFormat, row.valueBase, row.valueStep, lv + 1)); // ui.aup.val / ui.aup.max
                m.Add(new MetaRow(row.key, MetaRowKind.Row, NameOf(row.id, row.key)) { Icon = row.icon, Value = val })
                    .Act(BuyPrefix + row.key, max ? "MAX" : PriceText(Port.NextPrice(MvGrowth.AreaUp, row.key), tables),
                        !max && Port.CanBuy(MvGrowth.AreaUp, row.key));
            }
        }

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
