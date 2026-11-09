using System.Threading;
using Cysharp.Threading.Tasks;
using jjevol;

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
            // 문구: 다국어(7단계)가 연결되면 LocalizationManager.GetLoc(collection, key)로 바꾼다. 지금은 키를 그대로 돌려준다.
            if (UIRuntimeOptions.Localize == null) UIRuntimeOptions.Localize = (collection, key) => key;
            // 뒤로 가기 종료 확인: 종료 확인 팝업은 2차. 지금은 확인 없이 종료를 허용한다(Framework는 이 값이 없으면 오류).
            if (UINavigationOptions.ConfirmExitAsync == null) UINavigationOptions.ConfirmExitAsync = _ => UniTask.FromResult(true);
        }

        public static async UniTask InitAsync(UIManagerCreater creator, CancellationToken token = default)
        {
            ConfigureOptions();
            if (creator) await creator.StartInitAsync(token);
        }
    }
}
