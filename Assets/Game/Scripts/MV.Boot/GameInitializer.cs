using Cysharp.Threading.Tasks;
using jjevol;
using MV.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MV.Boot
{
    /// <summary>
    /// Game 씬 초기화(기획서 10 §10.3). 게임 루프(<see cref="MvGameLoop"/>)를 붙여 월드·로비 무대를 만들고 로비를 연다(MV-PLAY-INT).
    /// Editor에서 Game 씬을 바로 실행하면 Boot가 돌지 않아 표·저장·UI가 없다(데이터 없음). 이때는 Boot 씬을 먼저 열어
    /// 정상 순서(Boot → Game)로 다시 들어온다. Boot가 돌았는데 표·UI가 없으면(시작 실패) 루프를 만들지 않고 경고만 남긴다.
    /// </summary>
    public sealed class GameInitializer : SceneInitializerBase
    {
        /// <summary>이 프로세스에서 초기화가 돈 횟수. 시험이 읽는다.</summary>
        public static int InitializeCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => InitializeCount = 0;

        public override async UniTask OnInitializeAsync()
        {
            InitializeCount++;
            if (GameBootstrap.State == BootState.NotStarted && !GameBootstrap.ServicesReady)
            {
                // 데이터 없음 경로: Boot 없이 Game 씬을 바로 실행했다. Boot부터 다시 돈다(Boot가 끝나면 이 씬을 다시 연다).
                Debug.Log("[GameInitializer] Boot 없이 시작했습니다. Boot 씬부터 엽니다.");
                SceneManager.LoadScene(GameBootstrap.BootSceneName, LoadSceneMode.Single);
                return;
            }
            var tables = MvTables.Current;
            if (!GameBootstrap.ServicesReady || tables == null)
            {
                Debug.LogWarning("[GameInitializer] 시작 서비스(표·UI)가 준비되지 않아 게임 루프를 만들지 않습니다.");
                return;
            }
            var loop = gameObject.AddComponent<MvGameLoop>();
            await loop.InitAsync(tables);
        }
    }
}
