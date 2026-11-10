using System;
using MV.Data;
using MV.Run;

namespace MV.View
{
    /// <summary>
    /// 런 투사체(던진 장난감)·창·회전 장난감 배치·그림 고르기(02 §2.1.4 「던진 장난감 L3804, 회전 장난감 L3800」·「창 궤적 L3802~3803」 행,
    /// 03 §3.5 L394~395). 순수 계산. 위치는 게임 좌표(z 앞 +), y는 지면 WY 기준 높이.
    /// 회전은 정본 캔버스 `rotate(+a)`(화면 y 아래, 시계 방향) = Unity 로컬 z 회전 −a.
    /// </summary>
    public static class ProjLayout
    {
        /// <summary>투사체 높이(정본 syncScene `place(b.m, b.x, 0.95, b.z)` L2766).</summary>
        public const double ProjY = 0.95;
        /// <summary>그리는 반지름 m: 던진 장난감 0.5(L3804), 회전 장난감 0.6(L3800). 그림은 0.6 m 기준으로 굽는다(03 §3.3.3 160×160).</summary>
        public const double ProjRadius = 0.5, BladeRadius = 0.6, BakedRadius = 0.6;
        /// <summary>회전 주기: 실시간 ms / 110(투사체), / 180 + i·2(회전 장난감).</summary>
        public const double ProjSpinMs = 110, BladeSpinMs = 180, BladePhaseStep = 2;
        /// <summary>창 꼬리 길이 m(뒤 2.8 m 점, L3802), 선 굵기 m(0.32 반투명 0.45, 0.08 흰색).</summary>
        public const double SpearTail = 2.8, SpearWide = 0.32, SpearCore = 0.08, SpearWideAlpha = 0.45;
        public const string SpearColorFire = "#ffb03a", SpearColorOther = "#ff3a4a", SpearCoreColor = "#ffffff";
        public const string SpearHeadFire = "World_Proj_SpearHead_Fire", SpearHeadBolt = "World_Proj_SpearHead_Bolt";
        public const string ProjPrefix = "World_Proj_";
        public const int MinTier = 1, MaxTier = 8;
        /// <summary>회전 장난감 수·궤도(정본 rebuildUnits L1842 3개, syncScene L2765 반지름 1.5, 높이 1.0 ± sin(2a)·0.15).</summary>
        public const int BladeCount = 3;
        public const double BladeOrbitR = 1.5, BladeY = 1.0, BladeBob = 0.15;
        /// <summary>`R.spinA` 증가 속도 rad/게임초(정본 updRun L2141 orb ×4, L2147 spin ×5).</summary>
        public const double OrbSpinRate = 4, SpinSpinRate = 5;

        static readonly int[] s_bladeTiers = { 1, 6, 1 };
        static readonly string[] s_defaultNames = BuildDefaultNames();

        static string[] BuildDefaultNames()
        {
            var a = new string[MaxTier + 1];
            for (int t = MinTier; t <= MaxTier; t++) a[t] = ProjPrefix + t;
            return a;
        }

        /// <summary>정본 `b.t || 1`: 0 이하는 1. 8 초과는 8로 자른다([창작] drawProjShape에 분기 없음).</summary>
        public static int ClampTier(int t) => t < MinTier ? MinTier : t > MaxTier ? MaxTier : t;

        /// <summary>등급 → 그림 이름 표(인덱스 1..8). `103_item.projSprite`가 있으면 그 이름, 없으면 `World_Proj_{t}`.</summary>
        public static string[] NamesByTier(MvTables tables)
        {
            var a = (string[])s_defaultNames.Clone();
            var items = tables?.Item?.Sorted;
            if (items == null) return a;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null || it.tier < MinTier || it.tier > MaxTier || string.IsNullOrEmpty(it.projSprite)) continue;
                a[it.tier] = it.projSprite;
            }
            return a;
        }

        /// <summary>던진 장난감 그림 이름(등급은 <see cref="ClampTier"/>). 표가 없으면 기본 이름.</summary>
        public static string ProjSpriteName(int tier, string[] byTier)
        {
            int t = ClampTier(tier);
            string s = byTier != null && t < byTier.Length ? byTier[t] : null;
            return string.IsNullOrEmpty(s) ? s_defaultNames[t] : s;
        }

        /// <summary>회전 장난감 i번째 등급: 불이면 8, 아니면 [1, 6, 1][i % 3](L3800).</summary>
        public static int BladeTier(int i, bool fire) => fire ? 8 : s_bladeTiers[((i % 3) + 3) % 3];

        public static bool IsFire(string el) => el == "fire";

        /// <summary>창 머리 그림: 불 `_Fire`, 그 외 `_Bolt`(03 L395).</summary>
        public static string SpearHeadName(string el) => IsFire(el) ? SpearHeadFire : SpearHeadBolt;

        /// <summary>창 색 c: 불 `#ffb03a`, 그 외 `#ff3a4a`(L3802).</summary>
        public static string SpearColor(string el) => IsFire(el) ? SpearColorFire : SpearColorOther;

        /// <summary>창 꼬리 끝(정본 `pj(wp.x, wp.y, -(wp.z - 2.8))` = 게임 z − 2.8, 같은 x·y).</summary>
        public static void SpearTailPoint(double x, double y, double z, out double tx, out double ty, out double tz)
        {
            tx = x;
            ty = y;
            tz = z - SpearTail;
        }

        /// <summary>던진 장난감 로컬 z 회전(rad) = −(실시간ms/110 + b.sz).</summary>
        public static double ProjAngle(double nowSec, double sz) => -(nowSec * 1000.0 / ProjSpinMs + sz);

        /// <summary>회전 장난감 로컬 z 회전(rad) = −(실시간ms/180 + i·2).</summary>
        public static double BladeAngle(double nowSec, int i) => -(nowSec * 1000.0 / BladeSpinMs + i * BladePhaseStep);

        /// <summary>rad → 도(−180~180). 실시간이 커져도 float 정밀도를 잃지 않게 double에서 먼저 2π로 접는다.</summary>
        public static float ToDegrees(double rad) => (float)(Math.IEEERemainder(rad, Math.PI * 2) * (180.0 / Math.PI));

        /// <summary>회전 장난감 i번째 위치(정본 syncScene L2765): a = spinA + i·2π/3, (px + cos a·1.5, 1.0 + sin 2a·0.15, pz + sin a·1.5).</summary>
        public static void BladePos(double spinA, int i, double px, double pz, out double x, out double y, out double z)
        {
            double a = spinA + i * Math.PI * 2 / BladeCount;
            x = px + Math.Cos(a) * BladeOrbitR;
            y = BladeY + Math.Sin(a * 2) * BladeBob;
            z = pz + Math.Sin(a) * BladeOrbitR;
        }

        /// <summary>얻은 카드에 효과 type이 있는가(표 `110_card.effectId` → `112_cardEffect.type`).</summary>
        public static bool HasEffect(RunState s, MvTables tables, CardEffectType type)
        {
            if (s == null || tables == null) return false;
            var order = s.CardOrder;
            for (int i = 0; i < order.Count; i++)
            {
                int id = order[i];
                if (s.CardN(id) <= 0) continue;
                var c = tables.Card.Get(id);
                var e = c != null ? tables.CardEffect.Get(c.effectId) : null;
                if (e != null && e.type == type) return true;
            }
            return false;
        }

        /// <summary>`R.spinA` 1 게임초당 증가(orb 4 + spin 5, 정본은 두 블록이 각각 더한다).</summary>
        public static double SpinRate(bool orb, bool spin) => (orb ? OrbSpinRate : 0) + (spin ? SpinSpinRate : 0);
    }
}
