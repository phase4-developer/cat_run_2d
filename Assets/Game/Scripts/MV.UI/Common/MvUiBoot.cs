using System.Threading;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Data;

namespace MV.UI
{
    /// <summary>
    /// 시작 8단계(10 §10.7): UI 정적 옵션 → <c>ui.StartInitAsync()</c>. MV.Boot의 GameBootstrap이 7단계 뒤에 부른다.
    /// </summary>
    public static class MvUiBoot
    {
        /// <summary>정적 옵션. Play 시작 때 Framework가 비우므로 부트스트랩에서 매번 정한다.</summary>
        public static void ConfigureOptions()
        {
            // 문구: 7단계(MvBootLocalization)가 LocalizationManager.GetLoc으로 이미 정한다. 다국어 초기화가 실패했을 때만 키를 그대로 돌려준다.
            if (UIRuntimeOptions.Localize == null) UIRuntimeOptions.Localize = (collection, key) => key;
            // 뒤로 가기 종료 확인: 종료 확인 팝업은 2차. 지금은 확인 없이 종료를 허용한다(Framework는 이 값이 없으면 오류).
            if (UINavigationOptions.ConfirmExitAsync == null) UINavigationOptions.ConfirmExitAsync = _ => UniTask.FromResult(true);
            // 숫자 표기: Framework 라벨(PriceLabel·PriceBtn 등)도 05 §5.1.12 fmt(10,000↑ x.xK, 1,000,000↑ x.xM)로 쓴다.
            if (UIDisplayOptions.NumberFormat == null) UIDisplayOptions.NumberFormat = new NumberFormatSettings(MvUiFormat.Profile);
            // 잔액 없는 비용(광고·인앱, 07 §7.1.6 ConsumeType): PriceLabel·PriceBtn이 부족 색 없이 늘 충분으로 그린다(ui.economy pre.8).
            if (LocalEconomyUIOptions.UnmeteredCost == null) LocalEconomyUIOptions.UnmeteredCost = IsUnmeteredCost;
        }

        /// <summary>보유량이 없는 비용 종류(광고 시청·인앱 결제).</summary>
        public static bool IsUnmeteredCost(ConsumeTypeId type) =>
            type.Value == (int)ConsumeType.AD || type.Value == (int)ConsumeType.Inapp;

        public static async UniTask InitAsync(UIManagerCreater creator, CancellationToken token = default)
        {
            ConfigureOptions();
            if (creator) await creator.StartInitAsync(token);
        }
    }
}
