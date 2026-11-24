using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Meta;
using MV.Save;
using UnityEngine.Localization.Settings;

namespace MV.Boot
{
    /// <summary>
    /// 시작 7단계(10 §10.7, MV-LOC): 언어 저장 연결 → <c>LocalizationManager.InitLocalization()</c> → UI·표 문구 연결.
    /// 문구 원본은 Google Sheet 「MoonVillage Localization」, 저장소 사본 <c>Assets/Game/GameData/Localization/Sheet</c>,
    /// String Table은 Framework 「Localization Sheet Import」 창(JJEvol/Localization/Sheet Import)의 Apply로 만든다(D-LOC-FWUNIFY).
    /// 원격 덧씌우기 교체 지점: 원격 카탈로그가 생기면 Init 전에 카탈로그를 받고, 갱신 때 PrepareForContentUpdateAsync → 갱신 → ReloadLocalization.
    /// </summary>
    public static class MvBootLocalization
    {
        /// <summary>저장된 언어 코드. 저장을 못 열었거나 고른 적이 없으면 빈 값(기기 언어를 따른다).</summary>
        public static string ReadLanguage()
        {
            var store = MvSave.Current;
            return store != null && !store.IsDisposed ? store.Settings.Language ?? "" : "";
        }

        /// <summary>
        /// 언어 저장. Framework 계약대로 동기로 쓰고, 거절이면 예외로 언어 바꾸기를 막는다.
        /// 저장을 못 연 상태(MvMeta 없음)면 저장하지 않고 언어만 바뀐다(README 「저장 hook이 없으면 Locale만」).
        /// </summary>
        public static void SaveLanguage(string code)
        {
            var meta = MvMeta.Current;
            if (meta == null || !meta.IsReady) return;
            var res = meta.SetLanguage(code);
            if (!res.Ok) throw new InvalidOperationException($"[MvBootLocalization] 언어 저장 거절 {res.Code}: {code}");
        }

        /// <summary>7단계. 실패해도 시작은 계속한다(문구는 MvUiText 대체 문구로 보인다).</summary>
        public static async UniTask InitAsync()
        {
            LocalizationRuntimeOptions.ReadLanguage = ReadLanguage;
            LocalizationRuntimeOptions.SaveLanguage = SaveLanguage;
            await LocalizationManager.Instance.InitLocalization();

            // 8단계 UI 옵션: 없는 키는 빈 값 → MvUiText가 대체 문구를 쓴다. 언어가 바뀌면 화면에 알린다.
            UIRuntimeOptions.Localize = (collection, key) => LocalizationManager.GetLoc(collection, key, false);
            UIDisplayOptions.Language = LocalizationManager.GetCurrentLanguageCode;
            UIDisplayOptions.Culture = () => LocalizationSettings.SelectedLocale?.Identifier.CultureInfo ?? System.Globalization.CultureInfo.InvariantCulture;
            LocalizationEvent.Published -= OnLocalizationEvent;
            LocalizationEvent.Published += OnLocalizationEvent;

            // 표 행 문구(07 §7.12): name·description 컬렉션, 키 = 행 id.
            TableRuntimeOptions.Localization = new MvTableLocalization();

            // 설정 → 언어 팝업(D-LOC-PLAN ③). MV.UI는 Localization을 직접 참조하지 않는다.
            MV.UI.MvUiLanguage.Service = new MvLanguageService();
        }

        /// <summary>언어 팝업 입구: Locale 목록·지금 언어·언어 이름·바꾸기(저장은 SaveLanguage 훅).</summary>
        sealed class MvLanguageService : MV.UI.IMvLanguageService
        {
            public IReadOnlyList<string> Codes
            {
                get
                {
                    var list = new List<string>();
                    if (!LocalizationSettings.HasSettings) return list;
                    var locales = LocalizationSettings.AvailableLocales?.Locales;
                    if (locales == null) return list;
                    foreach (var l in locales)
                        if (l != null) list.Add(l.Identifier.Code);
                    return list;
                }
            }

            public string Current => LocalizationManager.GetCurrentLanguageCode();

            public string DisplayName(string code) => LocalizationManager.GetLanguageDisplayName(code);

            public UniTask SetAsync(string code) => LocalizationManager.Instance.SetLanguage(code, true);
        }

        static void OnLocalizationEvent(LocalizationEvent e) => UIDisplayOptions.NotifyLocaleChanged();

        /// <summary>ITableLocalization: 없는 항목은 null, 있는 빈 번역은 빈 문자열(README 「Table 소비자」).</summary>
        sealed class MvTableLocalization : ITableLocalization
        {
            public bool IsReady => LocalizationManager.Instance != null && LocalizationManager.Instance.IsInitialized;

            public string GetString(string collection, string key)
            {
                if (string.IsNullOrEmpty(collection) || string.IsNullOrEmpty(key) || !LocalizationSettings.HasSettings) return null;
                var entry = LocalizationSettings.StringDatabase.GetTableEntry(collection, key).Entry;
                return entry?.LocalizedValue;
            }
        }
    }
}
