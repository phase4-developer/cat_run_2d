using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 임시 진단(MV-DIAG-GATE): 투사체 판정 과정을 Console에 `[HitDbg]`로 남긴다. 원인을 찾으면 이 파일과 호출을 지운다.
    /// 끄려면 <see cref="On"/>을 false로 둔다.
    /// </summary>
    public static class HitDebug
    {
        public static bool On = true;

        public static void Log(string message)
        {
            if (On) Debug.Log("[HitDbg] " + message);
        }
    }
}
