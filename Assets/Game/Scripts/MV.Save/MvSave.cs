using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("MV.Save.Tests")]

namespace MV.Save
{
    /// <summary>
    /// 저장 진입점. Boot 6단계(10 §10.7)에서 표·재화 다음에 <see cref="Init"/>을 부른다.
    /// 결과 <see cref="MvSaveOpenResult.Notice"/>에 따라 화면이 토스트·복구 팝업을 띄운다.
    /// 참조 정리(09 §9.6 <c>MvSaveReferenceFix</c>)는 표가 필요해 MV-DATA 통합 뒤 이어서 붙인다.
    /// </summary>
    public static class MvSave
    {
        private static MvSaveOptions _options;

        /// <summary>열린 저장소. 열리지 않았으면 null.</summary>
        public static MvSaveStore Current { get; private set; }

        /// <summary>저장을 연다(이미 열려 있으면 닫고 다시 연다).</summary>
        public static MvSaveOpenResult Init(MvSaveOptions options = null)
        {
            Shutdown();
            _options = options ?? new MvSaveOptions();
            MvSaveOpenResult result = MvSaveStore.Open(_options);
            Current = result.Store;
            return result;
        }

        /// <summary>복구 팝업 「다시 시도」, 저장소 팝업 「다시 시도」.</summary>
        public static MvSaveOpenResult Retry() => Init(_options);

        /// <summary>복구 팝업 「새로 시작」(두 번 확인 뒤). 이전 원문은 결과의 <c>RemovedSlotValues</c>.</summary>
        public static MvSaveOpenResult StartFresh()
        {
            Shutdown();
            _options ??= new MvSaveOptions();
            MvSaveOpenResult result = MvSaveStore.OpenFresh(_options);
            Current = result.Store;
            return result;
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
            Current?.Dispose();
            Current = null;
        }
    }
}
