using UnityEngine;

namespace MV.Fx
{
    /// <summary>MvFx.Tick을 매 프레임 부르는 숨은 오브젝트.</summary>
    public sealed class MvFxRunner : MonoBehaviour
    {
        static MvFxRunner s_instance;

        public static MvFxRunner Ensure()
        {
            if (s_instance != null) return s_instance;
            var go = new GameObject("MvFx");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<MvFxRunner>();
            return s_instance;
        }

        void Update() => MvFx.Tick();

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
