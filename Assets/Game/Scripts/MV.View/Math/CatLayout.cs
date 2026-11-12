using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.View
{
    /// <summary>
    /// 런 캐릭터(플레이어·분신) 배치(02 §2.1.4 고양이 행, 정본 `syncScene` L2754~2764). 그림 고르기는 <see cref="CharStateMachine"/>·`116_charLook`.
    /// 순수 계산. 위치는 게임 좌표(z 앞 +), y는 지면 WY 기준 높이(정본 `unitG.position.y = WY`).
    /// </summary>
    public static class CatLayout
    {
        /// <summary>
        /// 그림 번호 1..count. 플레이 값(단계·순번)이 그림 수보다 크면 마지막 그림을 다시 쓴다(2026-11-11 사용자 결정 「그림 개수는 이전꺼하고 같은걸로 채워」).
        /// 그림 수가 플레이 값을 막지 않는다 — 자르는 것은 그림 고르기뿐이다.
        /// </summary>
        public static int ArtIndex(int value, int count) => Math.Max(1, Math.Min(count, value));

        /// <summary>친구 자리 `CLONE_OFF`(L831) = `314_spawnGroup` key clone 행(dx, dz), 정렬 순서.</summary>
        public static void CloneOffsets(MvTables t, List<(double dx, double dz)> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            if (t?.SpawnGroup == null) return;
            foreach (var g in t.SpawnGroup.Sorted)
                if (g.key == "clone") into.Add((g.dx, g.dz));
        }

        /// <summary>친구 위치: x = clamp(px + ox, −xMax, xMax), z = pz + oz(L2764, 사격 위치와 같은 식).</summary>
        public static void ClonePos(double px, double pz, double ox, double oz, double xMax, out double x, out double z)
        {
            x = Math.Max(-xMax, Math.Min(xMax, px + ox));
            z = pz + oz;
        }

        /// <summary>계단 칸 번호 floor((z − z0)/stepD)(정본 `stairY` L2241).</summary>
        public static int StairIndex(double z, double z0, double stepD) => (int)Math.Floor((z - z0) / stepD);

        /// <summary>
        /// 오르기 중 친구 높이 `stairY(z)`(L2241): 칸 i가 0..k−1이고 떨어져 내려앉았으면(보인 지 dropSec 이상) (i + 1)·stepH, 아니면 0.
        /// </summary>
        public static double StairY(int i, int k, double stepH, bool landed) => i >= 0 && i < k && landed ? (i + 1) * stepH : 0;
    }
}
