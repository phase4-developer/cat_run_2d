using System;

namespace MV.Meta
{
    /// <summary>
    /// 메타 판정이 쓰는 현재 시각(UTC Unix ms). 일일 초기화·부활 쿨타임이 읽는다. 시험은 고정 시계를 넣는다.
    /// Framework 대체: 서버 시간(platform.network 고정본 없음). 서버가 생기면 서버 보정 시계를 이 인터페이스로 넣는다.
    /// </summary>
    public interface IMvClock
    {
        long NowMs { get; }
    }

    /// <summary>기기 시계(서버 없음, 09 §9.3.4 — 시계 조작은 막지 않는다).</summary>
    public sealed class MvSystemClock : IMvClock
    {
        public static readonly MvSystemClock Instance = new MvSystemClock();
        public long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>손으로 움직이는 시계(시험·개발 도구).</summary>
    public sealed class MvManualClock : IMvClock
    {
        public long NowMs { get; set; }
        public MvManualClock(long nowMs) { NowMs = nowMs; }
        public void Advance(TimeSpan by) => NowMs += (long)by.TotalMilliseconds;
    }

    /// <summary>
    /// 기준 시계 + 보정(개발 빌드 <c>debug.addTime</c>). 보정은 메모리에만 있다(저장하지 않음, 앱을 다시 켜면 0).
    /// 로컬 백엔드 = 서버 자리이므로 서버 시간 보정도 백엔드가 한다. <see cref="MvMeta.Clock"/>도 이 시계다.
    /// </summary>
    public sealed class MvOffsetClock : IMvClock
    {
        public IMvClock Base { get; }
        public long OffsetMs { get; private set; }
        public MvOffsetClock(IMvClock baseClock) { Base = baseClock ?? MvSystemClock.Instance; }
        public long NowMs => Base.NowMs + OffsetMs;
        public void Add(long ms) => OffsetMs = checked(OffsetMs + ms);
    }

    /// <summary>
    /// 광고 공급자(04 §4.12.4, 08_adPlacement). 자리 이름은 <c>08_adPlacement.placement</c>.
    /// Framework 대체: platform.ads 고정본 없음. MV-PLAT-ADS가 <c>AdManager.ShowRewardAd(new AdRequest(placement), cb)</c>를 이 인터페이스로 감싼다.
    /// </summary>
    public interface IMvAdService
    {
        /// <summary>보상 광고. 끝까지 봤으면 <c>true</c>.</summary>
        void ShowReward(string placement, Action<bool> done);
        /// <summary>전면 광고. 결과와 무관하게 끝나면 부른다.</summary>
        void ShowInterstitial(string placement, Action done);
    }

    /// <summary>
    /// 가짜 광고(공급자 없음). 바로 「봤음」으로 끝낸다. 3초 가짜 광고 팝업(10)은 화면(MV-UI)이 이 앞에서 보여 준다.
    /// <see cref="NextRewardResult"/>로 「끝까지 안 봄」을 흉내 낸다.
    /// </summary>
    public sealed class MvFakeAdService : IMvAdService
    {
        public bool NextRewardResult { get; set; } = true;
        public int RewardShown { get; private set; }
        public int InterstitialShown { get; private set; }
        public string LastPlacement { get; private set; }

        public void ShowReward(string placement, Action<bool> done)
        {
            RewardShown++;
            LastPlacement = placement;
            done?.Invoke(NextRewardResult);
        }

        public void ShowInterstitial(string placement, Action done)
        {
            InterstitialShown++;
            LastPlacement = placement;
            done?.Invoke();
        }
    }
}
