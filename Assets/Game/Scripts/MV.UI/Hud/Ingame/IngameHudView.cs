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
        [SerializeField] TMP_Text levelText;
        /// <summary>배속 토글 버튼(<c>Toggle PlaySpeed</c>).</summary>
        [SerializeField] Button speedButton;
        /// <summary>디자이너가 1x 자식에 둔 Toggle. 클릭을 가로채지 않게 끄고 버튼 하나로 받는다.</summary>
        [SerializeField] Toggle speedToggle;
        /// <summary>배속 상태 자식(1x, 2x …). i번째 = 배속 i+1. 토글 상태 수 = 이 개수.</summary>
        [SerializeField] GameObject[] speedStates;
        /// <summary>재화 칸(<c>Slot_Currency</c>): 츄르·캣닢(디자이너가 둔 두 칸).</summary>
        [SerializeField] RectTransform goldSlot;
        [SerializeField] RectTransform diaSlot;

        [Header("Mid")]
        [SerializeField] GameObject mid;
        /// <summary>장착 무기 칸(<c>Equip/Slot_Weapon</c>).</summary>
        [SerializeField] RectTransform equipSlot;
        /// <summary>쌓이는 무기(<c>Stack</c>, 자식 <c>Slot_Weapon_Stack</c>).</summary>
        [SerializeField] RectTransform stack;
        [SerializeField] AddressableImage stairGrade;
        /// <summary>「+{칸당 계단}」.</summary>
        [SerializeField] TMP_Text stairValue;
        /// <summary>「x{계단 수}」.</summary>
        [SerializeField] TMP_Text stairCount;
        [SerializeField] Slider gauge;
        [SerializeField] AddressableImage gaugeGrade;
        [SerializeField] TMP_Text gaugeLevel;
        [SerializeField] AddressableImage gaugeNextGrade;
        [SerializeField] TMP_Text gaugeNextLevel;
        /// <summary>얻은 카드 자리(<c>Card Pos</c>, 자식 <c>Holder_Card</c>).</summary>
        [SerializeField] RectTransform cardPos;

        [Header("ClimbStair")]
        [SerializeField] GameObject climbStair;
        [SerializeField] GameObject gradeSection;
        [SerializeField] RectTransform holderOut;
        [SerializeField] RectTransform holderPrev;
        [SerializeField] RectTransform holderCurrent;
        [SerializeField] RectTransform holderNext;
        [SerializeField] RectTransform holderIn;
        [SerializeField] RectTransform skillSection;

        [Header("연출 시간(초, 1배속 기준 — 계단·롤링은 배속만큼 빨라진다)")]
        [SerializeField] float climbShiftSec = 0.3f;
        [SerializeField] float weaponAddSec = 0.15f;
        [SerializeField] float weaponMergeSec = 0.2f;
        [SerializeField] float rollStepSec = 0.06f;
        [SerializeField] float popScale = 1.15f;

        // 문구: 디자이너 [HUD] 인게임 서식(키 자리 ui.hud.*)
        public const string StairValueFormat = "+{0}"; // ui.hud.stairPer
        public const string StairCountFormat = "x{0}"; // ui.hud.stairCount

        Func<RunState> _state;
        RunState _run;
        MvTables T => _run?.Tables;
        int _speed = 1;
        bool _ready;

        TMP_Text _gold, _dia;
        HudWeaponSlot _equip, _stackTemplate;
        readonly List<HudWeaponSlot> _stackSlots = new List<HudWeaponSlot>();
        int[] _hand;
        bool _handBusy;

        HudClimbCard _climbTemplate;
        readonly List<HudClimbCard> _climbPool = new List<HudClimbCard>();
        readonly List<HudClimbCard> _climbCards = new List<HudClimbCard>();
        ClimbState _climb;
        int _climbCur = int.MinValue;

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
        public HudWeaponSlot Equip { get { Init(); return _equip; } }
        public IReadOnlyList<HudWeaponSlot> StackSlots { get { Init(); return _stackSlots; } }
        public IReadOnlyList<HudClimbCard> ClimbCards => _climbCards;
        public HudSkillCard SkillCard { get { Init(); return _skill; } }
        public IReadOnlyList<HudSkillCard> PosCards => _posCards;
        public int SpeedStateCount => speedStates?.Length ?? 0;
        public int ShownSpeedState { get { if (speedStates != null) for (int i = 0; i < speedStates.Length; i++) if (speedStates[i] && speedStates[i].activeSelf) return i; return -1; } }
        public string LevelText => levelText ? levelText.text : null;
        public string GoldText { get { Init(); return _gold ? _gold.text : null; } }
        public string DiaText { get { Init(); return _dia ? _dia.text : null; } }
        public string StairValueText => stairValue ? stairValue.text : null;
        public string StairCountText => stairCount ? stairCount.text : null;
        public string GaugeGradeSprite => gaugeGrade ? gaugeGrade.spriteName : null;
        public bool HandBusy => _handBusy;

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            // HUD는 버튼만 누를 수 있다: 버튼(Selectable) 아래 그림만 raycastTarget을 남긴다(드래그가 아래로 지나감).
            foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = g.GetComponentInParent<Selectable>(true) != null;
            if (speedToggle) speedToggle.enabled = false;
            if (speedButton) speedButton.onClick.AddListener(OnSpeedClick);

            _gold = goldSlot ? goldSlot.GetComponentInChildren<TMP_Text>(true) : null;
            _dia = diaSlot ? diaSlot.GetComponentInChildren<TMP_Text>(true) : null;
            _equip = equipSlot ? equipSlot.GetComponentInChildren<HudWeaponSlot>(true) : null;
            if (stack)
                foreach (Transform c in stack)
                {
                    var slot = c.GetComponent<HudWeaponSlot>();
                    if (!slot) continue;
                    if (!_stackTemplate) _stackTemplate = slot;
                    _stackSlots.Add(slot);
                    slot.gameObject.SetActive(false);
                }

            foreach (var h in new[] { holderPrev, holderCurrent })
            {
                var card = h ? h.GetComponentInChildren<HudClimbCard>(true) : null;
                if (!card) continue;
                if (!_climbTemplate || h == holderCurrent) _climbTemplate = card;
                // Holder에 늘어난 크기를 고정하고(가운데 기준) 구간 공통 부모로 옮긴다. 자리·크기는 Holder 사이 보간(Fit)으로 정한다.
                var rt = card.Rect;
                var size = rt.rect.size;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
                if (size.x > 1 && size.y > 1) rt.sizeDelta = size;
                card.transform.SetParent(gradeSection ? gradeSection.transform : h, false);
                card.gameObject.SetActive(false);
                _climbPool.Add(card);
            }
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

        public void SetLevel(string text) { if (levelText) levelText.text = text; }
        public void SetGold(string text) { Init(); if (_gold) _gold.text = text; }
        public void SetDia(string text) { Init(); if (_dia) _dia.text = text; }
        public void SetStairs(string text, bool flying) { if (stairCount) stairCount.text = string.Format(StairCountFormat, text); }

        /// <summary>디자이너 화면에 칸이 없다(Area 이름·생선·머지 안내·「n / N 칸」). 값은 받기만 한다.</summary>
        public void SetFloor(string text) { }
        public void SetSouls(string text) { }
        public void SetMergeHint(string text) { }
        public void SetClimbCount(string text, float alpha) { }
        /// <summary>게이지는 원값(<see cref="SetState"/>)으로 그린다.</summary>
        public void SetGauge(string levelText, string perText, float fill01) { }

        /// <summary>런 상태가 없을 때(시험 출처)만 장착 칸을 직접 채운다. 런 중에는 손패 연출이 그린다.</summary>
        public void SetWeapon(int tier, string iconSprite, string name, string damageText, Color gradeColor)
        {
            Init();
            if (_run == null && _equip) _equip.Set(null, tier, 1);
        }

        // ---------------- IRunHudRawView(원값) ----------------

        public void SetState(in RunHudState s)
        {
            Init();
            _speed = Math.Max(1, s.TimeScale);
            int shown = MvRunSpeed.StateIndex(_speed, SpeedStateCount);
            if (speedStates != null) for (int i = 0; i < speedStates.Length; i++) if (speedStates[i]) speedStates[i].SetActive(i == shown);

            int lv = s.GaugeLv;
            var cap = T != null ? RunFormulas.GaugeRow(T) : null;
            int next = cap != null ? Math.Min(cap.cap, lv + 1) : lv + 1;
            if (gauge) gauge.normalizedValue = Mathf.Clamp01(s.GaugeFill);
            HudImage.Set(stairGrade, HudGrade.Frame(T, lv));
            HudImage.Set(gaugeGrade, HudGrade.Frame(T, lv));
            HudImage.Set(gaugeNextGrade, HudGrade.Frame(T, next));
            if (stairValue) stairValue.text = string.Format(StairValueFormat, lv);
            if (gaugeLevel) gaugeLevel.text = lv.ToString();
            if (gaugeNextLevel) gaugeNextLevel.text = next.ToString();
        }

        void OnSpeedClick() => MvRunSpeed.Set?.Invoke(MvRunSpeed.Next(_speed, SpeedStateCount));

        /// <summary>시험: 배속 토글을 누른 것처럼.</summary>
        public void ClickSpeed() => OnSpeedClick();

        // ---------------- IRunHudStateView(런 상태) ----------------

        public void BindRun(Func<RunState> state)
        {
            Init();
            _state = state;
            _run = null;
        }

        public void TickRun(float dt)
        {
            Init();
            var s = _state?.Invoke();
            if (s != _run) { _run = s; ResetRun(); }
            if (s == null) return;
            TickHand(s);
            TickClimb(s);
            TickRoulette(s);
            TickCardPos(s);
            bool rolling = s.Roulette != null && _focus >= 0;
            bool climbing = s.Phase == RunPhase.Climb && s.Climb != null;
            bool anyFly = _flown.Count > 0;
            if (climbStair) climbStair.SetActive(climbing || rolling);
            if (gradeSection) gradeSection.SetActive(climbing);
            if (skillSection) skillSection.gameObject.SetActive(climbing || rolling);
            // 디자이너 시퀀스: 계단·롤링 동안 Top·Mid를 끄고, 카드가 Card Pos로 날기 시작하면 다시 켠다.
            bool hide = climbing || (s.Roulette != null && !anyFly);
            if (top) top.SetActive(!hide);
            if (mid) mid.SetActive(!hide);
        }

        void ResetRun()
        {
            StopAllCoroutines();
            _handBusy = false;
            _hand = null;
            _climb = null;
            _climbCur = int.MinValue;
            foreach (var c in _climbCards) Release(c);
            _climbCards.Clear();
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

        // ---------------- 지팡이 손패(머지 연출) ----------------

        double DmgMul(RunState s) => s.Tables != null ? RunFormulas.CardMul(s, CardEffectType.DmgMul) : 1;

        void TickHand(RunState s)
        {
            if (_handBusy || s.W == null) return;
            if (_hand == null || _hand.Length != s.W.Length)
            {
                _hand = (int[])s.W.Clone();
                RedrawHand(s);
                return;
            }
            int mergeN = T != null ? T.RunConfig.I(RunConfigKeys.MERGE_N) : 0;
            var steps = HudWeaponHand.Steps(_hand, s.W, mergeN, s.Bt);
            if (steps != null && steps.Count == 0) return;
            if (steps == null || !isActiveAndEnabled || !Animate)
            {
                _hand = (int[])s.W.Clone();
                RedrawHand(s);
                return;
            }
            StartCoroutine(PlayHand(s, steps, mergeN));
        }

        IEnumerator PlayHand(RunState s, List<HudHandStep> steps, int mergeN)
        {
            _handBusy = true;
            foreach (var step in steps)
            {
                if (!step.Merge)
                {
                    _hand[step.Tier]++;
                    RedrawHand(s);
                    yield return Pop(FindSlots(step.Tier, 1), weaponAddSec);
                    continue;
                }
                // 같은 단계 MERGE_N장이 모여(줄어들며) 윗단계 1장이 튀어나온다.
                var gather = FindSlots(step.Tier, mergeN);
                var seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);
                foreach (var slot in gather) seq.Join(slot.transform.DOScale(0f, weaponMergeSec * .5f).SetEase(Ease.InBack));
                yield return seq.WaitForCompletion();
                HudWeaponHand.ApplyMerge(_hand, step.Tier, mergeN);
                RedrawHand(s);
                yield return Pop(FindSlots(step.Tier + 1, 1), weaponMergeSec);
            }
            _handBusy = false;
        }

        YieldInstruction Pop(List<HudWeaponSlot> slots, float sec)
        {
            if (slots.Count == 0) return null;
            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            foreach (var slot in slots)
            {
                var t = slot.transform;
                t.localScale = Vector3.zero;
                seq.Join(DOTween.Sequence()
                    .Append(t.DOScale(popScale, sec * .6f).SetEase(Ease.OutQuad))
                    .Append(t.DOScale(1f, sec * .4f).SetEase(Ease.InQuad)));
            }
            return seq.WaitForCompletion();
        }

        /// <summary>그 단계 칸 n개(스택 아래부터, 모자라면 장착 칸).</summary>
        List<HudWeaponSlot> FindSlots(int tier, int n)
        {
            var list = new List<HudWeaponSlot>();
            for (int i = _stackSlots.Count - 1; i >= 0 && list.Count < n; i--)
                if (_stackSlots[i].gameObject.activeSelf && _stackSlots[i].Tier == tier) list.Add(_stackSlots[i]);
            if (list.Count < n && _equip && _equip.Tier == tier) list.Add(_equip);
            return list;
        }

        void RedrawHand(RunState s)
        {
            double mul = DmgMul(s);
            if (_equip) { _equip.Set(T, HudWeaponHand.Equipped(_hand), mul); _equip.transform.localScale = Vector3.one; }
            var tiers = HudWeaponHand.Stack(_hand);
            while (_stackSlots.Count < tiers.Count && _stackTemplate)
                _stackSlots.Add(Instantiate(_stackTemplate, stack, false));
            for (int i = 0; i < _stackSlots.Count; i++)
            {
                var slot = _stackSlots[i];
                bool on = i < tiers.Count;
                slot.gameObject.SetActive(on);
                if (!on) continue;
                slot.Set(T, tiers[i], mul);
                slot.transform.localScale = Vector3.one;
            }
        }

        // ---------------- 계단 등급 카드(Out → Prev → Current → Next → In) ----------------

        void TickClimb(RunState s)
        {
            var c = s.Phase == RunPhase.Climb ? s.Climb : null;
            if (c == null) { _climb = null; return; }
            if (c != _climb) { _climb = c; _climbCur = int.MinValue; }
            int cur = HudGrade.ClimbCurrent(T, c.Tier, c.Rem, out float fill);
            if (cur < 0) return;
            if (cur != _climbCur)
            {
                ShiftClimb(cur, _climbCur == int.MinValue || cur < _climbCur);
                _climbCur = cur;
                if (_skill) _skill.ShowQuestion(T, cur);
            }
            foreach (var card in _climbCards)
                card.SetFill(card.Tier < cur ? 1 : card.Tier > cur ? 0 : fill);
        }

        RectTransform HolderFor(int slot) => slot switch
        {
            -2 => holderOut,
            -1 => holderPrev,
            0 => holderCurrent,
            1 => holderNext,
            _ => holderIn,
        };

        /// <summary>
        /// 등급이 오르면 카드가 한 칸씩 밀린다(Prev → Out으로 사라짐, Current → Prev, Next → Current, In에서 새 카드 → Next). 위치·크기는 Holder 사이 보간.
        /// snap이면(시작·카드 확정 뒤 다시 세기) 바로 놓는다.
        /// </summary>
        void ShiftClimb(int cur, bool snap)
        {
            int max = HudGrade.CardTierMax(T);
            float sec = climbShiftSec / Speed;
            if (snap)
            {
                foreach (var c in _climbCards) Release(c);
                _climbCards.Clear();
            }
            for (int i = _climbCards.Count - 1; i >= 0; i--)
            {
                var card = _climbCards[i];
                int slot = card.Tier - cur;
                if (slot >= -1) { Move(card.Rect, HolderFor(slot), sec, null); continue; }
                _climbCards.RemoveAt(i);
                Move(card.Rect, holderOut, sec, () => Release(card));
            }
            for (int tier = Math.Max(0, cur - 1); tier <= Math.Min(max, cur + 1); tier++)
            {
                if (_climbCards.Exists(c => c.Tier == tier)) continue;
                var card = Get();
                if (!card) break;
                card.Set(T, tier);
                _climbCards.Add(card);
                if (snap) { Place(card.Rect, HolderFor(tier - cur)); continue; }
                Place(card.Rect, holderIn);
                Move(card.Rect, HolderFor(tier - cur), sec, null);
            }
        }

        HudClimbCard Get()
        {
            foreach (var c in _climbPool)
                if (!c.gameObject.activeSelf && !_climbCards.Contains(c)) { c.gameObject.SetActive(true); return c; }
            if (!_climbTemplate) return null;
            var card = Instantiate(_climbTemplate, gradeSection ? gradeSection.transform : _climbTemplate.transform.parent, false);
            card.gameObject.SetActive(true);
            _climbPool.Add(card);
            return card;
        }

        void Release(HudClimbCard card)
        {
            if (!card) return;
            Kill(card.Rect);
            card.gameObject.SetActive(false);
        }

        /// <summary>card를 target 자리·크기로(같은 부모 공간에서 target 중심, 크기는 target에 맞춘 배율).</summary>
        static void Fit(RectTransform card, RectTransform target, out Vector3 localPos, out float scale)
        {
            var space = card.parent;
            localPos = space.InverseTransformPoint(target.TransformPoint(target.rect.center));
            var size = card.rect.size;
            Vector2 world = Vector2.Scale(target.rect.size, target.lossyScale);
            Vector2 unit = Vector2.Scale(size, space.lossyScale);
            scale = size.x > 1 && size.y > 1 && world.x > 1 && world.y > 1 ? Mathf.Min(world.x / unit.x, world.y / unit.y) : 1f;
        }

        static void Place(RectTransform card, RectTransform target)
        {
            if (!target) return;
            Kill(card);
            Fit(card, target, out var pos, out float scale);
            card.localPosition = pos;
            card.localScale = Vector3.one * scale;
        }

        static void Move(RectTransform card, RectTransform target, float sec, TweenCallback done)
        {
            if (!target) { done?.Invoke(); return; }
            Kill(card);
            Fit(card, target, out var pos, out float scale);
            if (sec <= 0 || !Animate) { card.localPosition = pos; card.localScale = Vector3.one * scale; done?.Invoke(); return; }
            DOTween.Sequence().SetUpdate(true).SetTarget(card)
                .Join(card.DOLocalMove(pos, sec).SetEase(Ease.OutCubic))
                .Join(card.DOScale(scale, sec).SetEase(Ease.OutCubic))
                .OnComplete(done);
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
            Move(clone.Rect, target.Rect, (float)Math.Max(0, k.FlyLeft), () => { if (clone) Discard(clone.gameObject); });
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
