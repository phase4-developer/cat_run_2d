using System;
using System.Collections.Generic;
using jjevol;
using MV.Platform;
using NUnit.Framework;

namespace MV.Platform.Tests.Ads
{
    public sealed class MvAdsTests
    {
        /// <summary>손으로 움직이는 대기 시계.</summary>
        private sealed class ManualTimer : IMvAdTimer
        {
            private readonly List<(TimeSpan due, Action cb)> _pending = new List<(TimeSpan, Action)>();
            public TimeSpan Now { get; private set; }

            public void After(TimeSpan delay, Action callback) => _pending.Add((Now + delay, callback));

            public void Advance(TimeSpan by)
            {
                TimeSpan end = Now + by;
                while (true)
                {
                    int next = -1;
                    for (int i = 0; i < _pending.Count; i++)
                        if (_pending[i].due <= end && (next < 0 || _pending[i].due < _pending[next].due)) next = i;
                    if (next < 0) break;
                    var p = _pending[next];
                    _pending.RemoveAt(next);
                    Now = p.due;
                    p.cb();
                }
                Now = end;
            }
        }

        private sealed class StubProvider : IMvAdProvider
        {
            public bool RewardSupported = true;
            public bool InterstitialSupported = true;
            public MvAdResultCode? Immediate = MvAdResultCode.Completed; // null = 나중에 Complete로
            public Action<MvAdResult> Pending;
            public readonly List<(MvAdKind kind, string placement)> Shown = new List<(MvAdKind, string)>();

            public bool Supports(MvAdKind kind) => kind == MvAdKind.Reward ? RewardSupported : InterstitialSupported;

            public void Show(MvAdKind kind, string placement, Action<MvAdResult> done)
            {
                Shown.Add((kind, placement));
                if (Immediate.HasValue) done(new MvAdResult(kind, placement, Immediate.Value));
                else Pending = done;
            }
        }

        private sealed class StubAdManager : IADManager
        {
            public bool initComplete { get; set; } = true;
            public bool IsRewardAdReady => true;
            public bool IsRewardAdLoading => false;
            public event Action<ADRevenuePaidEventData> OnAdRevenuePaid { add { } remove { } }
            public ADRewardRequest LastRequest;
            public ADRewardResult Next;

            public void ShowRewardAD(ADRewardRequest request, Action<ADRewardResult> callBack)
            {
                LastRequest = request;
                callBack?.Invoke(Next);
            }

            public void RequestRewardAD(string adUnitId = "", Action<ADRewardResult> callBack = null) { }
            public void RequestRewardAD(ADRewardRequest request, Action<ADRewardResult> callBack = null) { }
            public void Init() { }
        }

        private static ADRewardResult RewardResult(bool success, bool granted, bool skipped, string error = "") =>
            new ADRewardResult(success, success ? 0 : 1, error, "", "", "", "", "", granted, "", 0, false, false, skipped);

        [Test]
        public void NoProvider_DevBuild_FakeAdCompletesAfterThreeSeconds()
        {
            var timer = new ManualTimer();
            var ads = new MvAds(null, fakeWhenUnsupported: true, timer);
            MvFakeAdSession session = null;
            var seconds = new List<int>();
            ads.FakeAdStarted += s => { session = s; seconds.Add(s.SecondsLeft); s.Changed += c => seconds.Add(c.SecondsLeft); };
            MvAdResult? result = null;

            ads.ShowReward("result_x3", r => result = r);

            Assert.That(session, Is.Not.Null);
            Assert.That(session.Kind, Is.EqualTo(MvAdKind.Reward));
            Assert.That(session.Placement, Is.EqualTo("result_x3"));
            Assert.That(ads.IsShowing, Is.True);

            timer.Advance(TimeSpan.FromMilliseconds(2999));
            Assert.That(result, Is.Null, "3초 전에는 끝나지 않는다");
            Assert.That(session.IsOpen, Is.True);

            timer.Advance(TimeSpan.FromMilliseconds(1));
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.Code, Is.EqualTo(MvAdResultCode.Completed));
            Assert.That(result.Value.IsFake, Is.True);
            Assert.That(result.Value.Placement, Is.EqualTo("result_x3"));
            Assert.That(session.IsOpen, Is.False);
            Assert.That(ads.IsShowing, Is.False);
            Assert.That(seconds, Is.EqualTo(new[] { 3, 2, 1, 0 }), "정본 카운트다운 3→2→1→닫힘");
        }

        [Test]
        public void NoProvider_ReleaseBuild_Unsupported_NoFakeAd()
        {
            var ads = new MvAds(null, fakeWhenUnsupported: false, new ManualTimer());
            bool fakeStarted = false;
            ads.FakeAdStarted += _ => fakeStarted = true;
            var results = new List<MvAdResult>();

            ads.ShowReward("basket_free_dia", results.Add);
            ads.ShowInterstitial("result_interstitial", results.Add);

            Assert.That(fakeStarted, Is.False);
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].Code, Is.EqualTo(MvAdResultCode.Unsupported));
            Assert.That(results[0].Kind, Is.EqualTo(MvAdKind.Reward));
            Assert.That(results[1].Code, Is.EqualTo(MvAdResultCode.Unsupported));
            Assert.That(results[1].Kind, Is.EqualTo(MvAdKind.Interstitial));
            Assert.That(ads.IsShowing, Is.False);
        }

        [Test]
        public void RewardAndInterstitial_AreRoutedByKind()
        {
            var provider = new StubProvider { InterstitialSupported = false };
            var timer = new ManualTimer();
            var ads = new MvAds(provider, fakeWhenUnsupported: true, timer);
            var fakeKinds = new List<MvAdKind>();
            ads.FakeAdStarted += s => fakeKinds.Add(s.Kind);
            MvAdResult? reward = null, inter = null;

            ads.ShowReward("result_x3", r => reward = r);
            Assert.That(reward.Value.Code, Is.EqualTo(MvAdResultCode.Completed));
            Assert.That(reward.Value.Kind, Is.EqualTo(MvAdKind.Reward));
            Assert.That(reward.Value.IsFake, Is.False, "보상형은 공급자가 보여 준다");

            ads.ShowInterstitial("result_interstitial", r => inter = r);
            Assert.That(fakeKinds, Is.EqualTo(new[] { MvAdKind.Interstitial }), "전면만 공급자 미지원 → 가짜");
            timer.Advance(TimeSpan.FromSeconds(MvAds.FakeAdSeconds));
            Assert.That(inter.Value.Code, Is.EqualTo(MvAdResultCode.Completed));
            Assert.That(inter.Value.Kind, Is.EqualTo(MvAdKind.Interstitial));
            Assert.That(inter.Value.IsFake, Is.True);

            Assert.That(provider.Shown, Is.EqualTo(new[] { (MvAdKind.Reward, "result_x3") }));
        }

        [Test]
        public void ConcurrentRequest_IsRejectedWhileShowing()
        {
            var timer = new ManualTimer();
            var ads = new MvAds(null, fakeWhenUnsupported: true, timer);
            int started = 0;
            ads.FakeAdStarted += _ => started++;
            MvAdResult? first = null, second = null, third = null;

            ads.ShowReward("result_x3", r => first = r);
            ads.ShowInterstitial("result_interstitial", r => second = r);

            Assert.That(second, Is.Not.Null);
            Assert.That(second.Value.Code, Is.EqualTo(MvAdResultCode.Failed));
            Assert.That(second.Value.Error, Is.EqualTo(MvAdErrors.Busy));
            Assert.That(started, Is.EqualTo(1));

            timer.Advance(TimeSpan.FromSeconds(3));
            Assert.That(first.Value.Code, Is.EqualTo(MvAdResultCode.Completed));

            ads.ShowReward("basket_free_dia", r => third = r);
            Assert.That(started, Is.EqualTo(2), "끝난 뒤에는 다시 받는다");
            timer.Advance(TimeSpan.FromSeconds(3));
            Assert.That(third.Value.Code, Is.EqualTo(MvAdResultCode.Completed));
        }

        [Test]
        public void ConcurrentRequest_IsRejectedWhileProviderShowing_AndNextAdCanChainInCallback()
        {
            var provider = new StubProvider { Immediate = null };
            var ads = new MvAds(provider, fakeWhenUnsupported: false, new ManualTimer());
            MvAdResult? busy = null, chained = null;

            ads.ShowReward("result_x3", _ =>
            {
                provider.Immediate = MvAdResultCode.Completed;
                ads.ShowInterstitial("result_interstitial", r => chained = r);
            });
            ads.ShowReward("rebirth_skip", r => busy = r);
            Assert.That(busy.Value.Error, Is.EqualTo(MvAdErrors.Busy));

            provider.Pending(new MvAdResult(MvAdKind.Reward, "result_x3", MvAdResultCode.Completed));
            provider.Pending(new MvAdResult(MvAdKind.Reward, "result_x3", MvAdResultCode.Failed)); // 두 번째 알림은 무시
            Assert.That(chained, Is.Not.Null, "완료 콜백 안에서 다음 광고를 부를 수 있다");
            Assert.That(chained.Value.Code, Is.EqualTo(MvAdResultCode.Completed));
            Assert.That(ads.IsShowing, Is.False);
        }

        [Test]
        public void FakeAd_ClosedEarly_IsSkipped()
        {
            var timer = new ManualTimer();
            var ads = new MvAds(null, fakeWhenUnsupported: true, timer);
            MvFakeAdSession session = null;
            ads.FakeAdStarted += s => session = s;
            var results = new List<MvAdResult>();

            ads.ShowReward("rebirth_skip", results.Add);
            timer.Advance(TimeSpan.FromSeconds(1));
            session.Close();
            timer.Advance(TimeSpan.FromSeconds(5));

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Code, Is.EqualTo(MvAdResultCode.Skipped));
            Assert.That(ads.IsShowing, Is.False);
        }

        [Test]
        public void ProviderResults_PassThrough_AndUnsupportedFallsBackToFakeInDevBuild()
        {
            var provider = new StubProvider { Immediate = MvAdResultCode.Skipped };
            var timer = new ManualTimer();
            var dev = new MvAds(provider, fakeWhenUnsupported: true, timer);
            var results = new List<MvAdResult>();

            dev.ShowReward("result_x3", results.Add);
            provider.Immediate = MvAdResultCode.Failed;
            dev.ShowReward("result_x3", results.Add);
            provider.Immediate = MvAdResultCode.Unsupported;
            dev.ShowReward("result_x3", results.Add);
            timer.Advance(TimeSpan.FromSeconds(3));

            Assert.That(results.ConvertAll(r => r.Code),
                Is.EqualTo(new[] { MvAdResultCode.Skipped, MvAdResultCode.Failed, MvAdResultCode.Completed }));
            Assert.That(results[2].IsFake, Is.True);
        }

        [Test]
        public void EmptyPlacement_Fails()
        {
            var ads = new MvAds(null, fakeWhenUnsupported: true, new ManualTimer());
            MvAdResult? result = null;
            ads.ShowReward("", r => result = r);
            Assert.That(result.Value.Code, Is.EqualTo(MvAdResultCode.Failed));
            Assert.That(result.Value.Error, Is.EqualTo(MvAdErrors.NoPlacement));
            Assert.That(ads.IsShowing, Is.False);
        }

        [Test]
        public void AdManagerProvider_MapsRewardResults_AndInterstitialIsUnsupported()
        {
            var manager = new StubAdManager { Next = RewardResult(true, true, false) };
            var provider = new MvAdManagerProvider(manager,
                new PlatformLibrary.AdProviderConfiguration("app", () => "unit-reward"));
            var ads = new MvAds(provider, fakeWhenUnsupported: false, new ManualTimer());
            var results = new List<MvAdResult>();

            ads.ShowReward("result_x3", results.Add);
            Assert.That(manager.LastRequest.adUnitId, Is.EqualTo("unit-reward"));
            Assert.That(manager.LastRequest.placement, Is.EqualTo("result_x3"));
            manager.Next = RewardResult(false, false, true);
            ads.ShowReward("result_x3", results.Add);
            manager.Next = RewardResult(false, false, false, "no fill");
            ads.ShowReward("result_x3", results.Add);
            ads.ShowInterstitial("result_interstitial", results.Add);
            manager.initComplete = false;
            ads.ShowReward("result_x3", results.Add);

            Assert.That(results.ConvertAll(r => r.Code), Is.EqualTo(new[]
            {
                MvAdResultCode.Completed, MvAdResultCode.Skipped, MvAdResultCode.Failed,
                MvAdResultCode.Unsupported, MvAdResultCode.Unsupported,
            }));
            Assert.That(results[2].Error, Is.EqualTo("no fill"));
        }
    }
}
