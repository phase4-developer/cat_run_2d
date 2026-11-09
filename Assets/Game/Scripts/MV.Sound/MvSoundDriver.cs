using UnityEngine;

namespace MV.Sound
{
    /// <summary>지연 재생(fail 350ms, 덱 머지 450ms)을 매 프레임 흘리는 숨은 오브젝트. <see cref="MvSound.InstallDefault"/>가 만든다.</summary>
    public sealed class MvSoundDriver : MonoBehaviour
    {
        static MvSoundDriver s_instance;

        public static MvSoundDriver Ensure()
        {
            if (s_instance != null) return s_instance;
            var go = new GameObject("MvSound");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<MvSoundDriver>();
            return s_instance;
        }

        void Update() => MvSound.Tick();

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
