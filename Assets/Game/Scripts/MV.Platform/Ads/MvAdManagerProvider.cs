using System;
using jjevol;
using PlatformLibrary;

namespace MV.Platform
{
    /// <summary>
    /// platform.core 고정본 <see cref="IADManager"/>(보상형만)를 <see cref="IMvAdProvider"/>로 감싼다.
    /// 전면은 <see cref="IADManager"/>에 없어 Unsupported(개발 빌드는 가짜 광고).
    /// Framework 대체: platform.ads 고정본이 나오면 <c>AdManager.ShowInterstitialAd</c>를 여기 붙인다.
    /// </summary>
    public sealed class MvAdManagerProvider : IMvAdProvider
    {
        private readonly IADManager _manager;
        private readonly AdProviderConfiguration _config;

        public MvAdManagerProvider(IADManager manager, AdProviderConfiguration config = null)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _config = config;
        }

        public bool Supports(MvAdKind kind) => kind == MvAdKind.Reward && _manager.initComplete;

        public void Show(MvAdKind kind, string placement, Action<MvAdResult> done)
        {
            if (!Supports(kind)) { done?.Invoke(new MvAdResult(kind, placement, MvAdResultCode.Unsupported, error: MvAdErrors.NoProvider)); return; }

            string unitId = _config?.RewardedAdUnitId() ?? string.Empty;
            _manager.ShowRewardAD(new ADRewardRequest(unitId, placement), r => done?.Invoke(Map(kind, placement, r)));
        }

        /// <summary>보상 지급됨 → Completed, 건너뜀 → Skipped, 나머지 → Failed.</summary>
        public static MvAdResult Map(MvAdKind kind, string placement, ADRewardResult r)
        {
            if (r.success && r.rewardGranted) return new MvAdResult(kind, placement, MvAdResultCode.Completed);
            if (r.isSkipped) return new MvAdResult(kind, placement, MvAdResultCode.Skipped);
            string error = string.IsNullOrEmpty(r.errorMessage) ? $"code {r.code}" : r.errorMessage;
            return new MvAdResult(kind, placement, MvAdResultCode.Failed, error: error);
        }
    }
}
