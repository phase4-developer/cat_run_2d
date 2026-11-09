using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("MV.Save.Tests")]

namespace MV.Save
{
    /// <summary>
    /// 저장 진입점. Boot 6단계(10 §10.7)에서 표 다음에 <see cref="Init"/>을 부른다.
    /// 결과 <see cref="MvSaveOpenResult.Notice"/>에 따라 화면이 토스트·복구 팝업을 띄운다.
    /// 참조 정리(09 §9.6)는 <see cref="MvSaveOptions.References"/>가 있으면 열 때마다(다시 시도·새로 시작 포함) 돈다.
    /// </summary>
    public static class MvSave
    {
        private static MvSaveOptions _options;

        /// <summary>열린 저장소. 열리지 않았으면 null.</summary>
        public static MvSaveStore Current { get; private set; }

        /// <summary>
        /// 시험 격리 지점. null이 아니면 <see cref="Init"/>·<see cref="StartFresh"/>가 옵션의 저장소를 이것으로 바꾼다
        /// (실제 PlayerPrefs를 쓰지 않는다). 시험이 Boot 전에 <see cref="MvMemorySavePrefs"/>를 넣고 끝나면 null로 되돌린다.
        /// </summary>
        public static IMvSavePrefs PreferencesOverride { get; set; }

        /// <summary>시험용: 새 메모리 저장소를 <see cref="PreferencesOverride"/>에 끼우고 돌려준다. 끝나면 <c>PreferencesOverride = null</c>.</summary>
        public static MvMemorySavePrefs UseMemoryPreferences()
        {
            var prefs = new MvMemorySavePrefs();
            PreferencesOverride = prefs;
            return prefs;
        }

        /// <summary><see cref="Current"/>가 바뀔 때(열림·닫힘). 닫히면 null. Boot가 재화 서비스(ResourceService)를 다시 묶는다.</summary>
        public static event Action<MvSaveStore> CurrentChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain Reload를 끈 에디터: 이전 Play의 저장소·구독을 버린다.
            Current = null;
            _options = null;
            PreferencesOverride = null;
            CurrentChanged = null;
        }

        /// <summary>저장을 연다(이미 열려 있으면 닫고 다시 연다).</summary>
        public static MvSaveOpenResult Init(MvSaveOptions options = null)
        {
            Shutdown();
            _options = options ?? new MvSaveOptions();
            return SetCurrent(MvSaveStore.Open(Effective(_options)));
        }

        /// <summary>복구 팝업 「다시 시도」, 저장소 팝업 「다시 시도」.</summary>
        public static MvSaveOpenResult Retry() => Init(_options);

        /// <summary>복구 팝업 「새로 시작」(두 번 확인 뒤). 이전 원문은 결과의 <c>RemovedSlotValues</c>.</summary>
        public static MvSaveOpenResult StartFresh()
        {
            Shutdown();
            _options ??= new MvSaveOptions();
            return SetCurrent(MvSaveStore.OpenFresh(Effective(_options)));
        }

        /// <summary>쓰기 실패(<c>LocalSaveWriteException</c>·<c>RepositoryRecoveryRequiredException</c>) 뒤 다시 연다. 화면은 <c>toastMessage.saveFailed</c>.</summary>
        public static MvSaveOpenResult Reopen() => Init(_options);

        /// <summary>Boot의 MonoBehaviour <c>OnApplicationPause(bool)</c>에서 부른다. 메모리만 바뀐 것이 있을 때만 저장한다.</summary>
        public static void OnApplicationPause(bool paused)
        {
            if (!paused || Current == null || Current.IsDisposed || Current.RequiresReopen) return;
            try { Current.SaveIfDirty(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public static void Shutdown()
        {
            if (Current == null) return;
            Current.Dispose();
            Current = null;
            Notify(null);
        }

        private static MvSaveOptions Effective(MvSaveOptions options)
        {
            if (PreferencesOverride == null) return options;
            return new MvSaveOptions
            {
                ProfileId = options.ProfileId,
                KeyNamespace = options.KeyNamespace,
                KeyProvider = options.KeyProvider,
                Preferences = PreferencesOverride,
                Config = options.Config,
                NowMs = options.NowMs,
                Migrations = options.Migrations,
                References = options.References,
            };
        }

        private static MvSaveOpenResult SetCurrent(MvSaveOpenResult result)
        {
            Current = result.Store;
            if (Current != null) Notify(Current);
            return result;
        }

        private static void Notify(MvSaveStore store)
        {
            Action<MvSaveStore> handler = CurrentChanged;
            if (handler == null) return;
            // 구독자 예외는 저장 열기 결과를 가리지 않게 격리한다.
            foreach (Action<MvSaveStore> each in handler.GetInvocationList())
            {
                try { each(store); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
    }
}
