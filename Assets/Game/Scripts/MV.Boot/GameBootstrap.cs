using System;
using Cysharp.Threading.Tasks;
using jjevol;
using jjevol.Audio;
using jjevol.Effect;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MV.Boot
{
    public enum BootState
    {
        NotStarted,
        Running,
        Succeeded,
        Failed,
    }

    /// <summary>
    /// Boot 씬의 시작 순서(기획서 10 §10.7).
    /// MV-00은 1~3단계와 10단계만 돈다. 4단계(표)·5단계(재화)·6단계(저장)·7단계(다국어)·8단계(UI)·9단계(플랫폼)는
    /// MV-DATA, MV-SAVE, MV-UI-*, MV-PLAT-* 계약이 표시한 자리에 끼운다.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const string GameSceneName = "Game";

        [SerializeField] UIManagerCreater ui;

        /// <summary>마지막 시작 결과. 시험과 개발 화면이 읽는다.</summary>
        public static BootState State { get; private set; }

        public static Exception LastError { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            State = BootState.NotStarted;
            LastError = null;
        }

        void Start()
        {
            RunAsync().Forget();
        }

        async UniTaskVoid RunAsync()
        {
            State = BootState.Running;
            LastError = null;
            try
            {
                if (ui) DontDestroyOnLoad(ui.gameObject);

                // 1. Addressables. Managed Addressables 고정본 1.3.12-pre.10에는 앱 포함 A 로더(ManagedPackagedRevisionLoader)가 없다
                //    (pre.11에서 추가, Framework Delivery.md). 그 버전으로 올릴 때 여기서 Revision을 연다.
                var init = Addressables.InitializeAsync(false);
                await init.Task;
                var initialized = init.Status == AsyncOperationStatus.Succeeded;
                var initError = init.OperationException;
                Addressables.Release(init);
                if (!initialized) throw new InvalidOperationException("Addressables initialization failed.", initError);

                // 2. Addressables 관리자.
                await AddressablesManager.Instance.InitAsync();

                // 3. 공급자. 소리·이펙트를 처음 불러오기 전에 정한다.
                AudioManager.Instance.SetProvider(ProviderKind.Addressables);
                EffectManager.Instance.SetPrefabProvider(new AddressablesEffectPrefabProvider());

                // 4~9. (MV-DATA, MV-SAVE, MV-UI-*, MV-PLAT-*에서 추가)

                // 10. 게임 씬. Boot 오브젝트는 이 전환에서 사라지므로 여기 뒤에서는 정적 상태만 쓴다.
                var result = await CustomSceneManager.Instance.LoadSceneAsync(GameSceneName);
                if (!result.IsSuccess)
                    throw new InvalidOperationException($"Game scene load {result.Status} at {result.Stage}.", result.Exception);

                State = BootState.Succeeded;
            }
            catch (Exception exception)
            {
                LastError = exception;
                State = BootState.Failed;
                Debug.LogException(exception);
            }
        }
    }
}
