using System;
using System.Collections.Generic;
using jjevol;
using MV.Data;
using MV.Run;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 얻은 카드 섹션(<c>Mid/Card Pos</c>, MV-UI3-HUD7, HUD_구조_설계안 3.3 CardPosSection).
    /// 런에서 얻은 카드(<see cref="RunState.CardOrder"/>)를 Holder 자리에 순서대로 보인다(Holder = 디자이너 <c>Holder_Card</c>, 모자라면 첫 Holder 복제).
    /// 룰렛에서 확정된 카드는 출발 카드 자리에서 그 Holder로 난다. 비행 시간 = 시뮬 남은 비행 시간(<see cref="RouletteCard.FlyLeft"/>, 330 rlFlyMs)으로 같은 값을 두 곳에 두지 않는다.
    /// 나는 카드는 <c>Card_Skill.prefab</c> 에셋을 원본으로 <see cref="ObjectPool{T}"/>에서 꺼낸다.
    /// </summary>
    public sealed class CardPosSection : UIBase
    {
        [Tooltip("나는 카드 복제 원본: Card_Skill.prefab 에셋.")]
        [SerializeField] HudSkillCard flyPrefab;
        [Tooltip("나는 카드를 둘 부모(화면 루트, 다른 섹션 위)")]
        [SerializeField] RectTransform flyParent;
        [Header("비행 보간(MV-UI3-CLIMBFX). 시간은 시뮬 330 rlFlyMs")]
        [Tooltip("위치 보간 곡선")]
        [SerializeField] HudEase flyMoveEase = HudEase.OutCubic;
        [Tooltip("크기 보간 곡선")]
        [SerializeField] HudEase flyScaleEase = HudEase.OutCubic;

        RectTransform _holderTemplate;
        readonly List<HudSkillCard> _posCards = new List<HudSkillCard>();
        readonly List<HudSkillCard> _flying = new List<HudSkillCard>();
        /// <summary>날고 있는 카드 id(아직 <see cref="RunState.CardOrder"/>에 없는 새 카드의 자리 예약, 도착하면 뺀다).</summary>
        readonly List<int> _flyingIds = new List<int>();
        ObjectPool<HudSkillCard> _pool;
        int _stamp = -1;
        bool _ready;

        public IReadOnlyList<HudSkillCard> PosCards { get { Init(); return _posCards; } }
        public HudSkillCard FlyPrefab => flyPrefab;
        public int FlyingCount => _flying.Count;

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
            _stamp = -1;
            foreach (var c in _posCards) c.gameObject.SetActive(false);
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
            var t = s.Tables;
            for (int i = 0; i < s.CardOrder.Count; i++)
            {
                var card = PosCard(i);
                if (!card) break;
                card.gameObject.SetActive(true);
                card.ShowCard(t, t?.Card.Get(s.CardOrder[i]));
            }
            for (int i = s.CardOrder.Count; i < _posCards.Count; i++) _posCards[i].gameObject.SetActive(false);
        }

        /// <summary>확정 카드가 from 자리에서 Holder로 난다(시퀀스 6). 시간 = 시뮬 남은 비행 시간(실시간).</summary>
        public void Fly(RunState s, RouletteCard k, HudSkillCard from)
        {
            Init();
            if (s == null || k?.Pick == null || !flyPrefab) return;
            int id = k.Pick.id;
            var target = PosCard(SlotFor(s.CardOrder, _flyingIds, id));
            if (!target) return;
            var clone = _pool.Get();
            clone.transform.SetAsLastSibling();
            clone.ShowCard(s.Tables, k.Pick);
            if (from) HudRectMotion.Place(clone.Rect, from.Rect);
            _flying.Add(clone);
            _flyingIds.Add(id);
            HudRectMotion.Move(clone.Rect, target.Rect, (float)Math.Max(0, k.FlyLeft), () =>
            {
                if (!clone || !_flying.Remove(clone)) return;
                _flyingIds.Remove(id);
                _pool.Release(clone);
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
                added = true;
            }
            // 새 Holder는 레이아웃(Card Pos의 LayoutGroup)이 다음 갱신 때 자리를 잡는다. 지금 자리를 잡아야 비행 출발 때 첫 Holder 자리로 가지 않는다.
            if (added) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
            return index < _posCards.Count ? _posCards[index] : null;
        }
    }
}
