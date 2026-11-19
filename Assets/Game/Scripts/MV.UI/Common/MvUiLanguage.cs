using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace MV.UI
{
    /// <summary>
    /// 언어 고르기 입구(설정 → 언어 팝업, D-LOC-PLAN ③). 구현은 부트 7단계(<c>MV.Boot.MvBootLocalization</c>)가 LocalizationManager로 넣는다.
    /// MV.UI는 Localization 패키지를 직접 참조하지 않는다.
    /// </summary>
    public interface IMvLanguageService
    {
        /// <summary>고를 수 있는 언어 코드(LocalizationSettings Locale 순서, 지금 ko·en·ja).</summary>
        IReadOnlyList<string> Codes { get; }
        /// <summary>지금 언어 코드.</summary>
        string Current { get; }
        /// <summary>그 언어로 적은 언어 이름(<c>appInfo/LanguageDisplayName</c>).</summary>
        string DisplayName(string code);
        /// <summary>언어를 바꾸고 저장한다(저장은 SaveLanguage 훅 → <c>meta.setLanguage</c>).</summary>
        UniTask SetAsync(string code);
    }

    /// <summary>언어 서비스 자리. 부트 전·시험에서는 null이다.</summary>
    public static class MvUiLanguage
    {
        public static IMvLanguageService Service { get; set; }
    }
}
