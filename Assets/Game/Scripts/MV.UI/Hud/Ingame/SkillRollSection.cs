using System;
using System.Collections.Generic;
using DG.Tweening;
using jjevol;
using MV.Data;
using MV.Run;
using UnityEngine;
using UnityEngine.Pool;

namespace MV.UI
{
    /// <summary>
    /// 스킬 롤링 섹션(<c>ClimbStair/Skill Section</c>, MV-UI3-HUD7 → MV-UI3-CLIMBFX, HUD_구조_설계안 3.3 SkillRollSection).
    /// 계단 중에는 Current 등급 「?」 카드 1장. 룰렛이 시작되면 룰렛 카드마다 <c>Holder_CardSkill</c>(Skill Section의 HorizontalLayout이 정렬)과
    /// 그 안의 <c>Card_Skill</c>을 하나씩 두고, 모든 카드가 동시에 그림을 넘기다(넘김 = 시뮬 Flips) 시뮬이 정한 카드에서 멈춘다.
    /// 멈추면 완료 연출(크기·플래시·반짝임), 시뮬이 비행을 시작하면(<see cref="RouletteCard.Flying"/>) <see cref="FlyStarted"/>로 알리고 그 칸 카드를 숨긴다.
    /// 결과·시각은 시뮬이 이미 정했고(<see cref="RouletteState"/>, 330 rlEndBase·rlEndStep·rlFlyDelay), 이 섹션은 보이는 것만 한다.
    /// 완료 연출 시간은 rlFlyDelay(확정 → 비행) 안에 끝나게 둔다.
    /// </summary>
    public sealed class SkillRollSection : UIBase
    {
        [Tooltip("롤링 카드(Card_Skill). 디자이너가 Skill Section/Holder_CardSkill 안에 둔 카드 1장 — 그 Holder가 복제 원본")]
        [SerializeField] HudSkillCard card;
        [Tooltip("그림 한 장 넘김 초(1배속 기준, 배속만큼 빨라진다)")]
        [SerializeField] float rollStepSec = 0.06f;

        [Header("롤링 완료 연출(임시 값, MV-UI3-CLIMBFX)")]
        [Tooltip("완료 연출 전체 초(1배속 기준). 330 rlFlyDelay보다 짧게")]
        [SerializeField] float completeSec = 0.45f;
        [Tooltip("완료 때 가장 커지는 배율")]
        [SerializeField] float completeScale = 1.2f;
        [Tooltip("전체 시간 중 커지는 구간 비율(나머지는 1로 돌아옴)")]
        [Range(0, 1)]
        [SerializeField] float completeUpRatio = 0.3f;
        [Tooltip("플래시 처음 밝기(0~1)")]
        [Range(0, 1)]
        [SerializeField] float completeFlash = 0.8f;

        /// <summary>룰렛 카드 1장의 자리(Holder + 카드)와 표시 상태.</summary>
        sealed class Slot
        {
            public RectTransform Holder;
            public HudSkillCard Card;
            public int Flips;
            public bool Chosen;
            public bool Flown;
            public Tween Roll;
        }

        RouletteState _rl;
        readonly List<Slot> _slots = new List<Slot>();
        readonly List<string> _faces = new List<string>();
        Slot _first;
        ObjectPool<Slot> _pool;
        bool _ready;

        /// <summary>확정 카드가 Card Pos로 날기 시작했다(룰렛 카드, 출발 카드).</summary>
        public event Action<RouletteCard, HudSkillCard> FlyStarted;

        /// <summary>디자이너가 둔 첫 카드(계단 중 「?」, 룰렛 첫 카드).</summary>
        public HudSkillCard Card => card;
        /// <summary>룰렛 카드 순서대로의 카드(룰렛이 없으면 비어 있다).</summary>
        public IReadOnlyList<HudSkillCard> RollCards
        {
            get
            {
                _cardsView.Clear();
                foreach (var s in _slots) _cardsView.Add(s.Card);
                return _cardsView;
            }
        }
        readonly List<HudSkillCard> _cardsView = new List<HudSkillCard>();

        /// <summary>아직 날지 않은 룰렛 카드가 있다(롤링·확정·완료 연출 중).</summary>
        public bool Active
        {
            get
            {
                foreach (var s in _slots) if (!s.Flown) return true;
                return false;
            }
        }
        /// <summary>날기 시작한 카드가 하나라도 있다(디자이너 시퀀스: 이때 Top·Mid를 다시 켠다).</summary>
        public bool AnyFlown
        {
            get
            {
                foreach (var s in _slots) if (s.Flown) return true;
                return false;
            }
        }

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            if (!card)
            {
                Debug.LogWarning("[SkillRollSection] card(Skill Section/Holder_CardSkill/Card_Skill)가 비어 있습니다: Assets/Game/Art/UI/Prefab/UIScreen/IngameScreen.prefab", this);
                return;
            }
            var holder = card.transform.parent as RectTransform;
            if (!holder || holder == transform) holder = (RectTransform)card.transform;
            _first = new Slot { Holder = holder, Card = card };
            var template = holder;
            _pool = new ObjectPool<Slot>(
                () =>
                {
                    var h = Instantiate(template, transform, false);
                    var c = h == template ? null : h.GetComponentInChildren<HudSkillCard>(true);
                    if (!c) c = h.GetComponent<HudSkillCard>();
                    return new Slot { Holder = h, Card = c };
                },
                s => { s.Holder.gameObject.SetActive(true); s.Holder.SetAsLastSibling(); },
                s => { Clear(s); s.Holder.gameObject.SetActive(false); },
                s => { if (s.Holder) Discard(s.Holder.gameObject); },
                collectionCheck: false);
        }

        static void Discard(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        static void Clear(Slot s)
        {
            s.Roll?.Kill();
            s.Roll = null;
            s.Flips = 0;
            s.Chosen = s.Flown = false;
            if (!s.Card) return;
            s.Card.ResetLook();
            s.Card.gameObject.SetActive(true);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var s in _slots) s.Roll?.Kill();
            _pool?.Dispose();
        }

        /// <summary>룰렛 자리를 비우고 첫 카드만 남긴다.</summary>
        void ClearSlots()
        {
            foreach (var s in _slots)
            {
                if (s == _first) Clear(s);
                else _pool.Release(s);
            }
            _slots.Clear();
        }

        public void ResetRun()
        {
            Init();
            _rl = null;
            _qTables = null;
            _qTier = -1;
            if (_first == null) return;
            ClearSlots();
        }

        MvTables _qTables;
        int _qTier = -1;

        /// <summary>계단 Current 등급 「?」(첫 카드 1장). 앞 룰렛 카드가 아직 남아 있으면 룰렛이 끝난 뒤 그린다.</summary>
        public void ShowQuestion(MvTables t, int tier)
        {
            Init();
            _qTables = t;
            _qTier = tier;
            if (_first == null || _rl != null) return;
            DrawQuestion();
        }

        void DrawQuestion()
        {
            if (_qTier < 0) return;
            ClearSlots();
            Clear(_first);
            _first.Holder.gameObject.SetActive(true);
            card.ShowQuestion(_qTables, _qTier);
        }

        public void Tick(RunState s, float speed)
        {
            Init();
            if (_first == null) return;
            var rl = s?.Roulette;
            if (rl != _rl)
            {
                _rl = rl;
                ClearSlots();
                if (rl != null) Begin(s, rl);
                else if (s != null && s.Phase == RunPhase.Climb) DrawQuestion();   // 룰렛이 끝났는데 다음 계단 중이면 「?」
            }
            if (rl == null) return;
            var t = s.Tables;
            for (int i = 0; i < rl.Cards.Count && i < _slots.Count; i++)
            {
                var k = rl.Cards[i];
                var slot = _slots[i];
                if (slot.Flown || !slot.Card) continue;
                if (!slot.Chosen)
                {
                    if (k.Chosen)
                    {
                        slot.Chosen = true;
                        slot.Roll?.Kill();
                        slot.Card.ShowCard(t, k.Pick);
                        slot.Card.PlayComplete(completeSec / Math.Max(1, speed), completeScale, completeUpRatio, completeFlash);
                    }
                    else if (k.Flips != slot.Flips)
                    {
                        slot.Flips = k.Flips;
                        RollStep(s, slot, k.Tier, speed);
                    }
                }
                if (k.Flying)
                {
                    slot.Flown = true;
                    FlyStarted?.Invoke(k, slot.Card);
                    slot.Card.ResetLook();
                    slot.Card.gameObject.SetActive(false);   // 자리(Holder)는 남겨 다른 카드가 밀리지 않게
                }
            }
        }

        /// <summary>룰렛 카드 수만큼 자리를 만들고(첫 자리 = 디자이너 Holder) 그 상태로 그린다(중간에 붙어도 시뮬 상태 그대로).</summary>
        void Begin(RunState s, RouletteState rl)
        {
            var t = s.Tables;
            for (int i = 0; i < rl.Cards.Count; i++)
            {
                Slot slot;
                if (i == 0) { slot = _first; Clear(slot); slot.Holder.gameObject.SetActive(true); }
                else slot = _pool.Get();
                _slots.Add(slot);
                if (!slot.Card) continue;
                var k = rl.Cards[i];
                slot.Flips = k.Flips;
                slot.Chosen = k.Chosen;
                if (k.Chosen) slot.Card.ShowCard(t, k.Pick);
                else slot.Card.BeginRoll(t, k.Tier, Faces(s, k.Tier));
                if (k.Flying) { slot.Flown = true; slot.Card.gameObject.SetActive(false); }
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

        void RollStep(RunState s, Slot slot, int tier, float speed)
        {
            if (slot.Roll != null && slot.Roll.IsActive()) slot.Roll.Complete(true);
            var faces = Faces(s, tier);
            string next = faces.Count > 0 ? faces[0] : null;
            float sec = rollStepSec / Math.Max(1, speed);
            var c = slot.Card;
            if (!HudRectMotion.Animate) { c.Advance(next); return; }
            float k = 0;
            slot.Roll = DOTween.To(() => k, v => { k = v; c.SetStripOffset(v); }, 1f, sec).SetUpdate(true).SetTarget(c.transform)
                .OnComplete(() => c.Advance(next));
        }
    }
}
