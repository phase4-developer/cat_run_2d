using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 런 HUD 화면 코드(D-92 ③④). <see cref="IRunHudSource"/>를 읽고 정본처럼 값이 바뀐 항목만 <see cref="IRunHudView"/>에 넘긴다.
    /// Framework 화면 타입과 떨어진 순수 클래스라 컨트롤러(<see cref="RunHud"/>)와 겉을 따로 갈아 끼울 수 있다.
    /// </summary>
    public sealed class RunHudPresenter
    {
        RunHudState _last;
        bool _drawn;

        public IRunHudView View { get; private set; }
        public IRunHudSource Source { get; private set; }

        public void SetView(IRunHudView view)
        {
            View = view;
            _drawn = false;
        }

        public void Bind(IRunHudSource source)
        {
            Source = source;
            _drawn = false;
        }

        /// <summary>다음 <see cref="Tick"/>에서 모든 항목을 다시 그린다.</summary>
        public void Invalidate() => _drawn = false;

        /// <summary>한 번 읽고 바뀐 항목만 그린다. 그렸으면 true.</summary>
        public bool Tick()
        {
            var v = View;
            if (v == null || Source == null || !Source.TryRead(out var s)) return false;
            bool all = !_drawn;

            if (all || s.Level != _last.Level)
                v.SetLevel(LevelText(s.Level));
            if (all || s.AreaNo != _last.AreaNo || s.Floor != _last.Floor || s.AreaName != _last.AreaName)
                v.SetFloor(FloorText(s));
            if (all || s.Gold != _last.Gold)
                v.SetGold(MvUiFormat.Fmt(s.Gold));
            if (all || s.Souls != _last.Souls || s.NeedSouls != _last.NeedSouls)
                v.SetSouls(SoulsText(s.Souls, s.NeedSouls));
            if (all || s.Dia != _last.Dia)
                v.SetDia(s.Dia.ToString());
            if (all || s.Stairs != _last.Stairs || s.Flying != _last.Flying)
                v.SetStairs(s.Stairs.ToString(), s.Flying);
            if (all || s.GaugeLv != _last.GaugeLv || !Mathf.Approximately(s.GaugeFill, _last.GaugeFill))
                v.SetGauge(GaugeLvText(s.GaugeLv), GaugePerText(s.GaugeLv), s.GaugeFill);
            if (all || s.WeaponTier != _last.WeaponTier || s.WeaponDmg != _last.WeaponDmg || s.WeaponName != _last.WeaponName
                || s.LookName != _last.LookName || s.WeaponIcon != _last.WeaponIcon)
                v.SetWeapon(s.WeaponTier, s.WeaponIcon, s.WeaponName ?? "", WeaponDmgText(s.WeaponDmg, s.LookName), MvUi.Hex(s.WeaponGradeColor));
            if (all || s.MergeN != _last.MergeN)
                v.SetMergeHint(MergeHintText(s.MergeN));

            _last = s;
            _drawn = true;
            return true;
        }

        // 문구: 키 자리 ui:ui.hud.* (LocalizationManager.GetLoc 교체 지점, 05 §5.2.2~5.2.6)
        public static string LevelText(int level) => "Level " + level;            // ui.hud.level
        public static string SoulsText(int have, int need) => have + "/" + need; // fmt 아님
        public static string GaugeLvText(int lv) => "Lv " + lv;                   // ui.hud.gaugeLv
        public static string GaugePerText(int lv) => "칸당 +" + lv;               // ui.hud.gaugePer 「칸당 」 + 「+{0}」
        /// <summary>머지 안내(장수 = 300 MERGE_N). 0 이하면 빈 문자열.</summary>
        public static string MergeHintText(int mergeN) => mergeN > 0 ? "같은 무기 " + mergeN + "장 → 머지" : ""; // ui.hud.mergeHint

        public static string FloorText(RunHudState s)
        {
            string head = string.IsNullOrEmpty(s.AreaName) ? "Area " + s.AreaNo : s.AreaName + " · Area " + s.AreaNo; // ui.hud.floor
            string tail = s.Floor > 0 ? "골목 " + s.Floor : "플레이 구간";                                         // ui.hud.floorN / floorPlay
            return head + "\n" + tail;
        }

        public static string WeaponDmgText(double dmg, string look) => // ui.hud.wDmg
            string.IsNullOrEmpty(look) ? "피해 " + MvUiFormat.Fmt(dmg) : "피해 " + MvUiFormat.Fmt(dmg) + " · " + look;
    }
}
