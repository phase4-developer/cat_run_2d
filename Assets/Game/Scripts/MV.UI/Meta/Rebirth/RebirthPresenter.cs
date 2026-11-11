using System;
using MV.Data;
using MV.Meta;
using MV.Save;

namespace MV.UI
{
    /// <summary>
    /// 부활 화면 코드(05 §5.9). 행 = 부활 레벨·쿨타임(광고 스킵) + 안내 + <c>14_rebirthPerk</c>(sort) 행마다 1칸 + 경고.
    /// 부활은 <see cref="IMetaUiPort.PressRebirth"/>(2번 눌러야 부활, 확인 대기 창은 메타 규칙 <c>rebirthArmSec</c>).
    /// </summary>
    public sealed class RebirthPresenter : MetaPanelPresenter
    {
        public const string SkipAdKey = "skip";
        public const string SkipId = "skip";
        public const string SkipDiaId = "skipDia";
        public const string GoId = "go";
        public const string BuyPrefix = "perk:";

        float _tick;

        protected override void OnOpen() { _tick = 0; MvMeta.Current?.DisarmRebirth(); } // 05 §5.9 열기: 확인 대기 끔(IMetaUiPort에 자리가 생기면 그쪽으로)

        protected override void Build(MetaPanelModel m, MvTables tables)
        {
            m.Title = "아홉 목숨 · 부활"; // ui.rb.title
            long tokens = Port.Wealth(MvWealthIds.RbToken);
            m.Currencies = "조각 " + MvUiFormat.Fmt(tokens);
            m.Sub = "보유 조각 " + MvUiFormat.Fmt(tokens) + " · 영구 능력치는 부활해도 유지"; // ui.rb.subA + subB

            long cd = Port.RebirthCooldownLeftMs;
            var skip = tables.AdPlacement.ByKey(SkipAdKey);
            int left = Port.AdsLeft(SkipAdKey);
            m.Add(new MetaRow("state", MetaRowKind.Row, "부활 레벨 " + Port.RebirthLevel) // ui.rb.level
            {
                Value = "부활 쿨타임 " + (cd > 0 ? Hms(cd) : "준비됨"), // ui.rb.cd / ui.rb.ready
                Desc = "광고 스킵 오늘 " + (left < 0 ? "∞" : left.ToString()) + "회 · 유료 다이아는 향후", // ui.rb.adInfo
            })
            .Act(SkipId, "▶ " + SkipMinutes(skip) + "분 스킵", skip != null && skip.active && cd > 0 && left != 0) // ui.rb.skip
            .Act(SkipDiaId, "다이아", false); // ui.rb.skipDia: 늘 꺼짐(유료 재화 스텁)

            foreach (var row in tables.RebirthPerk.Sorted)
            {
                if (!row.active) continue;
                int lv = Port.Level(MvGrowth.Perk, row.key);
                bool max = Port.IsMax(MvGrowth.Perk, row.key);
                string val = "Lv " + lv + " · " + GrowthValue(row.valueFormat, row.valueBase, row.valueStep, lv)
                    + (max ? " (MAX)" : " → " + GrowthValue(row.valueFormat, row.valueBase, row.valueStep, lv + 1));
                m.Add(new MetaRow(row.key, MetaRowKind.Row, NameOf(row.id, row.key)) { Icon = row.icon, Value = val })
                    .Act(BuyPrefix + row.key, max ? "MAX" : PriceText(Port.NextPrice(MvGrowth.Perk, row.key), tables),
                        !max && Port.CanBuy(MvGrowth.Perk, row.key));
            }

            m.Add(new MetaRow("warn", MetaRowKind.Text,
                "부활하면 Area 진척도·츄르·생선·로비 강화·Area 강화가 초기화되고, 쌓아 둔 목숨 조각을 받습니다")); // ui.rb.warn

            int pending = Port.RebirthPending;
            if (!Port.RebirthOpen)
                m.Confirm = new MetaAction(GoId, LockedText(tables), false); // ui.rb.goLocked
            else if (cd > 0)
                m.Confirm = new MetaAction(GoId, "쿨타임 " + Hms(cd), false); // ui.rb.goCd
            else if (Port.RebirthArmed)
                m.Confirm = new MetaAction(GoId, "한 번 더 누르면 부활 · 조각 +" + pending, true); // ui.rb.goArm
            else
                m.Confirm = new MetaAction(GoId, "부활 · 조각 +" + pending, true); // ui.rb.goBase
        }

        /// <summary>잠김 문구: 표 <c>05_contentOpen[rebirth]</c>의 남은 조건(<see cref="MvUnlockText"/>).</summary>
        string LockedText(MvTables tables)
            => MvUnlockText.Need(tables.ContentOpen.ByKey(MvMetaRules.RebirthKey), Port.UnlockState(MvMetaRules.RebirthKey)) + " 달성 시 해금";

        static long SkipMinutes(AdPlacementData skip)
        {
            if (skip != null)
                foreach (var r in skip.adReward)
                    if (r.Type == ConsumeType.RebirthCooldownSec) return (long)Math.Round(r.Value / 60);
            return 70;
        }

        protected override bool OnAction(string actionId)
        {
            if (actionId == SkipId)
            {
                Port.WatchAdForReward(SkipAdKey, r =>
                {
                    if (!r.Ok) Say(r.Code);
                    Draw();
                });
                return true;
            }
            if (actionId == GoId)
            {
                MetaResult? r = Port.PressRebirth();
                if (r == null) return true; // 확인 대기 시작 → 다시 그림(「한 번 더」)
                if (!r.Value.Ok) { Say(r.Value.Code); return false; }
                Play(MetaSound.Rebirth);
                Say("부활 Lv " + Port.RebirthLevel + " · 목숨 조각 +" + r.Value.Value + " · 처음부터 다시"); // ui.banner.rebirth / rebirthSub(배너는 MV-UI-HUD)
                RequestClose();
                return true;
            }
            if (!actionId.StartsWith(BuyPrefix)) return false;
            var res = Port.Buy(MvGrowth.Perk, actionId.Substring(BuyPrefix.Length));
            if (!res.Ok) { Say(res.Code); return false; }
            Play(MetaSound.PerkBuy); // 소리 lvup(L2554). 시설 반짝임 stove는 로비 몫.
            return true;
        }

        /// <summary>열려 있는 동안 1초마다 다시 그림(쿨타임·확인 대기 만료, L2565).</summary>
        public override void Tick(float unscaledDt)
        {
            _tick += unscaledDt;
            if (_tick < 1f) return;
            _tick = 0;
            Draw();
        }
    }
}
