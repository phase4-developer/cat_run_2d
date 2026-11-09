using System;

namespace MV.View
{
    /// <summary>
    /// 뷰 깊이·안개·정렬 순서 계산(기획서 02 §2.1.1, §2.3.2, §2.4). 순수 계산이라 시험이 직접 부른다.
    /// d는 카메라 전방 축 투영 거리(유클리드 거리 아님, 정본 `pj`의 d).
    /// </summary>
    public static class DepthMath
    {
        /// <summary>sortingOrder는 int16이라 이 범위로 자른다.</summary>
        public const int MinOrder = short.MinValue, MaxOrder = short.MaxValue;

        /// <summary>정렬 범주 순위(같은 깊이 칸이면 먼저 넣은 범주가 뒤, 02 §2.3.2).</summary>
        public const int RankProp = 0, RankObject = 1, RankUnit = 2, RankFx = 3;

        /// <summary>전방 단위 벡터(주시점 − 위치를 정규화).</summary>
        public static Vector3d Forward(Vector3d camPos, Vector3d camLook) => (camLook - camPos).Normalized;

        /// <summary>d = (p − cam)·forward. <paramref name="forward"/>는 단위 벡터.</summary>
        public static double Depth(Vector3d p, Vector3d camPos, Vector3d forward) => Vector3d.Dot(p - camPos, forward);

        /// <summary>정본 `fogK`: clamp01((d − fogStart)/fogRange).</summary>
        public static double FogK(double d, double fogStart, double fogRange) =>
            Math.Max(0, Math.Min(1, (d - fogStart) / fogRange));

        /// <summary>그림 알파 = 1 − fogK(02 §2.4.1). 고양이·이펙트·가산광은 쓰지 않는다.</summary>
        public static double FogAlpha(double d, double fogStart, double fogRange) => 1 - FogK(d, fogStart, fogRange);

        /// <summary>정본 근평면 컷: 앵커 d &lt; nearCut이면 그 그림 전체를 숨긴다.</summary>
        public static bool IsNearCut(double d, double nearCut) => d < nearCut;

        /// <summary>
        /// sortingOrder = −floor((d + bias)·Q)·4 + rank (02 §2.3.2). 먼 것이 작은 값(먼저 그림).
        /// int16 범위를 넘으면 끝값으로 자른다(d ≤ 127 m면 넘지 않는다).
        /// </summary>
        public static int SortingOrder(double d, double bias, int rank, double quant)
        {
            double v = -Math.Floor((d + bias) * quant) * 4 + rank;
            if (v < MinOrder) return MinOrder;
            if (v > MaxOrder) return MaxOrder;
            return (int)v;
        }

        /// <summary>
        /// 그룹 안 깊이 순 정렬(우물 벽돌 등, 02 §2.3.2): 깊이 내림차순(먼 것부터) 순위를 0..n−1로 <paramref name="order"/>에 쓴다.
        /// 같은 깊이는 넣은 순서(안정 정렬, JS `Array.sort`와 같음). 할당 없음: <paramref name="scratch"/>는 길이 n 이상.
        /// </summary>
        public static void RankFarFirst(double[] depth, int n, int[] order, int[] scratch)
        {
            if (n > depth.Length || n > order.Length || n > scratch.Length) throw new ArgumentException("배열 길이가 n보다 짧습니다");
            for (int i = 0; i < n; i++) scratch[i] = i;
            // 삽입 정렬(안정). 그룹은 수십 개라 충분하다.
            for (int i = 1; i < n; i++)
            {
                int cur = scratch[i];
                int j = i - 1;
                while (j >= 0 && depth[scratch[j]] < depth[cur]) { scratch[j + 1] = scratch[j]; j--; }
                scratch[j + 1] = cur;
            }
            for (int r = 0; r < n; r++) order[scratch[r]] = r;
        }

        /// <summary>내려다보는 각(도): 수평과 시선 사이.</summary>
        public static double PitchDeg(Vector3d camPos, Vector3d camLook)
        {
            var v = camLook - camPos;
            double h = Math.Sqrt(v.x * v.x + v.z * v.z);
            return Math.Atan2(-v.y, h) * 180.0 / Math.PI;
        }

        /// <summary>
        /// 지면(높이 <paramref name="groundY"/>, x = <paramref name="x"/>) 위에서 뷰 깊이가 <paramref name="depth"/>인 점의 z.
        /// 시선이 x로 기울지 않는다(위치·주시점 x가 같다)고 보고 z 방향 한 축으로 푼다.
        /// </summary>
        public static double GroundZAtDepth(Vector3d camPos, Vector3d camLook, double groundY, double x, double depth)
        {
            var f = Forward(camPos, camLook);
            if (Math.Abs(f.z) < 1e-12) throw new InvalidOperationException("시선이 z축과 수직입니다");
            double rest = (x - camPos.x) * f.x + (groundY - camPos.y) * f.y;
            return camPos.z + (depth - rest) / f.z;
        }

        /// <summary>
        /// 화면 고정 굵기 px를 깊이 d의 월드 m로 바꾼다: px · 2·d·tan(vFov/2) / H. H는 기준 px(view.refHeightPx), vFov는 기준 세로 FOV.
        /// </summary>
        public static double PxToMeters(double px, double depth, double vFovDeg, double refHeightPx) =>
            px * 2 * depth * Math.Tan(vFovDeg * 0.5 * Math.PI / 180.0) / refHeightPx;
    }
}
