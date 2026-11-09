using Cysharp.Threading.Tasks;
using jjevol;

namespace MV.Boot
{
    /// <summary>
    /// Game 씬 초기화(기획서 10 §10.3). 로비 화면을 여는 일은 MV-UI-LOBBY가 여기에 더한다.
    /// Editor에서 Game 씬을 바로 실행하면 Boot가 돌지 않으므로, 데이터가 없는 경우를 스스로 처리해야 한다.
    /// </summary>
    public sealed class GameInitializer : SceneInitializerBase
    {
        /// <summary>이 프로세스에서 초기화가 돈 횟수. 시험이 읽는다.</summary>
        public static int InitializeCount { get; private set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => InitializeCount = 0;

        public override UniTask OnInitializeAsync()
        {
            InitializeCount++;
            return UniTask.CompletedTask;
        }
    }
}
