using System;
using System.Collections.Generic;
using jjevol;
using MV.Data;
using MV.Run;
using MV.View;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 얻은 카드 섹션(<c>Mid/Card Pos</c>, MV-UI3-HUD7, HUD_구조_설계안 3.3 CardPosSection).
    /// 런에서 얻은 카드(<see cref="RunState.CardOrder"/>)를 Holder 자리에 순서대로 보인다(Holder = 디자이너 <c>Holder_Card</c>, 모자라면 첫 Holder 복제).
    /// 룰렛에서 확정된 카드는 출발 카드 자리에서 그 Holder로 난다. 비행 시간은 이 섹션 프리팹 값(<see cref="FlySec"/>)이 정본이고,
    /// 도착하면 <see cref="RouletteCardFlight"/>로 시뮬에 알려 카드가 그 순간 적용된다(MV-UI3-CLIMBFX3, 같은 값을 표에 두지 않는다).
    /// 나는 카드는 <c>Card_Skill.prefab</c> 에셋을 원본으로 <see cref="ObjectPool{T}"/>에서 꺼낸다.
    /// </summary>
    public sealed class CardPosSection : UIBase
    {
        [Tooltip("나는 카드 복제 원본: Card_Skill.prefab 에셋.")]
        [SerializeField] HudSkillCard flyPrefab;
        [Tooltip("나는 카드를 둘 부모(화면 루트, 다른 섹션 위)")]
        [SerializeField] RectTransform flyParent;
        [Header("비행(MV-UI3-CLIMBFX3). 도착하는 순간 시뮬이 카드를 적용한다")]
        [Tooltip("비행 시간(초, 실시간·배속과 무관). 기본값 = 기획서 04 비행 620ms")]
        [Min(0)]
        [SerializeField] float flySec = 0.62f;
        [Tooltip("위치 보간 곡선")]
        [SerializeField] HudEase flyMoveEase = HudEase.OutCubic;
        [Tooltip("크기 보간 곡선")]
        [SerializeField] HudEase flyScaleEase = HudEase.OutCubic;

        RectTransform _holderTemplate;
        readonly List<HudSkillCard> _posCards = new List<HudSkillCard>();
        /// <summary>Holder마다 중복 배지(<c>Holder_Card/Badge_Override</c>, 없으면 null). <see cref="_posCards"/>와 같은 순서.</summary>
        readonly List<HudCountBadge> _badges = new List<HudCountBadge>();
        readonly List<HudSkillCard> _flying = new List<HudSkillCard>();
        /// <summary>날고 있는 카드 id(아직 <see cref="RunState.CardOrder"/>에 없는 새 카드의 자리 예약, 도착하면 뺀다).</summary>
        readonly List<int> _flyingIds = new List<int>();
        /// <summary>도착한 카드와 도착 때의 Holder 그림 표시(<see cref="_stamp"/>). 시뮬이 적용해 Holder가 다시 그려지면 반납한다(한 프레임 빈자리 방지).</summary>
        readonly List<(HudSkillCard card, int id, int stamp)> _landed = new List<(HudSkillCard, int, int)>();
        ObjectPool<HudSkillCard> _pool;
        int _stamp = -1;
        bool _ready;

        public IReadOnlyList<HudSkillCard> PosCards { get { Init(); return _posCards; } }
        public IReadOnlyList<HudCountBadge> Badges { get { Init(); return _badges; } }
        public HudSkillCard FlyPrefab => flyPrefab;
        public int FlyingCount => _flying.Count;
        public float FlySec => flySec;

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            foreach (Transform h in transform)
            {
                var card = h.GetComponentInChildren<HudSkillCard>(true);
                if (!card) continue;
                if (!_holderTemplate) _holderTemplate = (RectTransform)h;
                card.gameObject.SetActive(false);
                _posCards.Add(card);
                _badges.Add(BadgeOf(h));
            }
            if (!flyPrefab) Debug.LogError("[CardPosSection] flyPrefab(Card_Skill.prefab 에셋)이 비어 있습니다.", this);
            _pool = new ObjectPool<HudSkillCard>(
                () => Instantiate(flyPrefab, flyParent ? flyParent : (RectTransform)transform, false),
                c => c.gameObject.SetActive(true),
                c => { HudRectMotion.Kill(c.Rect); c.gameObject.SetActive(false); },
                c => { if (c) Discard(c.gameObject); },
                collectionCheck: false);
        }

        static void Discard(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _pool?.Dispose();
        }

        public void ResetRun()
        {
            Init();
            foreach (var c in _flying) _pool.Release(c);
            _flying.Clear();
            _flyingIds.Clear();
            _landed.Clear();
            _stamp = -1;
            foreach (var c in _posCards) c.gameObject.SetActive(false);
            foreach (var b in _badges) if (b) b.Show(0);
        }

        /// <summary>얻은 카드가 바뀌면 Holder 카드를 다시 그린다.</summary>
        public void Tick(RunState s)
        {
            Init();
            if (s == null) return;
            int stamp = s.CardOrder.Count;
            foreach (var n in s.Cards.Values) stamp = stamp * 31 + n;
            if (stamp == _stamp) return;
            _stamp = stamp;
            ReleaseLanded();
            var t = s.Tables;
            for (int i = 0; i < s.CardOrder.Count; i++)
            {
                var card = PosCard(i);
                if (!card) break;
                card.gameObject.SetActive(true);
                card.ShowCard(t, t?.Card.Get(s.CardOrder[i]));
                var badge = _badges[i];
                if (badge) badge.Show(s.Cards.TryGetValue(s.CardOrder[i], out int n) ? n : 1);
            }
            for (int i = s.CardOrder.Count; i < _posCards.Count; i++)
            {
                _posCards[i].gameObject.SetActive(false);
                if (_badges[i]) _badges[i].Show(0);
            }
        }

        /// <summary>도착한 뒤 Holder 그림이 바뀐(시뮬이 카드를 적용한) 비행 카드를 반납한다.</summary>
        void ReleaseLanded()
        {
            for (int i = _landed.Count - 1; i >= 0; i--)
            {
                var (card, id, stamp) = _landed[i];
                if (stamp == _stamp) continue;
                _landed.RemoveAt(i);
                if (!card || !_flying.Remove(card)) continue;
                _flyingIds.Remove(id);
                _pool.Release(card);
            }
        }

        /// <summary>
        /// 확정 카드가 from 자리에서 Holder로 난다(시퀀스 6). 시간 = <see cref="FlySec"/>(실시간).
        /// 날기 시작하면 그 비행을 맡고(<see cref="RouletteCardFlight.Claim"/>), 도착하면 시뮬에 알린다. 날지 못하면 맡지 않아 런 프레임이 바로 적용한다.
        /// </summary>
        public void Fly(RunState s, RouletteCard k, HudSkillCard from)
        {
            Init();
            if (s == null || k?.Pick == null || !flyPrefab) return;
            int index = s.Roulette != null ? s.Roulette.Cards.IndexOf(k) : -1;
            if (index < 0) return;
            int id = k.Pick.id;
            var target = PosCard(SlotFor(s.CardOrder, _flyingIds, id));
            if (!target) return;
            int run = RouletteCardFlight.Run;
            RouletteCardFlight.Claim(index);
            var clone = _pool.Get();
            clone.transform.SetAsLastSibling();
            clone.ShowCard(s.Tables, k.Pick);
            if (from) HudRectMotion.Place(clone.Rect, from.Rect);
            _flying.Add(clone);
            _flyingIds.Add(id);
            HudRectMotion.Move(clone.Rect, target.Rect, Math.Max(0f, flySec), () =>
            {
                // 도착 자리에서 기다리다 시뮬이 적용해 Holder가 다시 그려지면 반납한다(ReleaseLanded).
                if (clone && _flying.Contains(clone)) _landed.Add((clone, id, _stamp));
                RouletteCardFlight.RaiseArrived(run, index);
            }, flyMoveEase, flyScaleEase);
        }

        /// <summary>
        /// 카드 id가 놓일 Holder 번호. 시뮬은 도착(적용) 때 <see cref="RunState.CardOrder"/>에 넣으므로, 이미 얻은 카드는 그 자리,
        /// 새 카드는 「얻은 카드 수 + 먼저 날고 있는 다른 새 카드 수」번째 자리다(앞 카드가 도착 전이어도 같은 자리로 겹치지 않게, MV-UI3-CLIMBFX2).
        /// 같은 새 카드가 이미 날고 있으면 그 카드와 같은 자리.
        /// </summary>
        public static int SlotFor(IReadOnlyList<int> order, IReadOnlyList<int> flyingIds, int id)
        {
            for (int i = 0; i < order.Count; i++) if (order[i] == id) return i;
            int slot = order.Count;
            var seen = new List<int>();
            foreach (int f in flyingIds)
            {
                if (seen.Contains(f)) continue;
                bool owned = false;
                for (int i = 0; i < order.Count; i++) if (order[i] == f) { owned = true; break; }
                if (owned) continue;
                if (f == id) return slot;
                seen.Add(f);
                slot++;
            }
            return slot;
        }

        /// <summary>Holder의 중복 배지. 디자이너 Holder_Card에 없으면 경고하고 null(배지 없이 카드만 보인다).</summary>
        HudCountBadge BadgeOf(Transform holder)
        {
            var b = holder.GetComponentInChildren<HudCountBadge>(true);
            if (!b) Debug.LogWarning($"[CardPosSection] {holder.name}에 Badge_Override(HudCountBadge)가 없습니다: Assets/Game/Art/UI/Prefab/UIScreen/IngameScreen.prefab", this);
            return b;
        }

        /// <summary>index번째 Holder 카드(Holder_Card가 모자라면 첫 Holder를 복제).</summary>
        HudSkillCard PosCard(int index)
        {
            bool added = false;
            while (_posCards.Count <= index && _holderTemplate)
            {
                var h = Instantiate(_holderTemplate, transform, false);
                var card = h.GetComponentInChildren<HudSkillCard>(true);
                if (!card) break;
                card.gameObject.SetActive(false);
                _posCards.Add(card);
                var badge = BadgeOf(h);
                if (badge) badge.Show(0);
                _badges.Add(badge);
                added = true;
            }
            // 새 Holder는 레이아웃(Card Pos의 LayoutGroup)이 다음 갱신 때 자리를 잡는다. 지금 자리를 잡아야 비행 출발 때 첫 Holder 자리로 가지 않는다.
            if (added) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
            return index < _posCards.Count ? _posCards[index] : null;
        }
    }
}
