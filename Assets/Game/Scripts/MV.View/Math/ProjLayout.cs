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
        public const int MinTier = 1;
        /// <summary>회전 장난감 높이 1.0 ± sin(2a)·0.15(정본 syncScene L2765). 개수·반지름·각속도는 표(<see cref="OrbConfig"/>).</summary>
        public const double BladeY = 1.0, BladeBob = 0.15;

        static readonly int[] s_bladeTiers = { 1, 6, 1 };

        /// <summary>
        /// 등급 → 그림 이름 표(인덱스 1..`103_item` 최고 등급, 0은 비움). 등급 수는 표에서 읽는다.
        /// `projSprite`가 빈 등급(또는 행이 없는 등급)은 바로 아래 등급 그림을 다시 쓴다(2026-11-11 사용자 결정). 표가 없으면 null.
        /// </summary>
        public static string[] NamesByTier(MvTables tables)
        {
            var items = tables?.Item?.Sorted;
            if (items == null) return null;
            int max = 0;
            for (int i = 0; i < items.Count; i++) if (items[i] != null && items[i].tier > max) max = items[i].tier;
            var a = new string[max + 1];
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null || it.tier < MinTier || string.IsNullOrEmpty(it.projSprite)) continue;
                a[it.tier] = it.projSprite;
            }
            for (int t = MinTier + 1; t <= max; t++) if (string.IsNullOrEmpty(a[t])) a[t] = a[t - 1];
            return a;
        }

        /// <summary>
        /// 정본 `b.t || 1`: 0 이하는 1. 표 최고 등급보다 높으면 최고 등급 그림(그림 수가 등급을 막지 않는다).
        /// <paramref name="byTier"/> = <see cref="NamesByTier"/>. 표가 없으면 1.
        /// </summary>
        public static int ArtTier(int tier, string[] byTier) => byTier == null || byTier.Length <= MinTier ? MinTier : CatLayout.ArtIndex(tier, byTier.Length - 1);

        /// <summary>던진 장난감 그림 이름(등급은 <see cref="ArtTier"/>). 표가 없으면 null(그리지 않음).</summary>
        public static string ProjSpriteName(int tier, string[] byTier)
        {
            if (byTier == null || byTier.Length <= MinTier) return null;
            return byTier[ArtTier(tier, byTier)];
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

        /// <summary>
        /// 회전 장난감 i번째 위치(정본 syncScene L2765): a = spinA + i·2π/n, (px + cos a·r, 1.0 + sin 2a·0.15, pz + sin a·r).
        /// n = ORB_COUNT, r = ORB_RADIUS(<see cref="OrbConfig"/>).
        /// </summary>
        public static void BladePos(double spinA, int i, in OrbConfig orb, double px, double pz, out double x, out double y, out double z)
        {
            double a = spinA + i * Math.PI * 2 / Math.Max(1, orb.Count);
            x = px + Math.Cos(a) * orb.Radius;
            y = BladeY + Math.Sin(a * 2) * BladeBob;
            z = pz + Math.Sin(a) * orb.Radius;
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

        /// <summary>`R.spinA` 1 게임초당 증가(orb ORB_ANG_SPEED + spin BLADE_ANG_SPEED, 정본은 두 블록이 각각 더한다).</summary>
        public static double SpinRate(bool orb, bool spin, in OrbConfig cfg) => (orb ? cfg.OrbAngSpeed : 0) + (spin ? cfg.BladeAngSpeed : 0);
    }

    /// <summary>
    /// 회전 장난감 표 값(`300_runConfig` ORB_COUNT·ORB_RADIUS·ORB_ANG_SPEED·BLADE_ANG_SPEED, 정본 rebuildUnits L1842·syncScene L2765·updRun L2141/L2147).
    /// 표를 읽은 뒤 한 번 만든다. 키가 없으면 예외(대체값 없음).
    /// </summary>
    public readonly struct OrbConfig
    {
        public readonly int Count;
        public readonly double Radius, OrbAngSpeed, BladeAngSpeed;

        public OrbConfig(int count, double radius, double orbAngSpeed, double bladeAngSpeed)
        {
            Count = count;
            Radius = radius;
            OrbAngSpeed = orbAngSpeed;
            BladeAngSpeed = bladeAngSpeed;
        }

        public static OrbConfig From(RunConfigTable t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return new OrbConfig(t.I(RunConfigKeys.ORB_COUNT), t.D(RunConfigKeys.ORB_RADIUS), t.D(RunConfigKeys.ORB_ANG_SPEED), t.D(RunConfigKeys.BLADE_ANG_SPEED));
        }
    }
}
