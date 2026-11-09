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
    /// 지금은 1~6단계(+ 런 코어 연결 MvBootMeta)와 10단계를 돈다. 7단계(다국어)·8단계(UI)·9단계(플랫폼)는 MV-UI-*, MV-PLAT-* 계약이 표시한 자리에 끼운다.
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

                // 4. 표(MV-DATA). 원본 지정 → RegisterAll → LoadAllAsync → 게임 검증기. 검증 오류면 예외로 시작을 멈춘다.
                //    105 컴포넌트 type 구현 확인은 MV.Run 목록으로 한다(MV.Data는 MV.Run을 모른다).
                await MvBootMeta.LoadTablesAsync();

                // 5·6. 저장(MV-SAVE): 표 값으로 설정·참조 목록 → 저장소 → 이관 → 컨테이너 등록 → Open → 참조 정리(09 §9.5.2).
                //    재화 서비스(5단계)는 저장소의 WealthContainer가 있어야 해서 저장이 열릴 때 MvBootSave가 묶는다.
                //    열지 못해도(복구·업데이트·저장소 팝업) 시작은 계속한다. 화면이 MvBootSave.PendingNotice를 읽는다.
                MvBootSave.Open(MV.Data.MvTables.Current);
                MvSaveLifecycle.Ensure();

                // 6 뒤. 영속 변경 진입점(MV-META). 저장을 못 열었으면 MvMeta.Current는 null로 두고 저장이 열릴 때 만든다.
                MvBootMeta.Attach(MV.Data.MvTables.Current);

                // 7~9. (MV-UI-*, MV-PLAT-*에서 추가)

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
