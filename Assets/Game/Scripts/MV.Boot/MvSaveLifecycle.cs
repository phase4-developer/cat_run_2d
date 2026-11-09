using MV.Save;
using UnityEngine;

namespace MV.Boot
{
    /// <summary>
    /// 앱 일시정지·종료 때 메모리만 바뀐 저장을 쓴다(09 §9.4 마지막 줄). Boot 오브젝트는 Game 씬 전환에서 사라지므로
    /// 씬을 고치지 않고 코드로 숨은 오브젝트를 만들어 <c>DontDestroyOnLoad</c>로 살린다. 앱에 1개.
    /// </summary>
    public sealed class MvSaveLifecycle : MonoBehaviour
    {
        static MvSaveLifecycle _instance;

        /// <summary>없으면 만든다. Boot 6단계가 부른다.</summary>
        public static MvSaveLifecycle Ensure()
        {
            if (_instance) return _instance;
            var go = new GameObject("[MvSaveLifecycle]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MvSaveLifecycle>();
            return _instance;
        }

        void OnApplicationPause(bool paused) => MvSave.OnApplicationPause(paused);

        void OnApplicationQuit() => MvSave.OnApplicationPause(true);

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
