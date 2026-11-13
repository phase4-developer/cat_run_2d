using System;
using MV.Data;
using MV.Run;

namespace MV.UI
{
    /// <summary>
    /// 런 시뮬 상태(<see cref="RunState"/>)를 HUD 값으로 옮기는 기본 출처(05 §5.2). 게임 루프가 현재 상태와 저장된 생선을 넘긴다.
    /// </summary>
    public sealed class RunStateHudSource : IRunHudSource
    {
        readonly Func<RunState> _state;
        readonly Func<int> _savedSouls;

        /// <param name="state">현재 런 상태(없으면 null).</param>
        /// <param name="savedSouls">저장된 누적 생선 <c>save.souls</c>.</param>
        public RunStateHudSource(Func<RunState> state, Func<int> savedSouls)
        {
            _state = state;
            _savedSouls = savedSouls;
        }

        /// <summary>현재 런 상태(가호 카드 화면이 직접 읽는다).</summary>
        public RunState Current() => _state?.Invoke();

        public bool TryRead(out RunHudState h)
        {
            h = default;
            RunState s = _state?.Invoke();
            if (s == null) return false;
            MvTables t = s.Tables;

            h.Level = s.Level;
            h.AreaNo = s.Area + 1;
            h.Floor = s.Floor;
            h.Gold = s.Gold;
            h.Souls = (_savedSouls?.Invoke() ?? 0) + s.Souls;
            h.Dia = s.Dia;

            AreaData area = null;
            if (t != null) foreach (var a in t.Area.Datas) if (a.areaNo == h.AreaNo) { area = a; break; }
            h.NeedSouls = area?.needSouls ?? 0;
            h.AreaName = area != null ? MvUiText.Name(area.id, "") : "";

            var climb = s.Climb;
            if (climb != null)
            {
                h.Stairs = Math.Max(0, climb.N - climb.Shown);
                h.Flying = s.Phase == RunPhase.Climb;
                if (h.Flying)
                {
                    h.ClimbN = climb.N;
                    h.ClimbShown = Math.Min(climb.N, climb.Shown);
                    h.ClimbAlpha = ClimbAlpha(t, climb.Time);
                }
            }
            else h.Stairs = s.Stairs;

            h.GaugeLv = s.GaugeLv;
            h.MergeN = t != null ? t.RunConfig.I(RunConfigKeys.MERGE_N) : 0;
            h.GaugeFill = GaugeFill(s);

            int tier = s.EqTier();
            h.WeaponTier = tier;
            ItemData item = null;
            if (t != null) foreach (var it in t.Item.Datas) if (it.tier == tier) { item = it; break; }
            if (item != null)
            {
                h.WeaponName = MvUiText.Name(item.id, "무기 " + tier);
                h.WeaponIcon = item.icon;
                h.WeaponGradeColor = item.gradeCol;
                h.LookName = MvUiText.Get("ui", item.lookName, "");
                h.WeaponDmg = item.dmg * (t != null ? RunFormulas.CardMul(s, CardEffectType.DmgMul) : 1);
            }
            else h.WeaponName = "무기 " + tier;
            return true;
        }

        /// <summary>새 HTML 「n / N 칸」은 옆 시점 돌기 e(320 side.turnSec, smooth)가 ClimbTextFrom을 넘으면 ClimbTextRate 배로 나타난다.</summary>
        public const double ClimbTextFrom = 0.6, ClimbTextRate = 3;

        /// <summary>오르기 시작부터 time초 뒤 글자 알파. 카메라 돌기는 오르기와 함께 0에서 시작한다(<c>ORB.k += dt/dur</c>).</summary>
        public static float ClimbAlpha(MvTables t, double time)
        {
            var side = t?.Camera.ByKey("side");
            double turn = side != null ? side.turnSec : 0;
            double k = turn > 0 ? Math.Max(0, Math.Min(1, time / turn)) : 1, e = k * k * (3 - 2 * k);
            return e > ClimbTextFrom ? (float)Math.Min(1, (e - ClimbTextFrom) * ClimbTextRate) : 0f;
        }

        static float GaugeFill(RunState s)
        {
            // 게이지 최대 Lv = 107_chargeGate[Well].cap(옛 코드 상수 8).
            var g = s.Tables != null ? RunFormulas.GaugeRow(s.Tables) : null;
            if (g != null && s.GaugeLv >= g.cap) return 1f;
            double need = s.Tables != null ? RunFormulas.GaugeNeed(s, s.GaugeLv) : 0;
            if (need <= 0 || double.IsNaN(need)) return 0f;
            return (float)Math.Max(0, Math.Min(1, s.GaugeFill / need));
        }
    }
}
