using jjevol;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// <c>UIHud_Run</c> 컨트롤러(05 §5.2, 1차: 재화 칩·계단 상자·계단 게이지·무기 카드).
    /// 매 프레임 <see cref="IRunHudSource"/>를 읽고 정본처럼 값이 바뀐 항목만 겉(<see cref="IRunHudView"/>)에 넘긴다.
    /// 연료 게이지·등급 미터·가호 카드·HAND는 2차.
    /// </summary>
    public class RunHud : UIHudBase
    {
        /// <summary>겉 컴포넌트(<see cref="IRunHudView"/> 구현). 비면 같은 오브젝트에서 찾는다.</summary>
        [SerializeField] MonoBehaviour view;

        IRunHudView _view;
        IRunHudSource _source;
        RunHudState _last;
        bool _drawn;

        public IRunHudSource Source => _source;

        public IRunHudView View
        {
            get
            {
                if (_view == null) _view = view as IRunHudView ?? GetComponent<IRunHudView>();
                return _view;
            }
        }

        /// <summary>겉을 코드로 바꾼다(시험·겉 교체).</summary>
        public void SetView(IRunHudView next)
        {
            _view = next;
            _drawn = false;
        }

        /// <summary>런 시작에 값 출처를 묶는다. 다음 갱신에서 모든 항목을 다시 그린다.</summary>
        public void Bind(IRunHudSource source)
        {
            _source = source;
            _drawn = false;
            if (IsVisible) Tick();
        }

        protected virtual void Awake()
        {
            MvUi.EnsureLayer(gameObject, MvUiLayer.Hud, false); // HUD 전체는 누를 수 없다(드래그가 아래로 지나감)
        }

        protected override void _DrawContents()
        {
            _drawn = false;
            Tick();
        }

        void LateUpdate() => Tick();

        /// <summary>한 번 읽고 바뀐 항목만 그린다.</summary>
        public void Tick()
        {
            var v = View;
            if (v == null || _source == null || !_source.TryRead(out var s)) return;
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
            if (all || s.WeaponTier != _last.WeaponTier || s.WeaponDmg != _last.WeaponDmg || s.WeaponName != _last.WeaponName || s.LookName != _last.LookName)
                v.SetWeapon(s.WeaponTier, s.WeaponIcon, s.WeaponName ?? "", WeaponDmgText(s.WeaponDmg, s.LookName), MvUi.Hex(s.WeaponGradeColor));

            _last = s;
            _drawn = true;
        }

        // 문구: 키 자리 ui:ui.hud.* (LocalizationManager.GetLoc 교체 지점, 05 §5.2.2~5.2.6)
        public static string LevelText(int level) => "Level " + level;                                  // ui.hud.level
        public static string SoulsText(int have, int need) => have + "/" + need;                       // fmt 아님
        public static string GaugeLvText(int lv) => "Lv " + lv;                                         // ui.hud.gaugeLv
        public static string GaugePerText(int lv) => "+" + lv;                                          // ui.hud.gaugePer 「칸당 」 + 「+{0}」

        public static string FloorText(RunHudState s)
        {
            string head = string.IsNullOrEmpty(s.AreaName) ? "Area " + s.AreaNo : s.AreaName + " · Area " + s.AreaNo; // ui.hud.floor
            string tail = s.Floor > 0 ? "골목 " + s.Floor : "플레이 구간";                                         // ui.hud.floorN / floorPlay
            return head + "\n" + tail;
        }

        public static string WeaponDmgText(double dmg, string look) =>                                  // ui.hud.wDmg
            string.IsNullOrEmpty(look) ? "피해 " + MvUiFormat.Fmt(dmg) : "피해 " + MvUiFormat.Fmt(dmg) + " · " + look;
    }
}
