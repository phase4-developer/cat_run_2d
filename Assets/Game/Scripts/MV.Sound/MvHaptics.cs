using UnityEngine;

namespace MV.Sound
{
    /// <summary>
    /// 진동(설정 팝업 진동 On/Off, MV-UI2-SETTING). 정크버스터즈 HapticsUtility와 같이 Android·iOS에서만 <c>Handheld.Vibrate</c>.
    /// 지금은 진동을 울리는 곳이 없다(사용자 2026-11-19 「저장만 해둠」). 울릴 자리는 기획이 정해지면 <see cref="Vibrate"/>를 부른다.
    /// </summary>
    public static class MvHaptics
    {
        /// <summary>설정 저장값(<c>mvSettings.haptic</c>). 부트와 설정 팝업이 넣는다.</summary>
        public static bool IsOn { get; set; } = true;

        /// <summary>켜져 있으면 1번 진동. 모바일이 아니면 아무것도 안 한다.</summary>
        public static void Vibrate()
        {
            if (!IsOn) return;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
