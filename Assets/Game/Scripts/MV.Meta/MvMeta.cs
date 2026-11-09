using System;
using System.Collections.Generic;
using System.Linq;
using jjevol;
using MV.Data;
using MV.Save;
using Newtonsoft.Json.Linq;
using UnityEngine;
using MvConsume = MV.Data.ConsumeData;

namespace MV.Meta
{
    /// <summary>성장 표 3종.</summary>
    public enum MvGrowth { Lobby, AreaUp, Perk }

    /// <summary>
    /// 영속 상태 변경의 유일한 진입점(10 §10, 09 §9.4). 화면은 아래 명령만 부르고 <c>DataRepository.Changed</c>
    /// (또는 <see cref="Applied"/>)로 다시 그린다. 명령은 요청(<see cref="MvMetaRequest"/>) → 백엔드 판정 → 한 번의 원자 저장 순서다.
    /// 메인 스레드에서만 쓴다.
    /// </summary>
    public sealed class MvMeta
    {
        /// <summary>Boot가 만든 현재 인스턴스(저장을 다시 열면 바뀐다).</summary>
        public static MvMeta Current { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        private readonly Func<MvSaveStore> _store;
        private readonly Func<ResourceService> _resources;
        private readonly MvLocalMetaBackend _local;
        private readonly IMvMetaBackend _backend;
        private long _issuedRunSeq;
        private long _rebirthArmedAt = long.MinValue;

        public MvTables Tables { get; }
        /// <summary>현재 시각. 넣은 시계를 <see cref="MvOffsetClock"/>로 감싼다(개발 빌드 <c>debug.addTime</c> 보정).</summary>
        public IMvClock Clock { get; }
        public IMvAdService Ads { get; }

        /// <summary>응답을 저장소에 반영한 뒤(성공만).</summary>
        public event Action<MvMetaRequest, MvMetaResponse> Applied;

        /// <param name="store">저장소 공급(Boot: <c>() => MvSave.Current</c>). 다시 열어도 같은 인스턴스를 쓴다.</param>
        /// <param name="resources">재화 서비스 공급(Boot: <c>() => MvBootSave.Resources</c>).</param>
        /// <param name="ads">null이면 가짜 광고(<see cref="MvFakeAdService"/>).</param>
        /// <param name="backend">null이면 로컬 백엔드.</param>
        public MvMeta(Func<MvSaveStore> store, Func<ResourceService> resources, MvTables tables, IMvClock clock = null,
            IMvAdService ads = null, IMvMetaBackend backend = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
            Clock = clock as MvOffsetClock ?? new MvOffsetClock(clock ?? MvSystemClock.Instance);
            Ads = ads ?? new MvFakeAdService();
            _local = new MvLocalMetaBackend(store, resources, tables, Clock);
            _backend = backend ?? _local;
        }

        public MvSaveStore Store => _store();
        private ResourceService Res => _resources();
        public bool IsReady => Store != null && !Store.IsDisposed && !Store.RequiresReopen && Res != null && !Res.IsDisposed;

        // ═════════ 요청 보내기 ═════════

        /// <summary>요청 1건을 보내고 성공하면 응답 값을 한 번에 저장한다. 저장 실패면 <see cref="MvMetaErrors.SaveFailed"/>.</summary>
        public MvMetaResponse Send(MvMetaRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!IsReady) return MvMetaResponse.Reject(MvMetaErrors.NoStore);
            MvMetaResponse response = _backend.Handle(request);
            if (!response.Ok) return response;
            string[] keys = response.Data.Properties().Select(p => p.Name).ToArray();
            if (response.ResetProgress || keys.Length > 0)
            {
                try
                {
                    if (response.ResetProgress)
                    {
                        Store.DevResetProgress(); // D-69: mvSettings만 남기고 기본값(개발 빌드 전용 저장 경로)
                        _rebirthArmedAt = long.MinValue;
                    }
                    if (keys.Length > 0) Store.ApplyAndSave(response.Data, keys);
                }
                catch (Exception exception)
                {
                    // 쓰기 실패·충돌: 같은 변경을 다시 시도하지 않는다. 화면은 toastMessage.saveFailed 뒤 MvSave.Reopen.
                    Debug.LogException(exception);
                    return MvMetaResponse.Reject(MvMetaErrors.SaveFailed, exception.Message);
                }
            }
            Applied?.Invoke(request, response);
            return response;
        }

        /// <summary>테스트 패널 요청(05 §5.14). 처리기는 개발 빌드 로컬 백엔드에만 있다(출시 빌드는 <see cref="MvMetaErrors.UnknownRequest"/>).</summary>
        public MvMetaResponse SendDebug(string name, string key = null, long amount = 0) => Send(MvMetaRequest.ForDebug(name, key, amount));

        // ═════════ 정산(04 §4.11) ═════════

        /// <summary>런 시작 때 받는 순번. 정산에 그대로 넘긴다(같은 런 두 번 정산 방지).</summary>
        public long BeginRun()
        {
            long settled = IsReady ? Store.Progress.LastSettledRunSeq : 0;
            _issuedRunSeq = Math.Max(_issuedRunSeq, settled) + 1;
            return _issuedRunSeq;
        }

        /// <summary>결과 ×3 버튼 활성(<c>R.gold ≥ 1 || R.dia > 0</c>).</summary>
        public static bool CanCollectX3(MvRunOutcome outcome) => outcome != null && (outcome.Gold >= 1 || outcome.Dia > 0);

        /// <summary>정산 ×1 또는 ×<c>collectAdMul</c>(광고 본 뒤). 영속 반영은 이 한 번이다.</summary>
        public MvSettleResult Settle(MvRunOutcome outcome, int mul = 1)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            var p = new JObject
            {
                ["runSeq"] = outcome.RunSeq, ["end"] = outcome.End.ToString(), ["mul"] = mul,
                ["gold"] = outcome.Gold, ["souls"] = outcome.Souls, ["dia"] = outcome.Dia, ["cpRun"] = outcome.CpRun,
            };
            if (outcome.Bars != null) p["bars"] = BarsJson(outcome.Bars);
            if (outcome.PendingSettle != null && outcome.PendingSettle.Count > 0)
                p["pending"] = new JArray(outcome.PendingSettle.Select(c => (object)new JObject { ["t"] = c.TypeId, ["i"] = c.Id, ["v"] = c.Value }).ToArray());

            MvMetaResponse response = Send(new MvMetaRequest(MvMetaRequestNames.Settle, p));
            var result = new MvSettleResult { Response = response, Mul = mul };
            if (!response.Ok) return result;
            JObject r = response.Result;
            result.AreaBefore = r.Value<int>("areaBefore");
            result.AreaAfter = r.Value<int>("areaAfter");
            result.Pass = r.Value<bool>("pass");
            result.ZoneEnd = r.Value<bool>("zoneEnd");
            result.ZoneChanged = r.Value<bool>("zoneChanged");
            result.GoldGained = r.Value<long>("gold");
            result.DiaGained = r.Value<long>("dia");
            result.SoulsGained = r.Value<int>("souls");
            result.Tokens = r.Value<int>("tok");
            result.ShowInterstitial = r.Value<bool>("interstitial");
            result.NewlyOpened = r["newlyOpened"].Values<string>().ToArray();
            result.Unsettled = ((JArray)r["unsettled"]).Select(t => new MvConsume((ConsumeType)t.Value<int>("t"), t.Value<int>("i"), t.Value<double>("v"))).ToArray();
            return result;
        }

        /// <summary>×3: 보상 광고(<c>collect3</c>)를 보고 끝까지 봤으면 ×mul 정산. 안 봤으면 정산하지 않고 <see cref="MvMetaErrors.AdNotWatched"/>.</summary>
        public void SettleWithAd(MvRunOutcome outcome, Action<MvSettleResult> done)
        {
            var row = Tables.AdPlacement.ByKey("collect3");
            int mul = row != null && row.mul > 0 ? row.mul : Tables.Config.Value.collectAdMul;
            WatchAd("collect3", watched =>
            {
                if (!watched) { done?.Invoke(new MvSettleResult { Response = MvMetaResponse.Reject(MvMetaErrors.AdNotWatched), Mul = mul }); return; }
                done?.Invoke(Settle(outcome, mul));
            });
        }

        /// <summary>정산 뒤 전면 광고(<see cref="MvSettleResult.ShowInterstitial"/>일 때 화면이 부른다).</summary>
        public void ShowInterstitial(Action done)
        {
            var row = Tables.AdPlacement.ByKey("interstitial");
            Ads.ShowInterstitial(row?.placement ?? "result_interstitial", () => done?.Invoke());
        }

        private static JArray BarsJson(IReadOnlyList<IReadOnlyDictionary<int, double>> floors)
        {
            var result = new JArray();
            foreach (var floor in floors)
            {
                var cells = new JArray();
                if (floor != null && floor.Count > 0)
                {
                    int top = floor.Keys.Max();
                    for (int idx = 0; idx <= top; idx++)
                    {
                        if (!floor.TryGetValue(idx, out double hp) || idx < 0) { cells.Add(JValue.CreateNull()); continue; }
                        hp = Math.Max(0, hp);
                        cells.Add(Math.Floor(hp) == hp ? new JValue((long)hp) : new JValue(hp));
                    }
                }
                result.Add(cells);
            }
            return result;
        }

        // ═════════ 성장 구매(07 §7.8.7) ═════════

        private ILevelPriced Row(MvGrowth growth, string key)
        {
            switch (growth)
            {
                case MvGrowth.Lobby: return Tables.LobbyUpgrade.ByKey(key);
                case MvGrowth.AreaUp: return Tables.AreaUpgrade.ByKey(key);
                default: return Tables.RebirthPerk.ByKey(key);
            }
        }

        /// <summary>현재 구매 수(단계) P.</summary>
        public int Level(MvGrowth growth, string key)
        {
            if (!IsReady) return 0;
            switch (growth)
            {
                case MvGrowth.Lobby: return Store.Lobby.GetUp(key);
                case MvGrowth.AreaUp: return Store.AreaUp.GetLevel(key);
                default: return Store.Rebirth.GetPerk(key);
            }
        }

        public bool IsMax(MvGrowth growth, string key)
        {
            var row = Row(growth, key);
            return row == null || Level(growth, key) >= row.max || NextPrice(growth, key) == null;
        }

        /// <summary>다음 구매 비용(<c>16_levelPrice[group][P+1]</c>). MAX면 null.</summary>
        public IReadOnlyList<MvConsume> NextPrice(MvGrowth growth, string key)
        {
            var row = Row(growth, key);
            if (row == null || Level(growth, key) >= row.max) return null;
            return MvMetaRules.NextPrice(Tables, row.levelPriceGroup, Level(growth, key));
        }

        /// <summary>버튼 활성: MAX 아님 && 비용 재화 충분(정본은 실패 문구 없이 비활성).</summary>
        public bool CanBuy(MvGrowth growth, string key)
        {
            var price = NextPrice(growth, key);
            return price != null && IsReady && CanAfford(price);
        }

        public bool CanAfford(IReadOnlyList<MvConsume> price)
        {
            if (!IsReady || price == null) return false;
            foreach (var group in price.GroupBy(c => (c.Type, c.Id)))
            {
                if (group.Key.Type != ConsumeType.Wealth || MvWealthIds.TypeOf(group.Key.Id) == null) return false;
                if (Res.Get(group.Key.Id) < (long)Math.Floor(group.Sum(c => c.Value))) return false;
            }
            return true;
        }

        public MvMetaResponse Buy(MvGrowth growth, string key)
        {
            string name = growth == MvGrowth.Lobby ? MvMetaRequestNames.BuyLobbyUp
                : growth == MvGrowth.AreaUp ? MvMetaRequestNames.BuyAreaUp : MvMetaRequestNames.BuyPerk;
            return Send(new MvMetaRequest(name, new JObject { ["key"] = key }));
        }

        /// <summary>로비 강화 구매(04 §4.12.1). weapon은 현재 무기 Level 카드 1장을 덱에 넣고 3장 머지.</summary>
        public MvLobbyBuyResult BuyLobbyUp(string key)
        {
            MvMetaResponse response = Buy(MvGrowth.Lobby, key);
            var result = new MvLobbyBuyResult { Response = response, Key = key };
            if (!response.Ok) return result;
            result.LvBefore = response.Result.Value<int>("before");
            result.LvAfter = response.Result.Value<int>("after");
            result.AddedTier = response.Result.Value<int>("tier");
            result.MergedTier = response.Result.Value<int>("merged");
            return result;
        }

        public MvMetaResponse BuyAreaUp(string key) => Buy(MvGrowth.AreaUp, key);
        public MvMetaResponse BuyPerk(string key) => Buy(MvGrowth.Perk, key);

        /// <summary>캣타워 → 런 시작 게이지 Lv·진행(kegStart).</summary>
        public MvStepLevel StairStart() => MvMetaRules.StairStart(Tables, Level(MvGrowth.Lobby, MvMetaRules.LobbyStair));
        /// <summary>스크래쳐 → 무기 Level(= 런 공방 등급 R.bt).</summary>
        public MvStepLevel WeaponLevel() => MvMetaRules.WeaponLevel(Tables, Level(MvGrowth.Lobby, MvMetaRules.LobbyWeapon));
        /// <summary>생선 건조대 goldMul.</summary>
        public double GoldMul() => MvMetaRules.GoldMul(Tables, Level(MvGrowth.Lobby, MvMetaRules.LobbyIncome));
        /// <summary>부활 특성 값 = max(floor, base + perLv × lv)(14_rebirthPerk).</summary>
        public double PerkValue(string key) => Tables.RebirthPerk.ByKey(key)?.ValueAt(Level(MvGrowth.Perk, key)) ?? 0;
        /// <summary>Area 강화 효과 = perLv × L(clone은 min(max, L)과 같다).</summary>
        public double AreaUpValue(string key) => (Tables.AreaUpgrade.ByKey(key)?.perLv ?? 0) * Level(MvGrowth.AreaUp, key);

        // ═════════ 부활(04 §4.12.3) ═════════

        public bool RebirthOpen => IsReady && _local.RebirthOpen();
        public long RebirthCooldownLeftMs => IsReady ? _local.RebirthCooldownLeftMs() : 0;
        public bool CanRebirth => RebirthOpen && RebirthCooldownLeftMs == 0;

        /// <summary>부활 실행(확인 절차 없이). 화면 버튼은 <see cref="PressRebirth"/>.</summary>
        public MvMetaResponse Rebirth() => Send(new MvMetaRequest(MvMetaRequestNames.Rebirth));

        /// <summary>
        /// 부활 버튼: <c>00_config.rebirthArmSec</c> 안에 두 번 눌러야 부활한다(정본). 첫 번째 누름이면 null(화면은 「한 번 더」 표시).
        /// </summary>
        public MvMetaResponse PressRebirth()
        {
            if (!CanRebirth) return MvMetaResponse.Reject(RebirthOpen ? MvMetaErrors.Cooldown : MvMetaErrors.Locked);
            long now = Clock.NowMs;
            long arm = (long)(Tables.Config.Value.rebirthArmSec * 1000);
            if (_rebirthArmedAt == long.MinValue || now - _rebirthArmedAt > arm)
            {
                _rebirthArmedAt = now;
                return null;
            }
            _rebirthArmedAt = long.MinValue;
            return Rebirth();
        }

        /// <summary>부활 화면을 열면 확인 대기를 끈다(05 §5.9 「열기」, 정본 L2572).</summary>
        public void DisarmRebirth() => _rebirthArmedAt = long.MinValue;

        public bool RebirthArmed => _rebirthArmedAt != long.MinValue && Clock.NowMs - _rebirthArmedAt <= (long)(Tables.Config.Value.rebirthArmSec * 1000);

        // ═════════ 신 선택(04 §4.12.5) ═════════

        public bool GodUnlocked(string key) => IsReady && _local.GodUnlocked(Tables.God.ByKey(key));
        public MvMetaResponse SelectGod(string key) => Send(new MvMetaRequest(MvMetaRequestNames.SelectGod, new JObject { ["key"] = key }));

        // ═════════ 광고·일일(04 §4.12.4, 09 §9.3.4) ═════════

        /// <summary>오늘 하루 키(<c>00_config.adDayUtcOffsetHour</c> 기준).</summary>
        public string Today => _local.Today();

        /// <summary>오늘 남은 횟수. 한도 없음이면 −1. 날이 바뀌었으면 한도 전체(읽기만, 저장은 다음 사용 때).</summary>
        public int AdsLeft(string key) => IsReady ? _local.AdsLeft(key) : 0;

        /// <summary>모드 오늘 입장 수(<c>mvDaily.modePlays</c>). 한도는 <c>400_content.dailyLimit</c>(0 = 없음).</summary>
        public int ModePlaysToday(string contentKey) => IsReady ? _local.ModePlaysToday(contentKey) : 0;

        public MvMetaResponse RecordModePlay(string contentKey) =>
            Send(new MvMetaRequest(MvMetaRequestNames.RecordModePlay, new JObject { ["key"] = contentKey }));

        /// <summary>광고 제거(재화 noAds ≥ 1)이고 자리의 noAdsSkip이 1이면 광고 없이 처리.</summary>
        public bool SkipsAd(string key)
        {
            var row = Tables.AdPlacement.ByKey(key);
            return row != null && row.noAdsSkip && IsReady && Res.GetRaw(MvWealthIds.NoAds) > 0;
        }

        /// <summary>광고를 보여 주고 끝까지 봤는지 알린다(한도 검사 포함, 보상은 없음). 한도 있는 자리는 사용 수를 올린다.</summary>
        public void WatchAd(string key, Action<bool> done)
        {
            var row = Tables.AdPlacement.ByKey(key);
            if (row == null || !row.active || AdsLeft(key) == 0) { done?.Invoke(false); return; }
            if (SkipsAd(key)) { done?.Invoke(Count(key)); return; }
            Ads.ShowReward(row.placement, watched => done?.Invoke(watched && Count(key)));
        }

        private bool Count(string key) => Send(new MvMetaRequest(MvMetaRequestNames.UseAd, new JObject { ["key"] = key })).Ok;

        /// <summary>
        /// 보상 광고(dia 캣닢 +100, skip 부활 쿨 −70분 …): 한도·조건 확인 → 광고 → 끝까지 봤으면 보상과 사용 수를 한 번에 저장.
        /// </summary>
        public void WatchAdForReward(string key, Action<MvMetaResponse> done)
        {
            var row = Tables.AdPlacement.ByKey(key);
            if (row == null || !row.active) { done?.Invoke(MvMetaResponse.Reject(MvMetaErrors.UnknownKey, key)); return; }
            if (AdsLeft(key) == 0) { done?.Invoke(MvMetaResponse.Reject(MvMetaErrors.DailyLimit, key)); return; }
            if (row.adReward.Any(r => r.Type == ConsumeType.RebirthCooldownSec) && RebirthCooldownLeftMs == 0)
            { done?.Invoke(MvMetaResponse.Reject(MvMetaErrors.NoCooldown, key)); return; }
            var request = new MvMetaRequest(MvMetaRequestNames.GrantAd, new JObject { ["key"] = key });
            if (SkipsAd(key)) { done?.Invoke(Send(request)); return; }
            Ads.ShowReward(row.placement, watched =>
                done?.Invoke(watched ? Send(request) : MvMetaResponse.Reject(MvMetaErrors.AdNotWatched, key)));
        }

        // ═════════ 해금(07 §7.8.9) ═════════

        /// <summary>기능 열림(sticky 기록 또는 조건 성립). 모르는 key는 false.</summary>
        public bool IsOpen(string key)
        {
            if (!IsReady) return false;
            return _local.IsOpen(Tables.ContentOpen.ByKey(key), Store.Progress.Area, Store.Progress.Runs, Store.Unlock.Opened);
        }

        /// <summary>해금 연출 대상: 열림 && 연출 안 봄.</summary>
        public IReadOnlyList<string> PendingUnlockShows()
            => IsReady ? Tables.ContentOpen.Sorted.Where(r => IsOpen(r.key) && !Store.Unlock.SeenUnlock.Contains(r.key)).Select(r => r.key).ToArray() : new string[0];

        /// <summary>NEW 표시 = 열림 && NEW 안 봄.</summary>
        public bool IsNew(string key) => IsOpen(key) && !Store.Unlock.SeenNew.Contains(key);

        /// <summary>해금 연출 봄 / 컨텐츠 메뉴 닫음(그때 보인 열린 key, 마지막 탭).</summary>
        public MvMetaResponse MarkSeen(IEnumerable<string> unlockShown = null, IEnumerable<string> newSeen = null, string contentTab = null)
        {
            var p = new JObject();
            if (unlockShown != null) p["unlock"] = new JArray(unlockShown.Cast<object>().ToArray());
            if (newSeen != null) p["new"] = new JArray(newSeen.Cast<object>().ToArray());
            if (contentTab != null) p["contentTab"] = contentTab;
            return Send(new MvMetaRequest(MvMetaRequestNames.MarkSeen, p));
        }
    }
}
