using System;

namespace MV.Platform
{
    /// <summary>광고 종류(07 §7.8.8 <c>08_adPlacement.adType</c>).</summary>
    public enum MvAdKind
    {
        Reward = 0,
        Interstitial = 1,
    }

    /// <summary>광고 한 번의 결과.</summary>
    public enum MvAdResultCode
    {
        /// <summary>끝까지 봤다(보상형이면 보상 지급 대상).</summary>
        Completed = 0,
        /// <summary>사용자가 중간에 닫았다.</summary>
        Skipped = 1,
        /// <summary>공급자 오류·동시 요청 거절·자리 이름 없음.</summary>
        Failed = 2,
        /// <summary>공급자가 없거나 이 종류를 지원하지 않는다(출시 빌드).</summary>
        Unsupported = 3,
    }

    /// <summary><see cref="MvAdResult.Error"/>에 들어가는 이유 문자열.</summary>
    public static class MvAdErrors
    {
        public const string Busy = "Busy";
        public const string NoPlacement = "NoPlacement";
        public const string NoProvider = "NoProvider";
        public const string ProviderException = "ProviderException";
    }

    public readonly struct MvAdResult
    {
        public MvAdKind Kind { get; }
        /// <summary><c>08_adPlacement.placement</c>.</summary>
        public string Placement { get; }
        public MvAdResultCode Code { get; }
        /// <summary>가짜 광고(공급자 없음, 개발 빌드)로 끝났으면 true.</summary>
        public bool IsFake { get; }
        /// <summary>실패·미지원 이유(<see cref="MvAdErrors"/> 또는 공급자 메시지). 성공이면 빈 문자열.</summary>
        public string Error { get; }

        public bool IsCompleted => Code == MvAdResultCode.Completed;

        public MvAdResult(MvAdKind kind, string placement, MvAdResultCode code, bool isFake = false, string error = "")
        {
            Kind = kind;
            Placement = placement ?? string.Empty;
            Code = code;
            IsFake = isFake;
            Error = error ?? string.Empty;
        }

        public override string ToString() => $"{Kind}/{Placement}: {Code}{(IsFake ? " (fake)" : "")}{(Error.Length > 0 ? " " + Error : "")}";
    }

    /// <summary>
    /// 게임이 부르는 광고 인터페이스. 한도·보상 지급은 MV.Meta 몫이고 여기서는 보여 주고 결과만 알린다.
    /// 공급자 없음 → 개발 빌드는 가짜 광고(<see cref="FakeAdStarted"/>, 정본 3초) 뒤 Completed, 출시 빌드는 Unsupported.
    /// 한 번에 하나만 보여 준다. 보여 주는 중 요청은 바로 Failed(<see cref="MvAdErrors.Busy"/>).
    /// </summary>
    public interface IMvAds
    {
        /// <summary>광고(가짜 포함)를 보여 주는 중.</summary>
        bool IsShowing { get; }

        /// <summary>가짜 광고 시작. 화면(MV-UI-LOBBY)이 팝업을 띄우고 <see cref="MvFakeAdSession.Changed"/>로 남은 초·닫힘을 따른다.</summary>
        event Action<MvFakeAdSession> FakeAdStarted;

        /// <summary>보상형. <paramref name="placement"/>는 <c>08_adPlacement.placement</c>.</summary>
        void ShowReward(string placement, Action<MvAdResult> done);

        /// <summary>전면.</summary>
        void ShowInterstitial(string placement, Action<MvAdResult> done);
    }

    /// <summary>
    /// 실제 광고 공급자 자리.
    /// Framework 대체: platform.ads 고정본이 나오면 <c>AdManager.ShowRewardAd/ShowInterstitialAd</c>를 감싼 구현으로 교체.
    /// </summary>
    public interface IMvAdProvider
    {
        /// <summary>지금 이 종류를 보여 줄 수 있으면 true. false면 <see cref="MvAds"/>가 Unsupported로 본다.</summary>
        bool Supports(MvAdKind kind);

        /// <summary>광고를 보여 주고 결과를 한 번 알린다. Unsupported를 알리면 개발 빌드는 가짜 광고로 넘어간다.</summary>
        void Show(MvAdKind kind, string placement, Action<MvAdResult> done);
    }

    /// <summary>가짜 광고 대기 시계. 시험은 손으로 움직이는 구현을 넣는다.</summary>
    public interface IMvAdTimer
    {
        void After(TimeSpan delay, Action callback);
    }
}
