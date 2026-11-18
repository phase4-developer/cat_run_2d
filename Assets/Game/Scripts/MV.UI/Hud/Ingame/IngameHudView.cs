using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using jjevol;
using MV.Data;
using MV.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 <c>IngameScreen</c>(Art/UI/Prefab/UIScreen)을 런 HUD 겉으로 쓰는 연결(MV-UI2-HUD, [HUD] 인게임).
    /// <c>UIHud_Run</c>(<see cref="RunHud"/>, UIHudBase)이 이 프리팹을 자식으로 두고, 프레젠터 값(<see cref="IRunHudView"/>·<see cref="IRunHudRawView"/>)과
    /// 런 상태(<see cref="IRunHudStateView"/>: 지팡이 머지·계단 등급 카드·스킬 롤링·얻은 카드)를 이 컴포넌트가 디자이너 계층에 넣는다.
    /// 겉(배치·그림·크기)은 프리팹이 정하고 코드는 값·켜고 끄기·위치 보간만 한다. 시뮬 값은 읽기만 한다(연출이 게임 데이터를 다시 바꾸지 않는다).
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
        /// <summary>얻은 카드 자리(<c>Card Pos</c>, 자식 <c>Holder_Card</c>).</summary>
        [SerializeField] RectTransform cardPos;

        [Header("ClimbStair")]
        [SerializeField] GameObject climbStair;
        /// <summary>계단 등급 카드 섹션(<c>Grade Step Section</c>, MV-UI3-HUD6).</summary>
        [SerializeField] GradeStepSection gradeStep;
        [SerializeField] RectTransform skillSection;

        [Header("연출 시간(초, 1배속 기준 — 계단·롤링은 배속만큼 빨라진다)")]
        [SerializeField] float rollStepSec = 0.06f;

        Func<RunState> _state;
        RunState _run;
        MvTables T => _run?.Tables;
        bool _ready;

        HudSkillCard _skill;
        RouletteState _rl;
        int _focus = -1, _focusFlips;
        Tween _roll;
        readonly HashSet<int> _flown = new HashSet<int>();
        readonly List<GameObject> _flies = new List<GameObject>();
        readonly List<string> _faces = new List<string>();

        RectTransform _holderTemplate;
        readonly List<HudSkillCard> _posCards = new List<HudSkillCard>();
        int _posStamp = -1;

        // ---- 시험·QA ----
        public WeaponSection Weapon => weapon;
        public GradeStepSection GradeStep => gradeStep;
        public IReadOnlyList<HudClimbCard> ClimbCards => gradeStep ? gradeStep.Cards : (IReadOnlyList<HudClimbCard>)Array.Empty<HudClimbCard>();
        public HudSkillCard SkillCard { get { Init(); return _skill; } }
        public IReadOnlyList<HudSkillCard> PosCards => _posCards;
        public TopBarSection TopBar => topBar;
        public StairSection Stair => stair;

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            // HUD는 버튼만 누를 수 있다: 버튼(Selectable) 아래 그림만 raycastTarget을 남긴다(드래그가 아래로 지나감).
            foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = g.GetComponentInParent<Selectable>(true) != null;
            if (topBar) topBar.BindTables(MvTables.Current);

            if (gradeStep) gradeStep.CurrentChanged += OnClimbCurrent;
            _skill = skillSection ? skillSection.GetComponentInChildren<HudSkillCard>(true) : null;

            if (cardPos)
                foreach (Transform h in cardPos)
                {
                    var card = h.GetComponentInChildren<HudSkillCard>(true);
                    if (card && !_holderTemplate) _holderTemplate = (RectTransform)h;
                    if (card) { card.gameObject.SetActive(false); _posCards.Add(card); }
                }
            if (climbStair) climbStair.SetActive(false);
        }

        void OnDestroy()
        {
            _roll?.Kill();
            Kill(this);
        }

        /// <summary>연출은 재생 중에만(에디터 시험은 끝 상태로 바로 놓는다).</summary>
        static bool Animate => Application.isPlaying;

        static void Discard(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        static void Kill(object target)
        {
            if (Animate) DOTween.Kill(target);
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
            if (s != _run) { _run = s; ResetRun(); if (topBar && s?.Tables != null) topBar.BindTables(s.Tables); }
            if (weapon) weapon.Tick(dt);
            if (s == null) return;
            if (gradeStep) gradeStep.Tick(s, Speed);
            TickRoulette(s);
            TickCardPos(s);
            bool rolling = s.Roulette != null && _focus >= 0;
            bool climbing = s.Phase == RunPhase.Climb && s.Climb != null;
            bool anyFly = _flown.Count > 0;
            if (climbStair) climbStair.SetActive(climbing || rolling);
            if (gradeStep) gradeStep.gameObject.SetActive(climbing);
            if (skillSection) skillSection.gameObject.SetActive(climbing || rolling);
            // 디자이너 시퀀스: 계단·롤링 동안 Top·Mid를 끄고, 카드가 Card Pos로 날기 시작하면 다시 켠다.
            bool hide = climbing || (s.Roulette != null && !anyFly);
            if (top) top.SetActive(!hide);
            if (mid) mid.SetActive(!hide);
        }

        void ResetRun()
        {
            StopAllCoroutines();
            if (gradeStep) gradeStep.ResetRun();
            _rl = null;
            _focus = -1;
            _roll?.Kill();
            _flown.Clear();
            foreach (var f in _flies) if (f) Discard(f);
            _flies.Clear();
            _posStamp = -1;
            foreach (var c in _posCards) c.gameObject.SetActive(false);
            if (climbStair) climbStair.SetActive(false);
        }

        float Speed => _run?.Setup != null ? Math.Max(1, _run.Setup.TimeScale) : 1;

        /// <summary>계단 Current 등급이 바뀌면 스킬 카드를 그 등급 「?」로.</summary>
        void OnClimbCurrent(int cur)
        {
            if (_skill) _skill.ShowQuestion(T, cur);
        }

        // ---------------- 스킬 롤링 → 확정 → Card Pos ----------------

        void TickRoulette(RunState s)
        {
            var rl = s.Roulette;
            if (rl != _rl)
            {
                _rl = rl;
                _focus = -1;
                _flown.Clear();
                _roll?.Kill();
            }
            if (rl == null || !_skill) return;

            int f = -1;
            for (int i = 0; i < rl.Cards.Count; i++) if (!rl.Cards[i].Flying && !rl.Cards[i].Applied) { f = i; break; }
            if (f != _focus)
            {
                _focus = f;
                _roll?.Kill();
                if (f >= 0)
                {
                    var k = rl.Cards[f];
                    _focusFlips = k.Flips;
                    if (k.Chosen) _skill.ShowCard(T, k.Pick);
                    else _skill.BeginRoll(T, k.Tier, Faces(s, k.Tier));
                }
            }
            if (f >= 0)
            {
                var k = rl.Cards[f];
                if (!k.Chosen && k.Flips != _focusFlips) { _focusFlips = k.Flips; RollStep(s, k.Tier); }
                else if (k.Chosen && _skill.CardId != (k.Pick?.id ?? 0)) { _roll?.Kill(); _skill.ShowCard(T, k.Pick); }
            }
            for (int i = 0; i < rl.Cards.Count; i++)
            {
                var k = rl.Cards[i];
                if (!k.Flying || _flown.Contains(i)) continue;
                _flown.Add(i);
                Fly(s, k);
            }
        }

        /// <summary>롤링 후보 그림: 그 등급 카드 풀의 그림(UI 쪽 무작위, 시뮬 결과와 무관). 없으면 풀 전체.</summary>
        List<string> Faces(RunState s, int tier)
        {
            _faces.Clear();
            foreach (var c in s.CardPool) if (c.tier == tier && !string.IsNullOrEmpty(c.icon)) _faces.Add(c.icon);
            if (_faces.Count == 0) foreach (var c in s.CardPool) if (!string.IsNullOrEmpty(c.icon)) _faces.Add(c.icon);
            for (int i = _faces.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (_faces[i], _faces[j]) = (_faces[j], _faces[i]); }
            return _faces;
        }

        void RollStep(RunState s, int tier)
        {
            if (_roll != null && _roll.IsActive()) _roll.Complete(true);
            var faces = Faces(s, tier);
            string next = faces.Count > 0 ? faces[0] : null;
            float sec = rollStepSec / Speed;
            if (!Animate) { _skill.Advance(next); return; }
            float k = 0;
            _roll = DOTween.To(() => k, v => { k = v; _skill.SetStripOffset(v); }, 1f, sec).SetUpdate(true).SetTarget(this)
                .OnComplete(() => _skill.Advance(next));
        }

        /// <summary>확정 카드가 Card Pos 자리로 난다(시퀀스 6). 시간 = 시뮬 비행 시간(rlFlyMs, 실시간).</summary>
        void Fly(RunState s, RouletteCard k)
        {
            if (!_skill || k.Pick == null) return;
            int index = s.CardOrder.IndexOf(k.Pick.id);
            if (index < 0) index = s.CardOrder.Count;
            var target = PosCard(index);
            var clone = Instantiate(_skill, transform, true);
            clone.ShowCard(T, k.Pick);
            _flies.Add(clone.gameObject);
            if (!target) { Discard(clone.gameObject); return; }
            HudRectMotion.Move(clone.Rect, target.Rect, (float)Math.Max(0, k.FlyLeft), () => { if (clone) Discard(clone.gameObject); });
        }

        /// <summary>Card Pos index번째 카드(Holder_Card가 모자라면 첫 Holder를 복제).</summary>
        HudSkillCard PosCard(int index)
        {
            while (_posCards.Count <= index && _holderTemplate)
            {
                var h = Instantiate(_holderTemplate, cardPos, false);
                var card = h.GetComponentInChildren<HudSkillCard>(true);
                if (!card) break;
                card.gameObject.SetActive(false);
                _posCards.Add(card);
            }
            return index < _posCards.Count ? _posCards[index] : null;
        }

        void TickCardPos(RunState s)
        {
            int stamp = s.CardOrder.Count;
            foreach (var n in s.Cards.Values) stamp = stamp * 31 + n;
            if (stamp == _posStamp) return;
            _posStamp = stamp;
            for (int i = 0; i < s.CardOrder.Count; i++)
            {
                var card = PosCard(i);
                if (!card) break;
                card.gameObject.SetActive(true);
                card.ShowCard(T, T?.Card.Get(s.CardOrder[i]));
            }
            for (int i = s.CardOrder.Count; i < _posCards.Count; i++) _posCards[i].gameObject.SetActive(false);
        }
    }
}
