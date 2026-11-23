using System;
using System.Collections.Generic;
using DG.Tweening;
using MV.Data;
using MV.Run;
using MV.Sound;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 무기 손패 스택 공용 부품(MV-UI3-LOBBY-WEAPONFX). 인게임 <c>IngameScreen/Mid/Weapon</c>과 로비 <c>LobbyScreen/Mid/Weapon</c>이 같이 쓴다.
    /// 장착 칸 + 스택 칸(<c>Slot_Weapon_Stack</c> 프리팹 에셋을 원본으로 <see cref="ObjectPool{T}"/>), 스택 밑 줄 서기, 전달(맨 앞 카드가 위로 올라감)·같은 등급 머지(모으기·Flash·Fx·Scale)·장착 칸 튐을 맡는다.
    /// 손패 순서·시간은 <see cref="WeaponHandModel"/>(04 §4.10, 05 §5.3), 모양은 디자이너 Stack의 세로 배치 값(간격·위 여백)을 읽어 이 부품이 직접 놓는다.
    /// 카드를 어디서 받는지는 화면별 연결이 정한다: 인게임 <see cref="WeaponSection"/>(런 상태·무기문·비행 층), 로비 <see cref="LobbyWeaponStack"/>(스크래쳐 구매).
    /// 연결은 <see cref="ResetHand"/>·<see cref="Deliver"/>·줄 세우기(<see cref="LaunchFromScreen"/>/<see cref="JoinQueue"/>)·<see cref="Step"/>만 부른다.
    /// 사용자 2026-11-20 「로비와 런 화면 연출이 중복적인 부분이 존재하니까 공용 부품으로 만들고 개별적인 부분들만 따로 붙여서 구현」.
    /// </summary>
    public sealed class WeaponStackView : MonoBehaviour
    {
        [Header("디자이너 계층")]
        [Tooltip("장착 칸 자리(Equip/Slot_Weapon).")]
        [SerializeField] RectTransform equipRoot;
        [Tooltip("손패 스택(Stack). 디자이너 VerticalLayoutGroup 값(간격·위 여백·정렬)을 읽고, 실행 중에는 이 부품이 직접 놓는다.")]
        [SerializeField] RectTransform stack;
        [Tooltip("스택 칸 복제 원본: Slot_Weapon_Stack.prefab 에셋(화면 안 예시 인스턴스가 아니다).")]
        [SerializeField] HudWeaponSlot stackPrefab;
        [Tooltip("복제한 카드 칸이 누름을 받는지. 끄면 칸 그림의 raycastTarget을 꺼서 누름이 뒤로 지나간다(로비: 버튼 밖은 출발 영역, 05 §5.4.1).")]
        [SerializeField] bool cardsTakeInput = true;

        [Header("손패 시간(04 §4.10, 05 §5.3.2 기본값)")]
        [SerializeField] WeaponHandTiming timing = new WeaponHandTiming();

        [Header("손패 연출(05 §5.3.3 기본값)")]
        [Tooltip("자리 이동(정렬·당겨짐) 초 (L1211)")] [SerializeField] float moveSec = 0.28f;
        [Tooltip("전달 wdeal 초 (L501)")] [SerializeField] float dealFadeSec = 0.25f;
        [Tooltip("전달: 줄 맨 앞 자리에서 곧장 위로 스택 맨 밑 칸에 합류하는 초(올라오는 속도). 좌우 이동 없음. 머지는 이 시간이 지난 뒤.")] [SerializeField] float dealUpSec = 0.22f;
        [Tooltip("모으기 끝 크기 (wmg scale 1.06)")] [SerializeField] float gatherScale = 1.06f;
        [Tooltip("결과 시작 크기 (wmerged scale 1.3)")] [SerializeField] float mergedScale = 1.3f;
        [Tooltip("결과 연출 최소 초: max(이 값, u) (L1214)")] [SerializeField] float mergedMinSec = 0.15f;
        [Tooltip("장착 칸 등급 상승 튐 크기")] [SerializeField] float equipPopScale = 1.15f;
        [Tooltip("장착 칸 등급 상승 튐 초")] [SerializeField] float equipPopSec = 0.25f;

        [Header("줄서기(MV-UI3-HUD9, 참고 영상)")]
        [Tooltip("들어오는 무기 카드가 줄 서는 자리(줄 맨 앞 카드의 위 가운데). 비우면 스택 맨 밑 칸 아래(queueGapRatio)로 계산한다. 사용자 2026-11-19 「날라올 위치 게임오브젝트로 지정」.")] [SerializeField] RectTransform queueAnchor;
        [Tooltip("queueAnchor가 없을 때: 스택 맨 아래 칸의 아래 끝과 줄 첫 카드 사이 빈칸(칸 높이 배). 사용자 2026-11-19 「마지막 카드 위치에서 지금보다 더 아래 카드 사이즈 0.3정도 더 밑으로」.")] [SerializeField] float queueGapRatio = 0.38f;
        [Tooltip("줄 카드끼리 위 끝 사이 거리(칸 높이 배). 스택(-110 px 간격)보다 넓게 띄워 아이콘 실루엣이 보이게 한다(사용자 2026-11-19 「간격도 더 널널하게 해서 아이콘 실루엣은 확인 가능하게」).")] [SerializeField] float queueStepRatio = 0.6f;
        [Tooltip("출발 자리 → 줄 자리 비행 초(인게임 문 위 카드, 로비 Spwan → Dst와 Dst → 줄)")] [SerializeField] float launchSec = 0.4f;
        [Tooltip("카드마다 출발 간격 초(표 300 fly.cloneGapMs 70 ms를 옮김, 사용자 2026-11-19 「프리팹으로 관리」)")] [SerializeField] float launchGapSec = 0.07f;
        [Tooltip("비행 곡선 휨(출발~도착 거리 배, 바깥쪽으로 부푼 2차 곡선). 0 = 직선. 사용자 2026-11-19 「너무 직선 말고 … 곡선으로 가는게 더 자연스러울거 같아」.")] [SerializeField] float launchArc = 0.25f;
        [Tooltip("받을 장이 없는데 줄에 남은 카드(거절된 장)를 치우기까지 초")] [SerializeField] float queueHoldSec = 2f;

        [Header("버림")]
        [Tooltip("손패를 장수에 다시 맞출 때(무기 Level이 올라 버린 낮은 카드 등) 빠지는 칸이 흐려지며 사라지는 초(사용자 2026-11-19 카드 「짧게 흐려지며 사라짐」, 옛 로비 discardFadeSec). 0 = 바로.")]
        [SerializeField] float discardFadeSec = 0.15f;

        MvTables _t;
        readonly List<HudWeaponSlot> _fading = new List<HudWeaponSlot>();
        HudWeaponSlot _equip;
        VerticalLayoutGroup _layout;
        ObjectPool<HudWeaponSlot> _pool;
        WeaponHandModel _model;
        readonly Dictionary<int, HudWeaponSlot> _slots = new Dictionary<int, HudWeaponSlot>();   // 카드 Id → 칸
        readonly List<HandAction> _actions = new List<HandAction>();
        readonly List<HudWeaponSlot> _gathering = new List<HudWeaponSlot>();
        /// <summary>모으는 자리 y(맨 위 카드 자리). 모이는 3장은 깊이를 이 높이로 고정해 맨 위 카드가 앞에 남는다.</summary>
        float _gatherY;

        /// <summary>전달 이동 중인 칸. 매 프레임 지금 손패 목표 자리를 다시 읽어 따라간다(다음 카드가 이어서 들어와 목표가 바뀌어도 경로가 끊기지 않는다).</summary>
        sealed class DealMove
        {
            public HudWeaponSlot Slot;
            public Vector2 Start;
            public float T;
        }
        readonly List<DealMove> _moves = new List<DealMove>();
        readonly Queue<int> _units = new Queue<int>();   // 받을 장(단계)
        int _credits;                                    // 줄 없이 도착한 카드 수(인게임 비행 층 Hand)

        sealed class Queued { public HudWeaponSlot Slot; public bool Arrived; }
        readonly List<Queued> _queue = new List<Queued>();   // 스택 밑 줄(앞 = 위, 먼저 들어간다)
        int _reserved;                                       // 줄 앞에서 받을 장으로 잡아 둔 카드 수
        float _queueIdle;
        int _equipTier;
        int[] _counts;
        double _now;
        bool _ready;

        /// <summary>카드 공격력 배수. 인게임은 런 카드 효과, 로비는 1.</summary>
        public Func<double> DmgMul { get; set; }
        /// <summary>줄 밖에서 날고 있는 카드 수(인게임 비행 층). 0이 아니면 받을 장을 도착할 때까지 붙잡는다.</summary>
        public Func<int> ExternalInFlight { get; set; }
        /// <summary>장착 칸(손패 맨 위)에 보이는 등급이 정해졌다(없으면 0). 인게임은 캐릭터 무기 외형을 이 값에 맞춘다.</summary>
        public event Action<int> TopTierShown;

        // ---- 시험·QA ----
        public HudWeaponSlot Equip { get { Init(); return _equip; } }
        public RectTransform Stack => stack;
        public HudWeaponSlot StackPrefab => stackPrefab;
        public RectTransform QueueAnchor => queueAnchor;
        public MvTables Tables => _t;
        public bool HasHand => _model != null;
        /// <summary>손패 카드 칸(위에서부터 = 손패 순서). 맨 위 카드 칸은 장착 칸 자리에 숨어 있다(<see cref="TopIsEquip"/>).</summary>
        public IEnumerable<HudWeaponSlot> StackSlots
        {
            get
            {
                if (_model == null) yield break;
                foreach (var c in _model.Cards) if (_slots.TryGetValue(c.Id, out var s)) yield return s;
            }
        }
        public IReadOnlyList<HandCard> Hand => _model != null ? _model.Cards : (IReadOnlyList<HandCard>)Array.Empty<HandCard>();
        public bool Busy => _model != null && !_model.Idle || _units.Count > 0 || _gathering.Count > 0 || _queue.Count > 0 || _moves.Count > 0;
        public int PendingUnits => _units.Count;
        /// <summary>스택 밑 줄 카드(앞 = 위).</summary>
        public IEnumerable<HudWeaponSlot> QueueSlots { get { foreach (var q in _queue) yield return q.Slot; } }
        public int QueuedCards => _queue.Count;

        /// <summary>빈 직렬화 참조 이름(시험·QA).</summary>
        public List<string> MissingRefs()
        {
            var miss = new List<string>();
            if (!equipRoot) miss.Add(nameof(equipRoot));
            if (!stack) miss.Add(nameof(stack));
            if (!stackPrefab) miss.Add(nameof(stackPrefab));
            return miss;
        }

        void Awake() => Init();

        /// <summary>한 번만: 장착 칸을 찾고 디자이너 예시 칸을 숨기고 풀을 만든다(EditMode 시험은 Awake가 돌지 않아 연결이 먼저 부른다).</summary>
        public void Init()
        {
            if (_ready) return;
            _ready = true;
            _equip = equipRoot ? equipRoot.GetComponentInChildren<HudWeaponSlot>(true) : null;
            if (stack)
            {
                _layout = stack.GetComponent<VerticalLayoutGroup>();
                if (_layout) _layout.enabled = false;   // 값만 읽고 놓기는 이 부품이(이동 연출)
                // 디자이너 예시 칸은 프리팹에 그대로 두고 실행 중에만 숨긴다(설계안 7절 결정).
                foreach (Transform c in stack) c.gameObject.SetActive(false);
            }
            if (!stackPrefab) Debug.LogWarning("[WeaponStackView] stackPrefab(Assets/Game/Art/UI/Prefab/HUD/Slot_Weapon_Stack.prefab 에셋)이 비어 있습니다.", this);
            _pool = new ObjectPool<HudWeaponSlot>(
                CreateSlot,
                s => { s.ResetLook(); s.gameObject.SetActive(true); },
                s => { s.ResetLook(); s.gameObject.SetActive(false); if (s.transform.parent != stack) s.transform.SetParent(stack, false); },
                s => { if (s) Discard(s.gameObject); },
                collectionCheck: false);
        }

        HudWeaponSlot CreateSlot()
        {
            var s = Instantiate(stackPrefab, stack, false);
            if (!cardsTakeInput)
                foreach (var g in s.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            return s;
        }

        static void Discard(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        void OnDestroy()
        {
            foreach (var s in _fading) if (s) DOTween.Kill(s);
            _fading.Clear();
            _pool?.Dispose();
        }

        // ---------------- 손패 공급(화면별 연결이 부른다) ----------------

        /// <summary>
        /// 손패를 등급별 장수 <paramref name="w"/>로 바로 놓는다(연출 없이). 줄·이동·받을 장은 비운다. 표나 장수가 없으면 비운 채로 둔다.
        /// </summary>
        public void ResetHand(MvTables t, IReadOnlyList<int> w) => ResetHand(t, w, fade: false);

        /// <param name="fade">보이던 칸을 바로 거두지 않고 <see cref="discardFadeSec"/> 동안 흐린 뒤 거둔다. 남는 카드는 같은 자리에 새 칸이 앞에 서서 그대로 보이고, 빠지는 카드만 흐려진다.</param>
        void ResetHand(MvTables t, IReadOnlyList<int> w, bool fade)
        {
            Init();
            FinishFades();
            foreach (var kv in _slots)
            {
                if (fade && Animate && discardFadeSec > 0 && kv.Value.gameObject.activeInHierarchy) FadeOut(kv.Value);
                else _pool.Release(kv.Value);
            }
            _slots.Clear();
            foreach (var q in _queue) _pool.Release(q.Slot);
            _queue.Clear();
            _reserved = 0;
            _queueIdle = 0;
            _gathering.Clear();
            _moves.Clear();
            _units.Clear();
            _credits = 0;
            _now = 0;
            _model = null;
            _t = t;
            TopTierShown?.Invoke(0);
            if (t == null || w == null) return;
            int mergeN = t.RunConfig.I(RunConfigKeys.MERGE_N);
            _model = new WeaponHandModel(timing, mergeN, RunFormulas.WeaponTierMax(t));
            if (Animate) _model.DealStraightSec = dealUpSec;
            _model.Reset(w, _now);
            foreach (var c in _model.Cards) Rent(c);
            Layout(snap: true);
            DrawEquip(pop: false);
        }

        /// <summary>받을 장 <paramref name="n"/>장(단계 <paramref name="tier"/>). 줄에 도착한 카드 수만큼 손패에 넣는다(<see cref="Feed"/>).</summary>
        public void Deliver(int n, int tier)
        {
            for (int i = 0; i < n; i++) _units.Enqueue(tier);
        }

        /// <summary>줄 없이 카드 1장이 도착했다(인게임 비행 층 Hand).</summary>
        public void AddArrival() => _credits++;

        /// <summary>다음에 받을 장의 단계(없으면 <paramref name="fallback"/>). 줄·비행 카드 그림에 쓴다.</summary>
        public int NextTier(int fallback) => _units.Count > 0 ? _units.Peek() : fallback;

        /// <summary>
        /// 실시간 dt(배속과 무관, 05 §5.3.2). <paramref name="target"/>이 있으면 쉬는 중 손패가 그 장수와 다를 때 바로 맞춘다
        /// (예: 시험·연출 없이 값만 바뀜, 로비 무기 Level 상승으로 낮은 카드 버림). 화면별 연결이 아직 줄에 넣지 않은 카드가 있으면 null을 넘긴다.
        /// </summary>
        public void Step(float realDt, IReadOnlyList<int> target)
        {
            Init();
            if (_model == null) return;
            _now += realDt;
            Feed();
            _actions.Clear();
            _model.Step(_now, realDt, _actions);
            foreach (var a in _actions) Play(a);
            StepMoves(realDt);
            OrderSiblings();   // 움직이는 중에도 지금 높이로 깊이를 맞춘다
            DropLeftovers(realDt);
            Sync(target);
        }

        /// <summary>쉬는 중이고 손패 장수가 <paramref name="target"/>과 다르면 바로 맞춘다. 맞췄으면 true.</summary>
        public bool Sync(IReadOnlyList<int> target)
        {
            if (target == null || _model == null || Busy) return false;
            if (_counts == null || _counts.Length != target.Count) _counts = new int[target.Count];
            var counts = _counts;
            Array.Clear(counts, 0, counts.Length);
            foreach (var c in _model.Cards) if (c.Tier < counts.Length) counts[c.Tier]++;
            for (int t = 0; t < counts.Length; t++)
                if (counts[t] != target[t]) { ResetHand(_t, target, fade: true); return true; }
            return false;
        }

        void FadeOut(HudWeaponSlot s)
        {
            DOTween.Kill(s.rectTransform);
            DOTween.Kill(s);
            _fading.Add(s);
            float a = 1f;
            DOTween.To(() => a, v => { a = v; s.SetAlpha(v); }, 0f, discardFadeSec).SetEase(Ease.OutQuad).SetTarget(s)
                .OnComplete(() =>
                {
                    if (!_fading.Remove(s)) return;
                    s.SetAlpha(1f);
                    _pool.Release(s);
                });
        }

        /// <summary>흐려지는 칸을 바로 거둔다(다시 맞추기 전).</summary>
        void FinishFades()
        {
            if (_fading.Count == 0) return;
            var list = _fading.ToArray();
            _fading.Clear();
            foreach (var s in list)
            {
                if (!s) continue;
                DOTween.Kill(s);
                s.SetAlpha(1f);
                _pool.Release(s);
            }
        }

        /// <summary>
        /// 받을 장을 줄에 도착한 카드(또는 줄 없는 도착) 만큼 손패에 넣는다(도착 = 지급, 설계안 7.4). 줄 카드는 받을 장으로 잡아 두고(<see cref="_reserved"/>)
        /// 손패 모델이 전달할 때 맨 앞 카드가 스택으로 올라간다. 날고 있는 것이 없으면 남은 장을 바로 넣는다(줄 카드 없는 지급).
        /// </summary>
        void Feed()
        {
            int arrived = 0;
            foreach (var q in _queue) if (q.Arrived) arrived++;
            int ready = arrived - _reserved;
            int flying = _queue.Count - arrived + (ExternalInFlight?.Invoke() ?? 0);
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

        /// <summary>받을 장이 없는데 줄에 남은 카드(거절된 장)는 잠시 뒤 치운다. 잡아 둔 카드는 남긴다.</summary>
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

        // ---------------- 손패 밖 칸(비행·줄 세우기 전) ----------------

        /// <summary>손패 밖에서 쓸 칸을 빌린다(같은 풀). 다 쓰면 <see cref="JoinQueue"/>로 줄에 세우거나 <see cref="ReleaseLoose"/>로 돌려준다.</summary>
        public HudWeaponSlot RentLoose(int tier)
        {
            Init();
            var s = _pool.Get();
            s.Set(_t, tier, Mul());
            return s;
        }

        public void ReleaseLoose(HudWeaponSlot s)
        {
            if (s) _pool.Release(s);
        }

        // ---------------- 손패 연출 ----------------

        HudWeaponSlot Rent(HandCard c)
        {
            var s = _pool.Get();
            s.Set(_t, c.Tier, Mul());
            _slots[c.Id] = s;
            return s;
        }

        double Mul() => DmgMul?.Invoke() ?? 1;

        void Play(in HandAction a)
        {
            float equipMergedSec = -1;   // 머지 결과가 맨 위(장착 칸)에 놓이면 장착 칸이 결과 연출을 한다(MV-UI3-FXFIX)
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
                        s.Set(_t, a.Card.Tier, Mul());
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
                    // 그 등급 3장이 맨 위 카드 자리로 위로 모인다(md × 0.9, ease-in) + 밝아짐·scale 1.06.
                    // 05 §5.3.3 「2번째 카드 자리」 대신 사용자 2026-11-19 「무기 머지 3개가 위로 모여지게 … 뎁스도 제일 위에있는게 유지되야」 → 「그렇게 바꿔」.
                    // 모을 자리 = 맨 위 카드의 손패 자리(올라오는 중이어도 이동이 끝나기를 기다리지 않는다, 사용자 2026-11-19 「3개 배치되면 그때 바로 머지」).
                    var to = TargetOf(_gathering[0], out var top, out _) ? top : _gathering[0].rectTransform.anchoredPosition;   // Gathered는 위 → 아래 순서
                    _gatherY = to.y;
                    _moves.RemoveAll(mv => _gathering.Contains(mv.Slot));   // 올라오던 카드도 곧장 모으는 자리로
                    foreach (var g in _gathering)
                    {
                        g.ResetLook();   // 깊이는 높이 규칙 그대로(위 카드가 앞): 모이는 3장이 바로 위 카드 앞으로 튀어나오지 않는다(사용자 2026-11-19 「마지막 머지할때 뎁스가 이상하게」)
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
                        at = _gathering[0].rectTransform.anchoredPosition;
                        has = true;
                    }
                    _gathering.Clear();
                    if (a.Gathered != null)
                        foreach (var id in a.Gathered)
                            if (_slots.TryGetValue(id, out var gone)) { _slots.Remove(id); _pool.Release(gone); }

                    var s = Rent(a.Card);
                    if (has) s.rectTransform.anchoredPosition = at;
                    Layout(snap: false, placed: has ? s : null);
                    float sec = Math.Max(mergedMinSec, a.Unit);
                    // 맨 위 카드 칸은 장착 칸 자리에 숨어 있어(TopIsEquip) 그 칸 연출은 보이지 않는다 → 장착 칸이 크기·Flash·Sparkle을 한다.
                    // 사용자 2026-11-20 「무기가 합쳐질때 연출이 제일위에꺼 실제 장착 무기가 나올때는 안나오는거 같은데」.
                    if (TopIsEquip && _model.Cards.Count > 0 && _model.Cards[0].Id == a.Card.Id) equipMergedSec = sec;
                    else if (Animate) s.PlayMerged(sec, mergedScale);
                    MvSound.PlayUi(MvUiSound.HandMerge);
                    break;
                }
            }
            if (a.TopTierUp || equipMergedSec >= 0) DrawEquip(pop: a.TopTierUp, mergedSec: equipMergedSec);
            LayoutQueue();
        }

        static bool Animate => Application.isPlaying;

        /// <summary>anchoredPosition 보간(DOTween UI 모듈 없이 코어만 쓴다). 실시간.</summary>
        static Tween AnchorTo(RectTransform rt, Vector2 to, float sec) =>
            DOTween.To(() => rt.anchoredPosition, v => rt.anchoredPosition = v, to, sec);

        /// <summary>
        /// 손패 순서대로 자리를 정한다(디자이너 Stack 세로 배치 값: 위 여백·간격, 반대 순서 = 위 칸이 앞).
        /// dealt = 방금 들어온 칸: 줄 맨 앞 자리(깊이 맨 뒤)에서 곧장 위로 자기 자리에 합류한다(<see cref="dealUpSec"/>).
        /// 무기 단계가 오를 때 그보다 낮은 카드는 버리므로(04 §4.10, 사용자 2026-11-19) 새 카드 자리는 언제나 스택 맨 밑이다 — 옆으로 도는 연출은 없다(같은 날 「사이드 연출은 빠져도」).
        /// 깊이는 지금 높이로 매 프레임 정한다(위 카드가 앞). dealtInPlace = 이미 화면에 있는 칸(줄 맨 앞 카드)이라 지금 자리에서 출발, 아니면 줄 맨 앞 자리에서 나타난다. placed = 머지 결과.
        /// </summary>
        void Layout(bool snap, HudWeaponSlot dealt = null, HudWeaponSlot placed = null, bool dealtInPlace = false)
        {
            if (_model == null || !stack) return;
            float top = _layout ? _layout.padding.top : 0, spacing = _layout ? _layout.spacing : 0;
            var cards = _model.Cards;
            int n = cards.Count;
            if (snap || !Animate) FinishMoves();
            if (dealt != null && !snap && Animate)
            {
                int di = -1;
                for (int i = 0; i < n; i++)
                    if (_slots.TryGetValue(cards[i].Id, out var d) && d == dealt) { di = i; break; }
                if (di >= 0)
                {
                    var drt = dealt.rectTransform;
                    DOTween.Kill(drt);
                    // 출발 = 줄 맨 앞 자리(새 카드가 들어오기 전 스택 기준).
                    if (!dealtInPlace) drt.anchoredPosition = QueueAt(0, n - 1);
                    _moves.RemoveAll(m => m.Slot == dealt);
                    _moves.Add(new DealMove { Slot = dealt, Start = drt.anchoredPosition });
                }
            }
            float y = -top;
            for (int i = 0; i < n; i++)
            {
                if (!_slots.TryGetValue(cards[i].Id, out var s)) continue;
                var rt = s.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f);
                rt.pivot = new Vector2(.5f, 1f);
                float h = rt.rect.height;
                var to = NextTarget(i, h, spacing, ref y);
                SetShown(s, !(i == 0 && TopIsEquip) || IsMoving(s));   // 맨 위 카드 = 장착 칸이 그린다(올라가는 동안만 칸이 보인다)
                if (_gathering.Contains(s)) continue;   // 모이는 중인 칸은 모으기 연출이 움직인다
                if (IsMoving(s)) continue;              // 전달 이동 중인 칸은 StepMoves가 지금 목표를 따라 움직인다
                if (snap || !Animate) { DOTween.Kill(rt); rt.anchoredPosition = to; continue; }
                DOTween.Kill(rt);
                if (rt.anchoredPosition == to) continue;
                // 머지 결과(placed)는 모인 자리에서 출발한다.
                AnchorTo(rt, to, moveSec).SetEase(Ease.OutQuad).SetTarget(rt);
            }
            OrderSiblings();
        }

        bool IsMoving(HudWeaponSlot s)
        {
            foreach (var m in _moves) if (m.Slot == s) return true;
            return false;
        }

        /// <summary>칸의 지금 손패 목표 자리(Layout과 같은 계산)와 순서.</summary>
        bool TargetOf(HudWeaponSlot slot, out Vector2 to, out int index)
        {
            to = default;
            index = -1;
            if (_model == null) return false;
            float top = _layout ? _layout.padding.top : 0, spacing = _layout ? _layout.spacing : 0;
            float y = -top;
            var cards = _model.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                if (!_slots.TryGetValue(cards[i].Id, out var s)) continue;
                var at = NextTarget(i, s.rectTransform.rect.height, spacing, ref y);
                if (s == slot) { to = at; index = i; return true; }
            }
            return false;
        }

        /// <summary>
        /// 손패 맨 위 카드 = 장착 카드. 장착 칸은 따로 있는 무기가 아니라 손패에서 가장 높은 등급 카드를 위에 크게 강조해 보이는 자리다
        /// (사용자 2026-11-19 「장착한것도 카드야 별개가 아니라」 「무기 소지 제일 높은 등급이 제일위에 따로 강조해서 표시하는거고」 「무기 같은거 3개가 합쳐지는거고」).
        /// 그래서 맨 위 카드의 스택 칸은 장착 칸 자리에 숨겨 두고 스택은 2번째 카드부터 그린다. 머지는 장착 카드도 같은 카드로 센다(장착 1장 + 스택 2장 → 장착 칸으로 모인다).
        /// 장착 칸이 없으면 모두 스택에 그린다.
        /// </summary>
        bool TopIsEquip => _equip != null;

        /// <summary>손패 i번째 카드 목표 자리(스택 위 가운데 기준 anchoredPosition). y = 다음 스택 칸 위 끝, 스택 칸이면 내려간다.</summary>
        Vector2 NextTarget(int i, float h, float spacing, ref float y)
        {
            if (i == 0 && TopIsEquip) return EquipAt(h);
            var to = new Vector2(0, y);
            y -= h + spacing;
            return to;
        }

        static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>장착 칸 가운데에 스택 칸(높이 h, 위 가운데 피벗)을 겹치는 자리.</summary>
        Vector2 EquipAt(float h)
        {
            if (!_equip || !stack) return Vector2.zero;
            _equip.rectTransform.GetWorldCorners(Corners);
            var local = (Vector2)stack.InverseTransformPoint((Corners[0] + Corners[2]) * .5f) - new Vector2(stack.rect.center.x, stack.rect.yMax);
            return local + new Vector2(0, h * .5f);
        }

        /// <summary>스택에 보이는 장 수(맨 위 카드는 장착 칸이 그린다).</summary>
        int StackRows(int n) => TopIsEquip ? Math.Max(0, n - 1) : n;

        static void SetShown(HudWeaponSlot s, bool on)
        {
            if (s.gameObject.activeSelf != on) s.gameObject.SetActive(on);
        }

        static float OutQuad(float u) => 1f - (1f - u) * (1f - u);

        /// <summary>
        /// 전달 이동: 줄 자리에서 곧장 위로 자기 자리까지(<see cref="dealUpSec"/>). 목표는 매 프레임 다시 읽어 다음 카드가 이어서 들어와도 경로가 끊기지 않는다.
        /// </summary>
        void StepMoves(float dt)
        {
            for (int k = _moves.Count - 1; k >= 0; k--)
            {
                var m = _moves[k];
                if (!m.Slot || !TargetOf(m.Slot, out var to, out int idx)) { _moves.RemoveAt(k); continue; }
                m.T += dt;
                var rt = m.Slot.rectTransform;
                float u = dealUpSec > 0 ? Mathf.Clamp01(m.T / dealUpSec) : 1f;
                bool done = u >= 1f;
                rt.anchoredPosition = done ? to : Vector2.LerpUnclamped(m.Start, to, OutQuad(u));
                if (!done) continue;
                _moves.RemoveAt(k);
                if (idx == 0 && TopIsEquip) SetShown(m.Slot, false);   // 장착 칸에 닿으면 장착 칸이 그린다
            }
        }

        /// <summary>전달 이동을 모두 끝 자리로(되돌리기·즉시 배치).</summary>
        void FinishMoves()
        {
            foreach (var m in _moves)
                if (m.Slot && TargetOf(m.Slot, out var to, out int idx))
                {
                    m.Slot.rectTransform.anchoredPosition = to;
                    if (idx == 0 && TopIsEquip) SetShown(m.Slot, false);
                }
            _moves.Clear();
        }

        readonly List<HudWeaponSlot> _depth = new List<HudWeaponSlot>();
        readonly List<int> _depthRank = new List<int>();
        readonly List<float> _depthY = new List<float>();

        /// <summary>
        /// 스택 칸 깊이 = 지금 화면 높이: 위에 있는 칸이 앞(뒤 형제), 아래 칸이 뒤(디자이너 반대 순서 배치와 같은 겹침). 매 프레임 지금 자리로 다시 맞춘다:
        /// 새 카드가 맨 밑에서 올라가며 한 칸씩 지나칠 때마다 한 칸씩 앞으로 나오고, 밀려 내려가는 칸은 한 칸 뒤로 간다(사용자 2026-11-19 「위로 올라가면서 한칸씩 밀면서 뎁스도 한칸 앞으로」).
        /// 같은 높이면 손패 순서(위 카드가 앞). 스택 칸끼리의 형제 자리만 서로 바꾸고 줄·비행 카드 자리는 건드리지 않는다. 모이는 칸도 같은 규칙(바로 위 카드 뒤).
        /// </summary>
        void OrderSiblings()
        {
            if (_model == null) return;
            _depth.Clear();
            _depthRank.Clear();
            _depthY.Clear();
            var cards = _model.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                if (!_slots.TryGetValue(cards[i].Id, out var s)) continue;
                // 뒤(낮음) → 앞(높음) 순서로 끼워 넣는다. 모이는 3장은 모으는 자리 높이로 고정: 서로 앞뒤가 바뀌지 않고 맨 위 카드가 앞(같은 높이는 손패 위 순서가 앞).
                float y = _gathering.Contains(s) ? _gatherY : s.rectTransform.anchoredPosition.y;
                int at = _depth.Count;
                while (at > 0)
                {
                    float py = _depthY[at - 1];
                    if (py < y || py == y && _depthRank[at - 1] > i) break;
                    at--;
                }
                _depth.Insert(at, s);
                _depthRank.Insert(at, i);
                _depthY.Insert(at, y);
            }
            // 이미 뒤 → 앞 순서면 그대로 둔다(매 프레임 캔버스를 다시 짓지 않게).
            bool sorted = true;
            for (int k = 1; k < _depth.Count && sorted; k++)
                if (_depth[k].transform.GetSiblingIndex() < _depth[k - 1].transform.GetSiblingIndex()) sorted = false;
            if (sorted) return;
            // 뒤 → 앞 순서로 맨 뒤 형제로 보낸다. 형제 번호를 하나씩 바꾸면 앞서 맞춘 칸 번호가 밀려 깊이가 뒤섞인다(사용자 2026-11-19 「뎁스가 왜 뒤죽박죽이야 밑에 있는게 뒤여야」).
            // 줄 카드(맨 앞 형제로 들어감)·풀에 쉬는 칸은 스택 칸보다 앞 번호(= 뒤 깊이)에 남는다.
            foreach (var d in _depth) d.transform.SetAsLastSibling();
        }

        /// <summary>시험·QA: 스택 칸을 뒤 → 앞 깊이 순서로.</summary>
        public List<HudWeaponSlot> SlotsBackToFront()
        {
            var list = new List<HudWeaponSlot>(StackSlots);
            list.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            return list;
        }

        /// <summary>시험·QA: 장착 칸이 머지 결과 연출을 시작한 횟수.</summary>
        public int EquipMergedCount { get; private set; }

        /// <param name="mergedSec">0 이상이면 머지 결과가 장착 칸에 놓였다: 튐 대신 머지 결과 연출(<see cref="HudWeaponSlot.PlayMerged"/>: 크기·Flash·Sparkle).</param>
        void DrawEquip(bool pop, float mergedSec = -1)
        {
            if (!_equip || _model == null) return;
            int tier = _model.TopTier;
            TopTierShown?.Invoke(tier);
            _equip.Set(_t, tier, Mul());
            bool up = pop && tier > _equipTier;
            _equipTier = tier;
            if (up) MvSound.PlayUi(MvUiSound.HandTierUp);
            if (mergedSec >= 0)
            {
                EquipMergedCount++;
                _equip.ResetLook();
                _equip.PlayMerged(mergedSec, mergedScale);   // EditMode 시험도 시작 모습(덮개·반짝임 켜짐)을 볼 수 있게 Animate와 무관하게 건다(트윈은 재생 중에만 돈다)
                return;
            }
            if (!up || !Animate) return;
            var t = _equip.transform;
            DOTween.Kill(t);
            t.localScale = Vector3.one;
            DOTween.Sequence().SetTarget(t)
                .Append(t.DOScale(equipPopScale, equipPopSec * .6f).SetEase(Ease.OutQuad))
                .Append(t.DOScale(1f, equipPopSec * .4f).SetEase(Ease.InQuad));
        }

        // ---------------- 줄서기(MV-UI3-HUD9) ----------------

        float SlotHeight() => stackPrefab ? ((RectTransform)stackPrefab.transform).rect.height : 0;

        /// <summary>줄 k번째 자리: 지금 스택 장 수 기준.</summary>
        Vector2 QueueAt(int k) => QueueAt(k, _model != null ? _model.Cards.Count : 0);

        /// <summary>
        /// 줄 k번째 자리(스택 기준 anchoredPosition, 위 가운데 기준): 스택 n장일 때 맨 아래 칸의 아래 끝에서 칸 높이 × <see cref="queueGapRatio"/> 띄우고(겹치지 않음), 그 아래로 칸 높이 × <see cref="queueStepRatio"/>씩.
        /// 사용자 2026-11-19 「제일 마지막 무기에서 겹치지 않게 포지션 맞춰야해」.
        /// </summary>
        Vector2 QueueAt(int k, int n)
        {
            float h = SlotHeight();
            float top = _layout ? _layout.padding.top : 0, spacing = _layout ? _layout.spacing : 0;
            // 스택 맨 아래 칸 바로 밑(칸 높이 × queueGapRatio 띄움).
            int rows = StackRows(n);
            var first = new Vector2(0, rows > 0 ? -top - (rows - 1) * (h + spacing) - h - h * queueGapRatio : -top);
            if (queueAnchor && stack)
            {
                // 지정한 오브젝트 = 줄의 가장 높은 자리와 x(스택 위 가운데 기준 anchoredPosition). 스택이 길어져 그보다 내려오면 스택 밑을 따라 내려간다
                // (사용자 2026-11-19 「카드가 가는 위치가 너무 아래」: 스택이 짧을 때 멀리 떨어지지 않게).
                var local = (Vector2)stack.InverseTransformPoint(queueAnchor.position) - new Vector2(stack.rect.center.x, stack.rect.yMax);
                first = new Vector2(local.x, Mathf.Min(local.y, first.y));
            }
            return first - new Vector2(0, k * h * queueStepRatio);
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
        /// 화면 rect 자리의 카드들을 같은 자리·같은 크기 스택 칸으로 세우고(알파 1) 스택 밑 줄 자리로 날린다(인게임 무기문 카드, <see cref="MvHudFly.Launched"/>).
        /// 손패 지급은 연결이 <see cref="Deliver"/>로 넣은 받을 장을 <see cref="Feed"/>가 한다.
        /// </summary>
        public void LaunchFromScreen(IReadOnlyList<Rect> cards, int tier)
        {
            Init();
            if (_model == null || !stack || cards == null) return;
            var cam = Cam();
            float h = SlotHeight();
            float screenH = ScreenHeight(h, cam);
            var anchorRef = new Vector2(stack.rect.center.x, stack.rect.yMax);
            for (int i = 0; i < cards.Count; i++)
            {
                var r = cards[i];
                var s = _pool.Get();
                if (s.transform.parent != stack) s.transform.SetParent(stack, false);
                s.transform.SetAsFirstSibling();   // 등장 깊이 = 맨 뒤(스택 카드 뒤로 지나 줄에 선다)
                s.Set(_t, tier, Mul());
                var rt = s.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f);
                rt.pivot = new Vector2(.5f, 1f);
                float scale = screenH > 0 && r.height > 0 ? r.height / screenH : 1f;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(stack, r.center, cam, out var local);
                rt.anchoredPosition = local - anchorRef + new Vector2(0, h * scale * .5f);
                s.transform.localScale = new Vector3(scale, scale, 1f);
                Enqueue(s, i * launchGapSec);
            }
        }

        /// <summary>
        /// 손패 밖 칸(<see cref="RentLoose"/>)을 지금 보이는 자리·크기 그대로 스택 아래로 옮겨 줄 맨 뒤 자리로 날린다(로비 Dst → 줄).
        /// 받을 장은 연결이 <see cref="Deliver"/>로 따로 넣는다.
        /// </summary>
        public void JoinQueue(HudWeaponSlot s, float delay = 0)
        {
            Init();
            if (!s) return;
            if (_model == null || !stack) { ReleaseLoose(s); return; }
            var rt = s.rectTransform;
            rt.GetWorldCorners(Corners);
            var center = (Corners[0] + Corners[2]) * .5f;
            float scale = stack.lossyScale.y != 0 ? s.transform.lossyScale.y / stack.lossyScale.y : 1f;
            DOTween.Kill(rt);
            DOTween.Kill(s.transform);
            s.transform.SetParent(stack, false);
            s.transform.SetAsFirstSibling();   // 등장 깊이 = 맨 뒤(스택 카드 뒤로 지나 줄에 선다)
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f);
            rt.pivot = new Vector2(.5f, 1f);
            var local = (Vector2)stack.InverseTransformPoint(center) - new Vector2(stack.rect.center.x, stack.rect.yMax);
            rt.anchoredPosition = local + new Vector2(0, rt.rect.height * scale * .5f);
            s.transform.localScale = new Vector3(scale, scale, 1f);
            Enqueue(s, delay);
        }

        /// <summary>줄 맨 뒤에 세우고 지금 자리에서 줄 자리로 곡선 비행(<see cref="launchSec"/>·<see cref="launchArc"/>).</summary>
        void Enqueue(HudWeaponSlot s, float delay)
        {
            var rt = s.rectTransform;
            var q = new Queued { Slot = s };
            _queue.Add(q);
            _queueIdle = 0;
            var to = QueueAt(_queue.Count - 1);
            if (!Animate)
            {
                rt.anchoredPosition = to;
                s.transform.localScale = Vector3.one;
                q.Arrived = true;
                return;
            }
            var from = rt.anchoredPosition;
            DOTween.Sequence().SetTarget(rt)
                .Insert(delay, Arc(from, to, v => rt.anchoredPosition = v))
                .Insert(delay, s.transform.DOScale(1f, launchSec).SetEase(Ease.InOutQuad))
                .OnComplete(() => { q.Arrived = true; LayoutQueue(); });
        }

        /// <summary>
        /// 같은 비행 곡선(<see cref="launchSec"/>·<see cref="launchArc"/>)으로 월드 자리 <paramref name="to"/>까지 옮긴다(로비 Spwan → Dst).
        /// 크기는 그대로. 실시간.
        /// </summary>
        public Tween FlyTo(Transform t, Vector3 to, float delay = 0)
        {
            var from = t.position;
            return DOTween.Sequence().SetTarget(t)
                .Insert(delay, ArcWorld(from, to, v => t.position = v));
        }

        /// <summary>출발 → 도착 2차 곡선(진행 방향의 수직, 화면 오른쪽으로 부풂, 휨 = 거리 × <see cref="launchArc"/>).</summary>
        Tween Arc(Vector2 from, Vector2 to, Action<Vector2> set)
        {
            var dir = to - from;
            var bulge = new Vector2(dir.y, -dir.x);                 // 진행 방향의 수직
            if (bulge.x < 0) bulge = -bulge;                         // 바깥(화면 오른쪽)으로 부푼다
            var ctrl = (from + to) * .5f + bulge * launchArc;        // |bulge| = 거리
            return DOTween.To(() => 0f, u =>
            {
                float a = 1f - u;
                set(a * a * from + 2f * a * u * ctrl + u * u * to);
            }, 1f, launchSec).SetEase(Ease.InOutQuad);
        }

        Tween ArcWorld(Vector3 from, Vector3 to, Action<Vector3> set)
        {
            float z = from.z;
            return Arc((Vector2)from, (Vector2)to, v => set(new Vector3(v.x, v.y, z)));
        }

        /// <summary>스택 안 높이 h를 화면 px 높이로.</summary>
        float ScreenHeight(float h, Camera cam)
        {
            var p0 = RectTransformUtility.WorldToScreenPoint(cam, stack.TransformPoint(Vector3.zero));
            var p1 = RectTransformUtility.WorldToScreenPoint(cam, stack.TransformPoint(new Vector3(0, h, 0)));
            return Vector2.Distance(p0, p1);
        }

        /// <summary>다음 카드가 들어갈 자리(스택 맨 아래 칸 다음)의 화면 가운데·크기(인게임 비행 층 도착점).</summary>
        public bool TryGetNextSlotScreenRect(out Vector2 center, out Vector2 size)
        {
            center = size = default;
            if (!stack) return false;
            float h = SlotHeight();
            float top = _layout ? _layout.padding.top : 0, spacing = _layout ? _layout.spacing : 0;
            int n = StackRows(_model != null ? _model.Cards.Count : 0);
            var local = new Vector2(stack.rect.center.x, stack.rect.yMax - top - n * (h + spacing) - h * .5f);
            var cam = Cam();
            center = RectTransformUtility.WorldToScreenPoint(cam, stack.TransformPoint(local));
            var p0 = RectTransformUtility.WorldToScreenPoint(cam, stack.TransformPoint(local - new Vector2(h, h) * .5f));
            var p1 = RectTransformUtility.WorldToScreenPoint(cam, stack.TransformPoint(local + new Vector2(h, h) * .5f));
            size = new Vector2(Mathf.Abs(p1.x - p0.x), Mathf.Abs(p1.y - p0.y));
            return true;
        }

        public Camera Cam()
        {
            var c = GetComponentInParent<Canvas>();
            c = c ? c.rootCanvas : null;
            return c && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
        }
    }
}
