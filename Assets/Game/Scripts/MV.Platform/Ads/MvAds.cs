using System;
using Cysharp.Threading.Tasks;

namespace MV.Platform
{
    /// <summary>
    /// <see cref="IMvAds"/> 구현. 공급자 → (없음·미지원이면) 개발 빌드 가짜 광고 / 출시 빌드 Unsupported.
    /// Framework 대체: platform.ads 고정본 없음. 고정본이 나오면 <see cref="IMvAdProvider"/>에 <c>AdManager</c> 감싼 구현을 넣는다.
    /// </summary>
    public sealed class MvAds : IMvAds
    {
        /// <summary>정본 <c>fakeAd</c>의 대기 초.</summary>
        public const int FakeAdSeconds = 3;

        private readonly IMvAdProvider _provider;
        private readonly bool _fakeWhenUnsupported;
        private readonly IMvAdTimer _timer;
        private readonly int _fakeSeconds;

        public bool IsShowing { get; private set; }
        public event Action<MvFakeAdSession> FakeAdStarted;

        /// <param name="provider">null = 공급자 없음.</param>
        /// <param name="fakeWhenUnsupported">개발 빌드면 true(공급자 없음·미지원 → 가짜 광고).</param>
        /// <param name="timer">null이면 실제 시간(<see cref="MvRealtimeAdTimer"/>).</param>
        public MvAds(IMvAdProvider provider, bool fakeWhenUnsupported, IMvAdTimer timer = null, int fakeSeconds = FakeAdSeconds)
        {
            _provider = provider;
            _fakeWhenUnsupported = fakeWhenUnsupported;
            _timer = timer ?? MvRealtimeAdTimer.Instance;
            _fakeSeconds = fakeSeconds;
        }

        /// <summary>기본 구성: 가짜 광고는 개발 빌드(에디터 포함)에서만.</summary>
        public static MvAds CreateDefault(IMvAdProvider provider = null) =>
            new MvAds(provider, UnityEngine.Debug.isDebugBuild);

        public void ShowReward(string placement, Action<MvAdResult> done) => Show(MvAdKind.Reward, placement, done);

        public void ShowInterstitial(string placement, Action<MvAdResult> done) => Show(MvAdKind.Interstitial, placement, done);

        private void Show(MvAdKind kind, string placement, Action<MvAdResult> done)
        {
            if (IsShowing) { done?.Invoke(new MvAdResult(kind, placement, MvAdResultCode.Failed, error: MvAdErrors.Busy)); return; }
            if (string.IsNullOrEmpty(placement)) { done?.Invoke(new MvAdResult(kind, placement, MvAdResultCode.Failed, error: MvAdErrors.NoPlacement)); return; }

            IsShowing = true;
            bool finished = false;
            void Finish(MvAdResult result)
            {
                if (finished) return; // 공급자가 두 번 알려도 한 번만
                finished = true;
                IsShowing = false;   // done 안에서 다음 광고(×1 뒤 전면 등)를 바로 부를 수 있게 먼저 푼다
                done?.Invoke(result);
            }

            if (_provider == null || !_provider.Supports(kind))
            {
                Unsupported(kind, placement, MvAdErrors.NoProvider, Finish);
                return;
            }

            try
            {
                _provider.Show(kind, placement, r =>
                {
                    if (finished) return;
                    if (r.Code == MvAdResultCode.Unsupported) Unsupported(kind, placement, r.Error, Finish);
                    else Finish(new MvAdResult(kind, placement, r.Code, error: r.Error));
                });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                Finish(new MvAdResult(kind, placement, MvAdResultCode.Failed, error: MvAdErrors.ProviderException));
            }
        }

        private void Unsupported(MvAdKind kind, string placement, string error, Action<MvAdResult> finish)
        {
            if (!_fakeWhenUnsupported)
            {
                finish(new MvAdResult(kind, placement, MvAdResultCode.Unsupported, error: error));
                return;
            }

            var session = new MvFakeAdSession(kind, placement, _fakeSeconds, _timer,
                code => finish(new MvAdResult(kind, placement, code, isFake: true)));
            FakeAdStarted?.Invoke(session);
            session.Begin();
        }
    }

    /// <summary>실제 시간 대기(광고 중 <c>Time.timeScale</c>이 0이어도 흐르게 unscaled).</summary>
    public sealed class MvRealtimeAdTimer : IMvAdTimer
    {
        public static readonly MvRealtimeAdTimer Instance = new MvRealtimeAdTimer();

        public void After(TimeSpan delay, Action callback) => Run(delay, callback).Forget();

        private static async UniTaskVoid Run(TimeSpan delay, Action callback)
        {
            await UniTask.Delay(delay, DelayType.UnscaledDeltaTime);
            callback?.Invoke();
        }
    }
}
