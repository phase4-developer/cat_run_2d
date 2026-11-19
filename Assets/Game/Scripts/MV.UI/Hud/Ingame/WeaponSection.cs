using System;
using System.Collections.Generic;
using DG.Tweening;
using jjevol;
using MV.Data;
using MV.Run;
using MV.Sound;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 무기 섹션(<c>IngameScreen/Mid/Weapon</c>, MV-UI3-HUD4, HUD_구조_설계안 3.3 WeaponSection·7.4).
    /// 장착 칸 + 손패 스택. 스택 칸은 <c>Slot_Weapon_Stack</c> 프리팹 에셋을 원본으로 <see cref="ObjectPool{T}"/>에서 꺼내 손패 장 수만큼만 보인다(시작 = 시작 무기 1장).
    /// 손패 순서·시간은 <see cref="WeaponHandModel"/>(04 §4.10, 05 §5.3), 모양은 디자이너 Stack의 세로 배치 값(간격·위 여백)을 읽어 섹션이 직접 놓는다.
    /// 무기문 통과 때 문 위 카드가 같은 자리·같은 크기 UI 카드로 바뀌어(<see cref="MvHudFly.Launched"/>, 알파 변화 없음) 스택 맨 아래 칸보다 조금 밑에
    /// 한 줄로 줄을 서고, 시뮬이 준 장마다 맨 앞 카드부터 위로 올라가 스택에 들어간다(참고 영상, 사용자 2026-11-19).
    /// 시뮬 값은 읽기만 한다.
    /// </summary>
    public sealed class WeaponSection : UIBase, IHudFlyHandler
    {
        [Header("디자이너 계층")]
        [Tooltip("장착 칸 자리(Equip/Slot_Weapon).")]
        [SerializeField] RectTransform equipRoot;
        [Tooltip("손패 스택(Stack). 디자이너 VerticalLayoutGroup 값(간격·위 여백·정렬)을 읽고, 실행 중에는 섹션이 직접 놓는다.")]
        [SerializeField] RectTransform stack;
        [Tooltip("스택 칸 복제 원본: Slot_Weapon_Stack.prefab 에셋(화면 안 예시 인스턴스가 아니다).")]
        [SerializeField] HudWeaponSlot stackPrefab;
        [Tooltip("날아오는 카드를 둘 부모(보통 Weapon).")]
        [SerializeField] RectTransform flyParent;

        [Header("손패 시간(04 §4.10, 05 §5.3.2 기본값)")]
        [SerializeField] WeaponHandTiming timing = new WeaponHandTiming();

        [Header("손패 연출(05 §5.3.3 기본값)")]
        [Tooltip("자리 이동(정렬·당겨짐) 초 (L1211)")] [SerializeField] float moveSec = 0.28f;
        [Tooltip("전달 wdeal 초 (L501)")] [SerializeField] float dealFadeSec = 0.25f;
        [Tooltip("전달: 제자리보다 이 칸 높이 배만큼 아래에서 나타남")] [SerializeField] float dealFromBelow = 1f;
        [Tooltip("전달: 칸 너비의 이 비율만큼 오른쪽에서 나타남 (wdeal translateX 35%)")] [SerializeField] float dealFromRight = 0.35f;
        [Tooltip("전달 1단계: 새 카드가 스택 맨 밑 칸까지 가는 초(사용자 2026-11-19 「무조건 제일 밑으로 가고」)")] [SerializeField] float dealToBottomSec = 0.28f;
        [Tooltip("전달 2단계: 맨 밑에서 자기 등급 자리로 올라가는 초(그 아래 카드는 같은 시간에 한 칸 내려간다). 이것이 끝난 뒤 3장이면 머지.")] [SerializeField] float dealRiseSec = 0.28f;
        [Tooltip("모으기 끝 크기 (wmg scale 1.06)")] [SerializeField] float gatherScale = 1.06f;
        [Tooltip("결과 시작 크기 (wmerged scale 1.3)")] [SerializeField] float mergedScale = 1.3f;
        [Tooltip("결과 연출 최소 초: max(이 값, u) (L1214)")] [SerializeField] float mergedMinSec = 0.15f;
        [Tooltip("장착 칸 등급 상승 튐 크기")] [SerializeField] float equipPopScale = 1.15f;
        [Tooltip("장착 칸 등급 상승 튐 초")] [SerializeField] float equipPopSec = 0.25f;

        [Header("문 카드 줄서기(MV-UI3-HUD9, 참고 영상)")]
        [Tooltip("스택 맨 아래 칸과 줄 첫 카드 사이 빈칸(px). 줄 카드 실루엣이 다 보이게 띄운다.")] [SerializeField] float queueGap = 40f;
        [Tooltip("문 위 카드 자리 → 줄 자리 비행 초")] [SerializeField] float launchSec = 0.4f;
        [Tooltip("카드마다 출발 간격 초(표 300 fly.cloneGapMs 70 ms를 옮김, 사용자 2026-11-19 「프리팹으로 관리」)")] [SerializeField] float launchGapSec = 0.07f;
        [Tooltip("받을 장이 없는데 줄에 남은 카드(거절된 장)를 치우기까지 초")] [SerializeField] float queueHoldSec = 2f;

        Func<RunState> _state;
        RunState _run;
        MvTables T => _run?.Tables;

        HudWeaponSlot _equip;
        VerticalLayoutGroup _layout;
        ObjectPool<HudWeaponSlot> _pool;
        WeaponHandModel _model;
        readonly Dictionary<int, HudWeaponSlot> _slots = new Dictionary<int, HudWeaponSlot>();   // 카드 Id → 칸
        readonly Dictionary<int, HudWeaponSlot> _flies = new Dictionary<int, HudWeaponSlot>();   // 비행 id → 칸
        readonly List<HandAction> _actions = new List<HandAction>();
        readonly List<HudWeaponSlot> _gathering = new List<HudWeaponSlot>();
        readonly Queue<int> _units = new Queue<int>();   // 받을 장(단계), 시뮬 WeaponDeliver
        int _credits;                                    // 도착한 비행 수(비행 층 Hand)

        sealed class Queued { public HudWeaponSlot Slot; public bool Arrived; }
        readonly List<Queued> _queue = new List<Queued>();   // 스택 밑 줄(앞 = 위, 먼저 들어간다)
        int _reserved;                                       // 줄 앞에서 받을 장으로 잡아 둔 카드 수
        float _queueIdle;
        int _equipTier;
        int[] _counts;
        double _now;

        /// <summary>
        /// 지금 장착 칸(손패 맨 위)에 보이는 무기 등급(없으면 0). 캐릭터 무기 외형이 이 값을 따른다
        /// (사용자 2026-11-19 「캐릭터 무기가 바뀌는건 제일위에 무기가 바뀌는 순간이야 … 머지 연출때문에 먹었을때하고 실제 적용이 다르니까」). Boot 다리가 MV.View로 넘긴다.
        /// </summary>
        public static int ShownTier { get; private set; }
        bool _ready;

        // ---- 시험·QA ----
        public HudWeaponSlot Equip { get { Init(); return _equip; } }
        /// <summary>보이는 스택 칸(위에서부터 = 손패 순서).</summary>
        public IEnumerable<HudWeaponSlot> StackSlots
        {
            get
            {
                if (_model == null) yield break;
                foreach (var c in _model.Cards) if (_slots.TryGetValue(c.Id, out var s)) yield return s;
            }
        }
        public IReadOnlyList<HandCard> Hand => _model != null ? _model.Cards : (IReadOnlyList<HandCard>)Array.Empty<HandCard>();
        public bool Busy => _model != null && !_model.Idle || _units.Count > 0 || _gathering.Count > 0 || _queue.Count > 0;
        public int PendingUnits => _units.Count;
        /// <summary>스택 밑 줄 카드(앞 = 위).</summary>
        public IEnumerable<HudWeaponSlot> QueueSlots { get { foreach (var q in _queue) yield return q.Slot; } }
        public int QueuedCards => _queue.Count;
        public int ActiveFlies => _flies.Count;
        public HudWeaponSlot StackPrefab => stackPrefab;

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            _equip = equipRoot ? equipRoot.GetComponentInChildren<HudWeaponSlot>(true) : null;
            if (stack)
            {
                _layout = stack.GetComponent<VerticalLayoutGroup>();
                if (_layout) _layout.enabled = false;   // 값만 읽고 놓기는 섹션이(이동 연출)
                // 디자이너 예시 칸은 프리팹에 그대로 두고 실행 중에만 숨긴다(설계안 7절 결정).
                foreach (Transform c in stack) c.gameObject.SetActive(false);
            }
            if (!stackPrefab) Debug.LogError("[WeaponSection] stackPrefab(Slot_Weapon_Stack.prefab 에셋)이 비어 있습니다.", this);
            _pool = new ObjectPool<HudWeaponSlot>(
                () => Instantiate(stackPrefab, stack, false),
                s => { s.ResetLook(); s.gameObject.SetActive(true); },
                s => { s.ResetLook(); s.gameObject.SetActive(false); if (s.transform.parent != stack) s.transform.SetParent(stack, false); },
                s => { if (s) Discard(s.gameObject); },
                collectionCheck: false);
        }

        static void Discard(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        protected override bool SetSubscribe(bool subscribe)
        {
            if (!base.SetSubscribe(subscribe)) return false;
            if (subscribe)
            {
                MvHudEvents.OnEvent += OnRunEvent;
                MvHudFly.Arrived += OnFlyArrived;
                MvHudFly.Launched += OnLaunched;
                MvHudFly.Register(HudFlyTarget.Hand, this);
            }
            else
            {
                MvHudEvents.OnEvent -= OnRunEvent;
                MvHudFly.Arrived -= OnFlyArrived;
                MvHudFly.Launched -= OnLaunched;
                MvHudFly.Unregister(HudFlyTarget.Hand, this);
            }
            return true;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ShownTier = 0;
            _pool?.Dispose();
        }

        // ---------------- 런 연결(화면이 런을 넘긴다) ----------------

        public void BindRun(Func<RunState> state)
        {
            Init();
            _state = state;
            _run = null;
        }

        /// <summary>실시간 dt(배속과 무관, 05 §5.3.2).</summary>
        public void Tick(float realDt)
        {
            Init();
            var s = _state?.Invoke();
            if (s != _run) { _run = s; ResetHand(s?.W); }
            if (s == null || _model == null) return;
            _now += realDt;
            Feed();
            _actions.Clear();
            _model.Step(_now, realDt, _actions);
            foreach (var a in _actions) Play(a);
            DropLeftovers(realDt);
            Resync(s);
        }

        void ResetHand(int[] w)
        {
            foreach (var kv in _slots) _pool.Release(kv.Value);
            _slots.Clear();
            foreach (var kv in _flies) _pool.Release(kv.Value);
            _flies.Clear();
            foreach (var q in _queue) _pool.Release(q.Slot);
            _queue.Clear();
            _reserved = 0;
            _queueIdle = 0;
            _gathering.Clear();
            _units.Clear();
            _credits = 0;
            _now = 0;
            _model = null;
            ShownTier = 0;
            if (_run?.Tables == null || w == null) return;
            int mergeN = T.RunConfig.I(RunConfigKeys.MERGE_N);
            _model = new WeaponHandModel(timing, mergeN, RunFormulas.WeaponTierMax(T));
            if (Animate)
            {
                _model.DealToBottomSec = dealToBottomSec;
                _model.DealRiseSec = dealRiseSec;
            }
            _model.Reset(w, _now);
            foreach (var c in _model.Cards) Rent(c);
            Layout(snap: true);
            DrawEquip(pop: false);
        }

        /// <summary>쉬는 중인데 손패가 시뮬과 다르면(예: 시험·연출 없이 값만 바뀜) 시뮬 손패로 바로 맞춘다.</summary>
        void Resync(RunState s)
        {
            if (Busy || _flies.Count > 0 || s.W == null) return;
            if (_counts == null || _counts.Length != s.W.Length) _counts = new int[s.W.Length];
            var counts = _counts;
            Array.Clear(counts, 0, counts.Length);
            foreach (var c in _model.Cards) if (c.Tier < counts.Length) counts[c.Tier]++;
            for (int t = 0; t < counts.Length; t++)
                if (counts[t] != s.W[t]) { ResetHand(s.W); return; }
        }

        // ---------------- 이벤트 ----------------

        /// <summary>런 이벤트 1개(구독은 SetSubscribe, 시험은 직접 부른다).</summary>
        public void OnRunEvent(RunEvent e)
        {
            if (_model == null) return;
            switch (e.Type)
            {
                case RunEventType.WeaponHandReset:
                    if (e.Ids != null) ResetHand(e.Ids);
                    break;
                case RunEventType.WeaponDeliver:
                    for (int i = 0; i < e.A; i++) _units.Enqueue(e.B);
                    break;
            }
        }

        /// <summary>비행 도착(구독은 SetSubscribe, 시험은 직접 부른다).</summary>
        public void OnFlyArrived(HudFlyTarget t, int amount)
        {
            if (t == HudFlyTarget.Hand) _credits++;
        }

        /// <summary>
        /// 시뮬이 준 장을 줄에 도착한 카드(또는 비행 층 도착) 만큼 손패에 넣는다(도착 = 지급, 설계안 7.4). 줄 카드는 받을 장으로 잡아 두고(<see cref="_reserved"/>)
        /// 손패 모델이 전달할 때 맨 앞 카드가 스택으로 올라간다. 날고 있는 것이 없으면 남은 장을 바로 넣는다(문 카드 없는 지급·비행 층이 없을 때).
        /// </summary>
        void Feed()
        {
            int arrived = 0;
            foreach (var q in _queue) if (q.Arrived) arrived++;
            int ready = arrived - _reserved;
            int flying = _queue.Count - arrived + MvHudFly.CountInFlight(HudFlyTarget.Hand);
            while (_units.Count > 0 && (ready > 0 || _credits > 0 || flying == 0))
            {
                int tier = _units.Peek(), n = 0;
                while (_units.Count > 0 && _units.Peek() == tier && (ready > 0 || _credits > 0 || flying == 0))
                {
                    _units.Dequeue();
                    if (ready > 0) { ready--; _reserved++; }
                    else if (_credits > 0) _credits--;
                    n++;
                }
                _model.Deliver(n, tier);
            }
            if (_units.Count == 0 && flying == 0) _credits = 0;
        }

        /// <summary>받을 장이 없는데 줄에 남은 카드(시뮬이 거절한 장)는 잠시 뒤 치운다. 잡아 둔 카드는 남긴다.</summary>
        void DropLeftovers(float dt)
        {
            bool allArrived = true;
            foreach (var q in _queue) if (!q.Arrived) { allArrived = false; break; }
            if (_queue.Count <= _reserved || _units.Count > 0 || !allArrived || !_model.Idle) { _queueIdle = 0; return; }
            _queueIdle += dt;
            if (_queueIdle < queueHoldSec) return;
            _queueIdle = 0;
            while (_queue.Count > _reserved)
            {
                int last = _queue.Count - 1;
                _pool.Release(_queue[last].Slot);
                _queue.RemoveAt(last);
            }
            LayoutQueue();
        }

        // ---------------- 손패 연출 ----------------

        HudWeaponSlot Rent(HandCard c)
        {
            var s = _pool.Get();
            s.Set(T, c.Tier, DmgMul());
            _slots[c.Id] = s;
            return s;
        }

        double DmgMul() => _run?.Tables != null ? RunFormulas.CardMul(_run, CardEffectType.DmgMul) : 1;

        void Play(in HandAction a)
        {
            switch (a.Kind)
            {
                case HandActionKind.Dealt:
                {
                    if (_reserved > 0 && _queue.Count > 0)
                    {
                        // 줄 맨 앞 카드가 그대로 스택 자리로 올라간다(알파·크기 변화 없음).
                        var q = _queue[0];
                        _queue.RemoveAt(0);
                        _reserved--;
                        var s = q.Slot;
                        DOTween.Kill(s.rectTransform);
                        s.transform.localScale = Vector3.one;
                        s.Set(T, a.Card.Tier, DmgMul());
                        _slots[a.Card.Id] = s;
                        Layout(snap: false, dealt: s, dealtInPlace: true);
                    }
                    else
                    {
                        var s = Rent(a.Card);
                        Layout(snap: false, dealt: s);
                        if (Animate) s.PlayDeal(dealFadeSec);
                    }
                    break;
                }
                case HandActionKind.GatherStart:
                {
                    _gathering.Clear();
                    foreach (var id in a.Gathered) if (_slots.TryGetValue(id, out var g)) _gathering.Add(g);
                    if (_gathering.Count == 0) break;
                    // 그 등급 앞 3장이 2번째 카드 자리로(md × 0.9, ease-in) + 밝아짐·scale 1.06. 층 맨 앞.
                    var mid = _gathering[Math.Min(1, _gathering.Count - 1)];
                    var to = mid.rectTransform.anchoredPosition;
                    foreach (var g in _gathering)
                    {
                        g.transform.SetAsLastSibling();
                        g.ResetLook();
                        if (Animate)
                        {
                            AnchorTo(g.rectTransform, to, a.MoveSec).SetEase(Ease.InQuad).SetTarget(g);
                            g.PlayGather(a.Duration, gatherScale);
                        }
                        else g.rectTransform.anchoredPosition = to;
                    }
                    break;
                }
                case HandActionKind.Merged:
                {
                    Vector2 at = default;
                    bool has = false;
                    if (_gathering.Count > 0)
                    {
                        at = _gathering[Math.Min(1, _gathering.Count - 1)].rectTransform.anchoredPosition;
                        has = true;
                    }
                    _gathering.Clear();
                    if (a.Gathered != null)
                        foreach (var id in a.Gathered)
                            if (_slots.TryGetValue(id, out var gone)) { _slots.Remove(id); _pool.Release(gone); }

                    var s = Rent(a.Card);
                    if (has) s.rectTransform.anchoredPosition = at;
                    Layout(snap: false, placed: has ? s : null);
                    if (Animate) s.PlayMerged(Math.Max(mergedMinSec, a.Unit), mergedScale);
                    MvSound.PlayUi(MvUiSound.HandMerge);
                    break;
                }
            }
            if (a.TopTierUp) DrawEquip(pop: true);
            LayoutQueue();
        }

        static bool Animate => Application.isPlaying;

        /// <summary>anchoredPosition 보간(DOTween UI 모듈 없이 코어만 쓴다). 실시간.</summary>
        static Tween AnchorTo(RectTransform rt, Vector2 to, float sec) =>
            DOTween.To(() => rt.anchoredPosition, v => rt.anchoredPosition = v, to, sec).SetUpdate(true);

        /// <summary>
        /// 손패 순서대로 자리를 정한다(디자이너 Stack 세로 배치 값: 위 여백·간격, 반대 순서 = 위 칸이 앞).
        /// dealt = 방금 들어온 칸: 먼저 스택 맨 밑 칸 자리로 가고(<see cref="dealToBottomSec"/>), 그다음 자기 등급 자리로 올라간다(<see cref="dealRiseSec"/>).
        /// 그 자리 아래 카드들은 올라가는 동안 한 칸씩 내려간다(사용자 2026-11-19 「바로 자기 등급 밑으로 이동이 아니라 무조건 제일 밑으로가고 그 다음 위로 올라가면서 자기 등급 자리 찾아가면서 정렬」).
        /// dealtInPlace = 이미 화면에 있는 칸(줄 맨 앞 카드)이라 지금 자리에서 출발, 아니면 맨 밑 칸 아래·오른쪽에서 나타난다. placed = 이미 다른 자리에 놓인 칸(머지 결과).
        /// </summary>
        void Layout(bool snap, HudWeaponSlot dealt = null, HudWeaponSlot placed = null, bool dealtInPlace = false)
        {
            if (_model == null || !stack) return;
            float top = _layout ? _layout.padding.top : 0, spacing = _layout ? _layout.spacing : 0;
            var cards = _model.Cards;
            int n = cards.Count;
            int di = -1;
            if (dealt != null)
                for (int i = 0; i < n; i++)
                    if (_slots.TryGetValue(cards[i].Id, out var d) && d == dealt) { di = i; break; }
            float y = -top;
            Vector2 bottom = default;
            for (int i = 0; i < n; i++)
            {
                if (!_slots.TryGetValue(cards[i].Id, out var b)) continue;
                if (i == n - 1) bottom = new Vector2(0, y);
                y -= b.rectTransform.rect.height + spacing;
            }
            y = -top;
            for (int i = 0; i < n; i++)
            {
                if (!_slots.TryGetValue(cards[i].Id, out var s)) continue;
                var rt = s.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f);
                rt.pivot = new Vector2(.5f, 1f);
                float h = rt.rect.height;
                var to = new Vector2(0, y);
                y -= h + spacing;
                if (_gathering.Contains(s)) continue;   // 모이는 중인 칸은 모으기 연출이 움직인다
                DOTween.Kill(rt);
                if (snap || !Animate) { rt.anchoredPosition = to; continue; }
                if (s == dealt)
                {
                    // 1단계: 맨 밑 칸 자리로. 2단계: 자기 자리로 올라감(맨 밑이면 1단계로 끝).
                    if (!dealtInPlace) rt.anchoredPosition = bottom + new Vector2(rt.rect.width * dealFromRight, -h * dealFromBelow);
                    var seq = DOTween.Sequence().SetUpdate(true).SetTarget(rt)
                        .Append(AnchorTo(rt, bottom, dealToBottomSec).SetEase(Ease.OutQuad));
                    if (i < n - 1) seq.Append(AnchorTo(rt, to, dealRiseSec).SetEase(Ease.InOutQuad));
                    continue;
                }
                if (di >= 0 && i > di)
                {
                    // 새 카드 자리 아래 카드: 새 카드가 맨 밑에 닿은 뒤 올라오는 동안 한 칸 내려간다.
                    AnchorTo(rt, to, dealRiseSec).SetDelay(dealToBottomSec).SetEase(Ease.InOutQuad).SetTarget(rt);
                    continue;
                }
                // 머지 결과(placed)는 모인 자리에서 출발한다.
                AnchorTo(rt, to, moveSec).SetEase(Ease.OutQuad).SetTarget(rt);
            }
            OrderSiblings();
        }

        /// <summary>위 칸이 맨 앞(마지막 형제)이 되게 다시 맞춘다(디자이너 반대 순서 배치와 같은 겹침).</summary>
        void OrderSiblings()
        {
            var cards = _model.Cards;
            for (int i = cards.Count - 1; i >= 0; i--)
                if (_slots.TryGetValue(cards[i].Id, out var s)) s.transform.SetAsLastSibling();
            foreach (var g in _gathering) if (g) g.transform.SetAsLastSibling();
        }

        void DrawEquip(bool pop)
        {
            if (!_equip || _model == null) return;
            int tier = _model.TopTier;
            ShownTier = tier;
            _equip.Set(T, tier, DmgMul());
            bool up = pop && tier > _equipTier;
            _equipTier = tier;
            if (!up) return;
            MvSound.PlayUi(MvUiSound.HandTierUp);
            if (!Animate) return;
            var t = _equip.transform;
            DOTween.Kill(t);
            t.localScale = Vector3.one;
            DOTween.Sequence().SetUpdate(true).SetTarget(t)
                .Append(t.DOScale(equipPopScale, equipPopSec * .6f).SetEase(Ease.OutQuad))
                .Append(t.DOScale(1f, equipPopSec * .4f).SetEase(Ease.InQuad));
        }

        // ---------------- 문 카드 줄서기(MV-UI3-HUD9) ----------------

        float SlotHeight() => stackPrefab ? ((RectTransform)stackPrefab.transform).rect.height : 0;

        /// <summary>줄 k번째 자리(스택 기준 anchoredPosition, 위 가운데 기준): 스택 맨 아래 칸 밑 <see cref="queueGap"/> 띄우고 칸 간격으로 아래로.</summary>
        Vector2 QueueAt(int k)
        {
            float h = SlotHeight();
            float top = _layout ? _layout.padding.top : 0, spacing = _layout ? _layout.spacing : 0;
            int n = _model != null ? _model.Cards.Count : 0;
            return new Vector2(0, -top - n * (h + spacing) - queueGap - k * (h + spacing));
        }

        /// <summary>도착한 줄 카드를 자리로(스택이 늘고 줄 앞이 빠지면 나머지는 그대로, 머지로 스택이 줄면 위로 당겨진다).</summary>
        void LayoutQueue()
        {
            for (int k = 0; k < _queue.Count; k++)
            {
                var q = _queue[k];
                if (!q.Arrived) continue;   // 나는 중인 카드는 도착하면 다시 맞춘다
                var rt = q.Slot.rectTransform;
                var to = QueueAt(k);
                if (rt.anchoredPosition == to) continue;
                DOTween.Kill(rt);
                if (!Animate) rt.anchoredPosition = to;
                else AnchorTo(rt, to, moveSec).SetEase(Ease.OutQuad).SetTarget(rt);
            }
        }

        /// <summary>
        /// 문 위 카드가 HUD 카드로 바뀐다(구독은 SetSubscribe, 시험은 직접 부른다). 카드마다 같은 화면 자리·같은 크기로 스택 칸을 세우고(알파 1)
        /// 스택 밑 줄 자리로 날린다. 손패 지급은 시뮬 WeaponDeliver가 오면 <see cref="Feed"/>가 한다.
        /// </summary>
        public void OnLaunched(HudFlyTarget target, IReadOnlyList<Rect> cards)
        {
            Init();
            if (target != HudFlyTarget.Hand || _model == null || !stack || cards == null) return;
            var cam = Cam();
            float h = SlotHeight();
            float screenH = ScreenHeight(h, cam);
            var anchorRef = new Vector2(stack.rect.center.x, stack.rect.yMax);
            int tier = _units.Count > 0 ? _units.Peek() : _run != null ? _run.Bt : 1;
            for (int i = 0; i < cards.Count; i++)
            {
                var r = cards[i];
                var s = _pool.Get();
                if (s.transform.parent != stack) s.transform.SetParent(stack, false);
                s.transform.SetAsLastSibling();
                s.Set(T, tier, DmgMul());
                var rt = s.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f);
                rt.pivot = new Vector2(.5f, 1f);
                float scale = screenH > 0 && r.height > 0 ? r.height / screenH : 1f;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(stack, r.center, cam, out var local);
                rt.anchoredPosition = local - anchorRef + new Vector2(0, h * scale * .5f);
                s.transform.localScale = new Vector3(scale, scale, 1f);
                var q = new Queued { Slot = s };
                _queue.Add(q);
                var to = QueueAt(_queue.Count - 1);
                if (!Animate)
                {
                    rt.anchoredPosition = to;
                    s.transform.localScale = Vector3.one;
                    q.Arrived = true;
                    continue;
                }
                float delay = i * launchGapSec;
                DOTween.Sequence().SetUpdate(true).SetTarget(rt)
                    .Insert(delay, AnchorTo(rt, to, launchSec).SetEase(Ease.InOutQuad))
                    .Insert(delay, s.transform.DOScale(1f, launchSec).SetEase(Ease.InOutQuad))
                    .OnComplete(() => { q.Arrived = true; LayoutQueue(); });
            }
            _queueIdle = 0;
        }

        /// <summary>스택 안 높이 h를 화면 px 높이로.</summary>
        float ScreenHeight(float h, Camera cam)
        {
            var p0 = RectTransformUtility.WorldToScreenPoint(cam, stack.TransformPoint(Vector3.zero));
            var p1 = RectTransformUtility.WorldToScreenPoint(cam, stack.TransformPoint(new Vector3(0, h, 0)));
            return Vector2.Distance(p0, p1);
        }

        // ---------------- 날아오는 카드(IHudFlyHandler, 비행 층 Hand) ----------------

        public bool TryGetRect(out Vector2 center, out Vector2 size)
        {
            center = size = default;
            if (!stack) return false;
            // 도착 = 스택 맨 아래 칸 다음 자리(새 카드가 들어갈 곳).
            float h = stackPrefab ? ((RectTransform)stackPrefab.transform).rect.height : 0;
            float top = _layout ? _layout.padding.top : 0, spacing = _layout ? _layout.spacing : 0;
            int n = _model != null ? _model.Cards.Count : 0;
            var local = new Vector2(stack.rect.center.x, stack.rect.yMax - top - n * (h + spacing) - h * .5f);
            var cam = Cam();
            center = RectTransformUtility.WorldToScreenPoint(cam, stack.TransformPoint(local));
            var p0 = RectTransformUtility.WorldToScreenPoint(cam, stack.TransformPoint(local - new Vector2(h, h) * .5f));
            var p1 = RectTransformUtility.WorldToScreenPoint(cam, stack.TransformPoint(local + new Vector2(h, h) * .5f));
            size = new Vector2(Mathf.Abs(p1.x - p0.x), Mathf.Abs(p1.y - p0.y));
            return true;
        }

        public void Show(int id, int amount)
        {
            if (!flyParent || _model == null || _flies.ContainsKey(id)) return;
            var s = _pool.Get();
            s.transform.SetParent(flyParent, false);
            s.transform.SetAsLastSibling();
            int tier = _units.Count > 0 ? _units.Peek() : _run != null ? _run.Bt : 1;
            s.Set(T, tier, DmgMul());
            var rt = s.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            _flies[id] = s;
        }

        public void Place(int id, Vector2 screen, float scale, float alpha)
        {
            if (!_flies.TryGetValue(id, out var s) || !flyParent) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(flyParent, screen, Cam(), out var local))
                s.rectTransform.localPosition = new Vector3(local.x, local.y, 0);
            s.transform.localScale = Vector3.one * scale;
            s.SetAlpha(alpha);
        }

        public void Hide(int id)
        {
            if (!_flies.TryGetValue(id, out var s)) return;
            _flies.Remove(id);
            _pool.Release(s);
        }

        Camera Cam()
        {
            var c = GetComponentInParent<Canvas>();
            c = c ? c.rootCanvas : null;
            return c && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
        }
    }
}
