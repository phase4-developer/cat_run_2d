using System;

namespace MV.View
{
    /// <summary>
    /// 계단 정산 옆 시점 가림 처리(노션 「신작 배경 리소스 관리」 계단연출 3절, 새 HTML `streetProps`·`drawGround`·`XCUT`).
    /// e = 옆 시점 돌기 진행(<see cref="CameraModel.OrbitE"/>, 0 = 뒤, 1 = 옆). 카메라는 오른쪽(+x)으로 돈다.
    /// 가까운 쪽(x &gt; 0) 길가 가게·소품 알파 = max(0, 1 − e·2.2), 바닥 오른쪽 끝 x = 70 − 64.9·min(1, e/0.6),
    /// 카메라가 잘린 면 바깥(x &gt; 끝 + 0.2)이면 그 자리에 땅 단면을 세운다. 수치는 새 HTML 상수 그대로다.
    /// </summary>
    public static class ClimbOcclusion
    {
        /// <summary>HTML `nearA = max(0, 1 - ORB.e*2.2)`.</summary>
        public const double NearFadeK = 2.2;
        /// <summary>HTML `XCUT = 70 - 64.9*min(1, ORB.e/0.6)`: 자르기 전 끝 70 m, 다 자른 끝 5.1 m, e 0.6에서 다 자름.</summary>
        public const double CutFar = 70, CutNear = 70 - 64.9, CutFullE = 0.6;
        /// <summary>HTML `camGX() > XC + 0.2`일 때만 단면을 그린다.</summary>
        public const double SectionGap = 0.2;

        /// <summary>가까운 쪽(x &gt; 0) 소품 알파 배수. 먼 쪽은 1.</summary>
        public static float NearAlpha(double e, double x) => x > 0 ? (float)Math.Max(0, 1 - e * NearFadeK) : 1f;

        /// <summary>바닥 오른쪽 끝 x(m).</summary>
        public static double CutX(double e) => CutFar - (CutFar - CutNear) * Math.Min(1, Math.Max(0, e) / CutFullE);

        /// <summary>단면을 보일지(카메라 x가 잘린 끝 바깥).</summary>
        public static bool ShowSection(double camX, double e) => camX > CutX(e) + SectionGap;
    }
}
