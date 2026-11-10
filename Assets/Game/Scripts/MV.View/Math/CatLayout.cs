using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.View
{
    /// <summary>런 고양이 그림 묶음(03 §3.5 `mochiBack` 행). Throw는 플레이어만(`_Run_B`).</summary>
    public enum CatAnim : byte { Run, Throw, Climb, Idle }

    /// <summary>
    /// 런 고양이(플레이어·친구) 배치·프레임 고르기(02 §2.1.4 고양이 행, 정본 `syncScene` L2754~2764·`draw2D` L3797~3799·`mochiBack` L3679).
    /// 순수 계산. 위치는 게임 좌표(z 앞 +), y는 지면 WY 기준 높이(정본 `unitG.position.y = WY`).
    /// </summary>
    public static class CatLayout
    {
        /// <summary>프레임 묶음 길이(ph 0~2π를 12칸, 03 manifest `frames.count`).</summary>
        public const int Frames = 12;
        /// <summary>플레이어 룩 단계 상한(`Atlas_World_Cat_T1~T8`), 친구 상한(`Atlas_World_Friend_T1~T6`).</summary>
        public const int PlayerTierMax = 8, FriendTierMax = 6;
        /// <summary>친구 무늬 번호(pat 1~4) = 친구 순번 + 1. 친구 위상 = ph + 순번·1.7(L3799).</summary>
        public const double FriendPhaseStep = 1.7;

        public const string PlayerAtlasPrefix = "Atlas_World_Cat_T", FriendAtlasPrefix = "Atlas_World_Friend_T";

        /// <summary>
        /// 그림 상태(정본 `cst = CN ? 'climb' : run ? 'run' : 'idle'`, run = 런 중이고 끝나지 않음).
        /// 플레이어가 던지는 중(`thr &gt; 0`)이면 달리기 대신 <see cref="CatAnim.Throw"/>. 친구는 thr 0(L3799).
        /// </summary>
        public static CatAnim AnimOf(bool climbing, bool running, double throwT, bool player)
        {
            if (climbing) return CatAnim.Climb;
            if (!running) return CatAnim.Idle;
            return player && throwT > 0 ? CatAnim.Throw : CatAnim.Run;
        }

        /// <summary>ph(라디안) → 가장 가까운 프레임 번호 0..11(프레임 i는 ph = i·2π/12에서 구웠다).</summary>
        public static int FrameIndex(double ph)
        {
            if (double.IsNaN(ph) || double.IsInfinity(ph)) return 0;
            double step = Math.PI * 2 / Frames;
            double m = ph % (Math.PI * 2);
            if (m < 0) m += Math.PI * 2;
            return (int)Math.Round(m / step) % Frames;
        }

        /// <summary>플레이어 룩 t = max(1, 손패 최고 등급)(L3798), 1~8.</summary>
        public static int PlayerTier(int handTop) => Math.Max(1, Math.Min(PlayerTierMax, handTop));

        /// <summary>친구 룩 t = max(1, 손패 최고 − 2)(L3799), 1~6.</summary>
        public static int FriendTier(int handTop) => Math.Max(1, Math.Min(FriendTierMax, handTop - 2));

        public static string AtlasOf(int pat, int tier) => (pat == 0 ? PlayerAtlasPrefix : FriendAtlasPrefix) + tier;

        /// <summary>
        /// 프레임 이름 접두(03 §3.4.1): `World_Cat_P{pat}_T{t}_Run_A_`·`_Run_B_`·`_Climb_` 뒤에 `00`~`11`, 정지는 `World_Cat_P{pat}_T{t}_Idle`(한 장).
        /// 친구에게는 `_Run_B`가 없으므로 Throw는 Run으로 그린다.
        /// </summary>
        public static string FrameName(int pat, int tier, CatAnim anim)
        {
            string head = "World_Cat_P" + pat + "_T" + tier;
            switch (anim)
            {
                case CatAnim.Climb: return head + "_Climb_";
                case CatAnim.Idle: return head + "_Idle";
                case CatAnim.Throw: return pat == 0 ? head + "_Run_B_" : head + "_Run_A_";
                default: return head + "_Run_A_";
            }
        }

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

        /// <summary>
        /// 정지 숨쉬기(mochiBack L3682 `sq = 1 + sin(now·2.2 + pid)·0.015`, `scale(1/√sq, sq)`) — 발 기준 Transform 배율.
        /// </summary>
        public static void IdleBreath(double nowSec, int pat, out double sx, out double sy)
        {
            double sq = 1 + Math.Sin(nowSec * 2.2 + pat) * 0.015;
            sx = 1 / Math.Sqrt(sq);
            sy = sq;
        }
    }
}
