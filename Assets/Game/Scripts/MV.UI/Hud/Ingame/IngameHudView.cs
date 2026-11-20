using System;
using System.Collections.Generic;
using MV.Data;
using MV.Run;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 <c>IngameScreen</c>(Art/UI/Prefab/UIScreen)을 런 HUD 겉으로 쓰는 화면 조합(MV-UI2-HUD → MV-UI3-HUD4~7 섹션 분리).
    /// <c>UIHud_Run</c>(<see cref="RunHud"/>, UIHudBase)이 이 프리팹을 자식으로 두고, 프레젠터 값(<see cref="IRunHudView"/>·<see cref="IRunHudRawView"/>)과
    /// 런 상태(<see cref="IRunHudStateView"/>)를 받는다. 이 컴포넌트는 섹션에 값을 넘기고 단계(달리기·계단·룰렛)에 따라 켜고 끄기만 한다.
    /// 그리기와 연출은 섹션(<see cref="TopBarSection"/>·<see cref="StairSection"/>·<see cref="WeaponSection"/>·<see cref="GradeStepSection"/>·
    /// <see cref="SkillRollSection"/>·<see cref="CardPosSection"/>)과 공용 부품이 맡는다.
    /// </summary>
    public sealed class IngameHudView : MonoBehaviour, IRunHudView, IRunHudRawView, IRunHudStateView
    {
        [Header("Top")]
        [SerializeField] GameObject top;
        /// <summary>상단 줄 섹션(<c>Top</c>): 레벨·배속·재화(MV-UI3-HUD5).</summary>
        [SerializeField] TopBarSection topBar;

        [Header("Mid")]
        [SerializeField] GameObject mid;
        /// <summary>무기 섹션(<c>Mid/Weapon</c>): 장착 칸·손패 스택·날아오는 카드(MV-UI3-HUD4).</summary>
        [SerializeField] WeaponSection weapon;
        /// <summary>계단 섹션(<c>Mid/Stair</c>): 계단 수·게이지(MV-UI3-HUD5).</summary>
        [SerializeField] StairSection stair;
        /// <summary>얻은 카드 섹션(<c>Card Pos</c>, MV-UI3-HUD7).</summary>
        [SerializeField] CardPosSection cardPos;

        [Header("ClimbStair")]
        [SerializeField] GameObject climbStair;
        /// <summary>계단 등급 카드 섹션(<c>Grade Step Section</c>, MV-UI3-HUD6).</summary>
        [SerializeField] GradeStepSection gradeStep;
        /// <summary>스킬 롤링 섹션(<c>Skill Section</c>, MV-UI3-HUD7).</summary>
        [SerializeField] SkillRollSection skillRoll;

        Func<RunState> _state;
        RunState _run;
        // 첫 그리기(Bind 직후 Presenter.Tick)는 TickRun보다 먼저라 _run이 비어 있다. 그때도 표를 잡아 계단 등급 틀이 시작부터 보이게 한다.
        MvTables T => _run?.Tables ?? _state?.Invoke()?.Tables;
        bool _ready;

        // ---- 시험·QA ----
        public TopBarSection TopBar => topBar;
        public StairSection Stair => stair;
        public WeaponSection Weapon => weapon;
        public GradeStepSection GradeStep => gradeStep;
        public SkillRollSection SkillRoll => skillRoll;
        public CardPosSection CardPos => cardPos;
        public IReadOnlyList<HudClimbCard> ClimbCards => gradeStep ? gradeStep.Cards : (IReadOnlyList<HudClimbCard>)Array.Empty<HudClimbCard>();
        public HudSkillCard SkillCard => skillRoll ? skillRoll.Card : null;
        public IReadOnlyList<HudSkillCard> PosCards => cardPos ? cardPos.PosCards : (IReadOnlyList<HudSkillCard>)Array.Empty<HudSkillCard>();

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            // HUD는 버튼만 누를 수 있다: 버튼(Selectable) 아래 그림만 raycastTarget을 남긴다(드래그가 아래로 지나감).
            foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = g.GetComponentInParent<Selectable>(true) != null;
            if (topBar) topBar.BindTables(MvTables.Current);
            if (gradeStep) gradeStep.CurrentChanged += OnClimbCurrent;
            if (skillRoll) skillRoll.FlyStarted += OnFlyStarted;
            if (climbStair) climbStair.SetActive(false);
        }

        void OnDestroy()
        {
            if (gradeStep) gradeStep.CurrentChanged -= OnClimbCurrent;
            if (skillRoll) skillRoll.FlyStarted -= OnFlyStarted;
        }

        // ---------------- IRunHudView(프레젠터 문자열) ----------------

        public void SetLevel(string text) { if (topBar) topBar.SetLevel(text); }
        public void SetGold(string text) { Init(); if (topBar) topBar.SetGold(text); }
        public void SetDia(string text) { Init(); if (topBar) topBar.SetDia(text); }
        public void SetStairs(string text, bool flying) { if (stair) stair.SetStairs(text); }

        /// <summary>디자이너 화면에 칸이 없다(Area 이름·생선·머지 안내·「n / N 칸」). 값은 받기만 한다.</summary>
        public void SetFloor(string text) { }
        public void SetSouls(string text) { }
        public void SetMergeHint(string text) { }
        public void SetClimbCount(string text, float alpha) { }
        /// <summary>게이지는 원값(<see cref="SetState"/>)으로 그린다.</summary>
        public void SetGauge(string levelText, string perText, float fill01) { }

        /// <summary>장착 칸은 무기 섹션이 손패로 그린다(MV-UI3-HUD4). 값은 받기만 한다.</summary>
        public void SetWeapon(int tier, string iconSprite, string name, string damageText, Color gradeColor) { }

        // ---------------- IRunHudRawView(원값) ----------------

        public void SetState(in RunHudState s)
        {
            Init();
            if (topBar) topBar.SetSpeed(s.TimeScale);
            if (stair) stair.SetGauge(T, s.GaugeLv, s.GaugeFill);
        }

        // ---------------- IRunHudStateView(런 상태) ----------------

        public void BindRun(Func<RunState> state)
        {
            Init();
            _state = state;
            _run = null;
            if (weapon) weapon.BindRun(state);
        }

        public void TickRun(float dt)
        {
            Init();
            var s = _state?.Invoke();
            if (s != _run)
            {
                _run = s;
                ResetRun();
                // 계단 게이지 기준은 런 시작 값(유저 정보로 만든 RunState, RunLevelBuilder kegStart)으로 바로 잡는다(MV-UI3-STAIRFX5).
                // 안 그러면 런 첫 값 변경(첫 타격 레벨 업)이 「첫 그리기」로 바로 맞춰져 바퀴·등급 업 연출이 빠진다.
                if (stair && s != null) stair.SetGauge(s.Tables, s.GaugeLv, RunStateHudSource.GaugeFill(s));
                if (topBar && s?.Tables != null) topBar.BindTables(s.Tables);
            }
            if (weapon) weapon.Tick(dt);
            if (stair) stair.Tick(dt);
            if (s == null) return;
            float speed = s.Setup != null ? Math.Max(1, s.Setup.TimeScale) : 1;
            if (gradeStep) gradeStep.Tick(s, speed);
            if (skillRoll) skillRoll.Tick(s, speed);
            if (cardPos) cardPos.Tick(s);

            bool rolling = s.Roulette != null && skillRoll && skillRoll.Active;
            bool climbing = s.Phase == RunPhase.Climb && s.Climb != null;
            bool anyFly = skillRoll && skillRoll.AnyFlown;
            if (climbStair) climbStair.SetActive(climbing || rolling);
            if (gradeStep) gradeStep.gameObject.SetActive(climbing);
            if (skillRoll) skillRoll.gameObject.SetActive(climbing || rolling);
            // 디자이너 시퀀스(노션 1·6): 계단·롤링 동안 Top·Mid를 끄고, 카드가 Card Pos로 날기 시작하면 다시 켠다.
            bool hide = climbing || (s.Roulette != null && !anyFly);
            if (top) top.SetActive(!hide);
            if (mid) mid.SetActive(!hide);
        }

        void ResetRun()
        {
            if (stair) stair.ResetRun();
            if (gradeStep) gradeStep.ResetRun();
            if (skillRoll) skillRoll.ResetRun();
            if (cardPos) cardPos.ResetRun();
            if (climbStair) climbStair.SetActive(false);
        }

        /// <summary>계단 Current 등급이 바뀌면 스킬 카드를 그 등급 「?」로.</summary>
        void OnClimbCurrent(int cur)
        {
            if (skillRoll) skillRoll.ShowQuestion(T, cur);
        }

        /// <summary>확정 카드가 날기 시작하면 Card Pos가 그 자리로 날린다.</summary>
        void OnFlyStarted(RouletteCard k, HudSkillCard from)
        {
            if (cardPos) cardPos.Fly(_run, k, from);
        }
    }
}
