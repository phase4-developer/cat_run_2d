using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MV.Data;
using MV.Meta;
using MV.Save;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 환생 화면 코드(05 §5.9, 노션 [팝업] 환생 업그레이드, MV-UI2-REBIRTH). 디자이너 겉(<see cref="RebirthDesignView"/>)은
    /// <see cref="MetaPanelModel.Data"/>(<see cref="RebirthViewData"/>)와 각 특전 행의 <see cref="MetaRow.Data"/>(<see cref="UpgradeElementData"/>)를 읽는다.
    /// 상태 3가지(노션): 미해금 = Lock·해금 조건 진행, 쿨타임 = 타이머·광고/캣닢 스킵·환생 버튼 Disable, 가능 = 바 가득·완료 글자·환생 버튼.
    /// 환생은 공용 확인 팝업(<see cref="MvUiNav.ConfirmAsync"/>)에서 OK를 받은 뒤 <see cref="IMetaUiPort.RebirthNow"/> 1번(사용자 결정 「공용 확인 팝업 띄우기」).
    /// </summary>
    public sealed class RebirthPresenter : MetaPanelPresenter
    {
        public const string SkipAdKey = "skip";
        public const string SkipId = "skip";
        public const string SkipDiaId = "skipDia";
        public const string GoId = "go";
        public const string BuyPrefix = "perk:";
        public const string TitleKey = "ui.rb.title";
        public const string CooldownKey = "ui.rb.cooldown";
        public const string ReadyKey = "ui.rb.ready";
        public const string SkipAdLabelKey = "ui.rb.skipAd";
        public const string SkipDiaLabelKey = "ui.rb.skipDia";
        public const string UnlockAfterKey = "ui.rb.unlockAfter";
        public const string GoLabelKey = "ui.rb.go";
        public const string ConfirmKey = "ui.rb.confirm";
        public const string DoneKey = "ui.rb.done";

        /// <summary>
        /// 확인 받기(글, 결과). 기본은 공용 시스템 팝업. 시험은 바로 답하는 가짜를 넣는다.
        /// </summary>
        public Action<string, Action<bool>> AskConfirm = (text, done) =>
            MvUiNav.ConfirmAsync(text, MvUiText.Ui(TitleKey)).ContinueWith(ok => done?.Invoke(ok)).Forget(Debug.LogException);

        float _tick;
        bool _asking;

        protected override void OnOpen() { _tick = 0; _asking = false; }

        protected override void Build(MetaPanelModel m, MvTables tables)
        {
            m.Title = MvUiText.Ui(TitleKey);
            var d = new RebirthViewData { Tables = tables, Wallet = Port.Wallet, Level = Port.RebirthLevel, Title = m.Title };
            if (View is RebirthDesignView view && view.Currencies != null)
            {
                foreach (var currency in view.Currencies)
                {
                    if (currency == null || !currency.slot || string.IsNullOrEmpty(currency.wealthType)) continue;
                    int id = tables.Wealth.GetId(currency.wealthType);
                    if (id == 0) continue;
                    d.Currencies.Add(new AreaUpgradeCurrency(currency.wealthType, MvUiFormat.Fmt(Port.Wealth(id))));
                }
            }

            long cd = Port.RebirthCooldownLeftMs;
            d.State = !Port.RebirthOpen ? RebirthHeaderState.Locked : cd > 0 ? RebirthHeaderState.Cooldown : RebirthHeaderState.Ready;
            if (d.State == RebirthHeaderState.Locked) FillUnlock(d, tables);

            // 쿨타임 줄(노션 Rebirth CoolTime): 바 = 지난 비율, 가능이면 1.
            long total = Port.RebirthCooldownTotalMs;
            d.CooldownLabel = MvUiText.Ui(CooldownKey);
            d.CooldownText = Hms(cd);
            d.CompleteText = MvUiText.Ui(ReadyKey);
            d.CooldownFill = d.State == RebirthHeaderState.Ready ? 1f
                : total > 0 ? Mathf.Clamp01(1f - (float)cd / total) : 0f;

            bool cooling = d.State == RebirthHeaderState.Cooldown;
            var skip = tables.AdPlacement.ByKey(SkipAdKey);
            int left = Port.AdsLeft(SkipAdKey);
            d.AdLabel = MvUiText.Ui(SkipAdLabelKey, SkipMinutes(skip));
            d.AdActionId = SkipId;
            // 캣닢 스킵(08 §8.5.14): 가격이 있으면 누를 수 있고, 잔액 부족은 PriceBtn이 버튼을 막는다(사용자 「priceBtn 이용해서 재화 부족하면 처리」).
            d.SkipDiaLabel = MvUiText.Ui(SkipDiaLabelKey);
            d.SkipDiaActionId = SkipDiaId;
            d.SkipDiaPrice = cooling ? Port.RebirthSkipDiaPrice : Array.Empty<ConsumeData>();
            m.Add(new MetaRow("cooldown", MetaRowKind.Row, d.CooldownLabel) { Value = d.CooldownText })
                .Act(SkipId, d.AdLabel, cooling && skip != null && skip.active && left != 0)
                .Act(SkipDiaId, d.SkipDiaLabel, cooling && d.SkipDiaPrice.Count > 0);

            string maxText = MvUiText.Get("ui", AreaUpgradePresenter.MaxKey, "MAX");
            foreach (var row in tables.RebirthPerk.Sorted)
            {
                if (!row.active) continue;
                int lv = Port.Level(MvGrowth.Perk, row.key);
                bool max = Port.IsMax(MvGrowth.Perk, row.key);
                bool locked = Port.IsRebirthLocked(MvGrowth.Perk, row.key);
                bool canBuy = !max && !locked && Port.CanBuy(MvGrowth.Perk, row.key);
                string cur = GrowthValue(row.valueFormat, row.valueBase, row.valueStep, lv);
                string next = max ? null : GrowthValue(row.valueFormat, row.valueBase, row.valueStep, lv + 1);
                string name = NameOf(row.id, row.key);
                string actionId = BuyPrefix + row.key;
                var price = max ? null : Port.NextPrice(MvGrowth.Perk, row.key);
                var state = locked ? UpgradeElementData.LockState(lv)
                    : max ? UpgradeElementState.Max
                    : canBuy ? UpgradeElementState.Ready : UpgradeElementState.Disabled;
                m.Add(new MetaRow(row.key, MetaRowKind.Row, name)
                {
                    Icon = row.icon,
                    Value = cur,
                    Locked = locked,
                    Data = new UpgradeElementData(actionId, row.icon, name, lv, cur, next,
                        max ? maxText : PriceNumber(price), state, Port.NeedRebirth(MvGrowth.Perk, row.key), price, Port.Wallet),
                }).Act(actionId, max ? maxText : PriceText(price, tables), canBuy);
            }

            d.GoLabel = MvUiText.Ui(GoLabelKey);
            d.GainText = "+" + MvUiFormat.Fmt(Port.RebirthPending); // 숫자 표기라 언어팩 키 없음
            d.GainWealth = MvWealthIds.TypeOf(MvWealthIds.RbToken); // 환생 지급 재화(MvLocalMetaBackend: wealth.Gain(RbToken))
            d.GoActionId = GoId;
            m.Confirm = new MetaAction(GoId, d.GoLabel, d.State == RebirthHeaderState.Ready && !_asking);
            m.Data = d;
        }

        /// <summary>
        /// 해금 조건 진행(사용자 결정 「해금 조건 진행」): <c>05_contentOpen[rebirth]</c>에서 아직 안 된 첫 조건의 지금 값/목표 값.
        /// 숨김 단계면 잠금 표시 조건부터 본다. 행이 없으면 경고만 남긴다.
        /// </summary>
        void FillUnlock(RebirthViewData d, MvTables tables)
        {
            string key = MvMetaRules.RebirthKey;
            var row = tables.ContentOpen.ByKey(key);
            if (row == null)
            {
                Debug.LogWarning("[RebirthPresenter] 05_contentOpen에 `" + key + "` 행이 없습니다(Assets/Game/GameData/Table/TSV/05_contentOpen.txt).");
                return;
            }
            var state = Port.UnlockState(key);
            var p = Port.UnlockProgress;
            UnlockCondition? pick = null, last = null;
            if (state == MvUnlockState.Hidden) Pick(row.showConditions, p, ref pick, ref last);
            Pick(row.openConditions, p, ref pick, ref last);
            var c = pick ?? last;
            if (c != null)
            {
                d.UnlockTarget = c.Value.value;
                d.UnlockCurrent = Math.Min(MvUnlockRules.Current(c.Value.type, p), d.UnlockTarget);
            }
            d.UnlockText = MvUiText.Ui(UnlockAfterKey, MvUnlockText.Need(row, state));
        }

        static void Pick(IReadOnlyList<UnlockCondition> conditions, MvUnlockProgress p, ref UnlockCondition? pick, ref UnlockCondition? last)
        {
            if (conditions == null) return;
            foreach (var c in conditions)
            {
                if (c.type == UnlockConditionType.Always) continue;
                last = c;
                if (pick == null && !MvUnlockRules.Met(c, p)) pick = c;
            }
        }

        static long SkipMinutes(AdPlacementData skip)
        {
            if (skip != null)
                foreach (var r in skip.adReward)
                    if (r.Type == ConsumeType.RebirthCooldownSec) return (long)Math.Round(r.Value / 60);
            return 0; // 08[skip] 보상 행이 없으면 0(값은 08 adRewardValue 한 곳)
        }

        /// <summary>디자이너 가격 칸은 아이콘이 프리팹에 있어 숫자만 넣는다.</summary>
        static string PriceNumber(IReadOnlyList<ConsumeData> price)
            => price == null || price.Count == 0 ? "" : MvUiFormat.Fmt(price[0].Value);

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
            if (actionId == SkipDiaId)
            {
                var r = Port.SkipRebirthCooldownDia();
                if (!r.Ok) { Say(r.Code); return false; }
                Play(MetaSound.Click);
                return true;
            }
            if (actionId == GoId)
            {
                if (_asking) return false;
                _asking = true;
                long pending = Port.RebirthPending;
                AskConfirm(MvUiText.Ui(ConfirmKey, MvUiFormat.Fmt(pending)), ok =>
                {
                    _asking = false;
                    if (ok) DoRebirth();
                    else Draw();
                });
                return true;
            }
            if (!actionId.StartsWith(BuyPrefix)) return false;
            var res = Port.Buy(MvGrowth.Perk, actionId.Substring(BuyPrefix.Length));
            if (!res.Ok) { Say(res.Code); return false; }
            Play(MetaSound.PerkBuy); // 소리 lvup(L2554). 시설 반짝임 stove는 로비 몫.
            return true;
        }

        /// <summary>확인 팝업 OK 뒤 실제 환생. 그 사이 조건이 바뀌었으면 메타가 거절한다.</summary>
        void DoRebirth()
        {
            MetaResult r = Port.RebirthNow();
            if (!r.Ok) { Say(r.Code); Draw(); return; }
            Play(MetaSound.Rebirth);
            Say(MvUiText.Ui(DoneKey, Port.RebirthLevel, MvUiFormat.Fmt(r.Value)));
            RequestClose();
        }

        /// <summary>열려 있는 동안 1초마다 다시 그림(쿨타임 L2565).</summary>
        public override void Tick(float unscaledDt)
        {
            _tick += unscaledDt;
            if (_tick < 1f) return;
            _tick = 0;
            Draw();
        }
    }
}
