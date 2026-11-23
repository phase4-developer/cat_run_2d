using System;
using System.Collections.Generic;
using jjevol;
using MV.Data;
using MV.Run;
using UnityEngine;
using UnityEngine.Pool;

namespace MV.UI
{
    /// <summary>
    /// 계단 등급 카드 섹션(<c>ClimbStair/Grade Step Section</c>, MV-UI3-HUD6, HUD_구조_설계안 3.3 GradeStepSection).
    /// 다섯 자리(Out → Prev → Current → Next → In)의 <c>Card_Class</c> 카드. 등급이 오르면 한 칸씩 밀린다.
    /// 카드는 <c>Card_Class.prefab</c> 에셋을 원본으로 <see cref="ObjectPool{T}"/>에서 꺼낸다(디자이너 예시 카드는 실행 중에만 숨김).
    /// 자리·크기는 Holder 사이 보간(<see cref="HudRectMotion"/>), 등급·진행은 시뮬 계단 상태(<see cref="ClimbState"/>)를 읽기만 한다.
    /// </summary>
    public sealed class GradeStepSection : UIBase
    {
        [SerializeField] RectTransform holderOut;
        [SerializeField] RectTransform holderPrev;
        [SerializeField] RectTransform holderCurrent;
        [SerializeField] RectTransform holderNext;
        [SerializeField] RectTransform holderIn;
        [Tooltip("카드 복제 원본: Card_Class.prefab 에셋.")]
        [SerializeField] HudClimbCard cardPrefab;
        [Tooltip("한 칸 이동 초(1배속 기준, 배속만큼 빨라진다)")]
        [SerializeField] float shiftSec = 0.3f;

        ObjectPool<HudClimbCard> _pool;
        readonly List<HudClimbCard> _cards = new List<HudClimbCard>();
        ClimbState _climb;
        int _cur = int.MinValue;
        bool _ready;

        /// <summary>Current 등급이 바뀌었다(스킬 섹션이 「?」 카드를 그 등급으로 바꾼다).</summary>
        public event Action<int> CurrentChanged;

        public IReadOnlyList<HudClimbCard> Cards => _cards;
        public HudClimbCard CardPrefab => cardPrefab;
        public int Current => _cur;

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            // 디자이너 예시 카드는 프리팹에 두고 실행 중에만 숨긴다(설계안 7절 결정).
            foreach (var h in new[] { holderOut, holderPrev, holderCurrent, holderNext, holderIn })
                if (h) foreach (var c in h.GetComponentsInChildren<HudClimbCard>(true)) c.gameObject.SetActive(false);
            if (!cardPrefab) Debug.LogError("[GradeStepSection] cardPrefab(Card_Class.prefab 에셋)이 비어 있습니다.", this);
            _pool = new ObjectPool<HudClimbCard>(
                () =>
                {
                    var c = Instantiate(cardPrefab, transform, false);
                    var rt = c.Rect;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
                    return c;
                },
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
            foreach (var c in _cards) _pool.Release(c);
            _cards.Clear();
            _climb = null;
            _cur = int.MinValue;
        }

        /// <summary>계단 중이면 등급 카드를 맞춘다. 이동 시간은 게임 시간(배속 = Time.timeScale, MV-CLOCK-2)이라 따로 나누지 않는다.</summary>
        public void Tick(RunState s)
        {
            Init();
            var t = s?.Tables;
            var c = s != null && s.Phase == RunPhase.Climb ? s.Climb : null;
            if (c == null) { _climb = null; return; }
            if (c != _climb) { _climb = c; _cur = int.MinValue; }
            int cur = HudGrade.ClimbCurrent(t, c.Tier, c.Rem, out float fill);
            if (cur < 0) return;
            if (cur != _cur)
            {
                Shift(t, cur, _cur == int.MinValue || cur < _cur, shiftSec);
                _cur = cur;
                CurrentChanged?.Invoke(cur);
            }
            foreach (var card in _cards)
            {
                card.SetFill(card.Tier < cur ? 1 : card.Tier > cur ? 0 : fill);
                card.ShowProgress(card.Tier == cur);
            }
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
        /// 등급이 오르면 카드가 한 칸씩 밀린다(Prev → Out으로 사라짐, Current → Prev, Next → Current, In에서 새 카드 → Next).
        /// snap이면(시작·카드 확정 뒤 다시 세기) 바로 놓는다.
        /// </summary>
        void Shift(MvTables t, int cur, bool snap, float sec)
        {
            int max = HudGrade.CardTierMax(t);
            if (snap)
            {
                foreach (var c in _cards) _pool.Release(c);
                _cards.Clear();
            }
            for (int i = _cards.Count - 1; i >= 0; i--)
            {
                var card = _cards[i];
                int slot = card.Tier - cur;
                if (slot >= -1) { HudRectMotion.Move(card.Rect, HolderFor(slot), sec, null); continue; }
                _cards.RemoveAt(i);
                HudRectMotion.Move(card.Rect, holderOut, sec, () => _pool.Release(card));
            }
            for (int tier = Math.Max(0, cur - 1); tier <= Math.Min(max, cur + 1); tier++)
            {
                if (_cards.Exists(c => c.Tier == tier)) continue;
                if (!cardPrefab) break;
                var card = _pool.Get();
                card.Set(t, tier);
                _cards.Add(card);
                if (snap) { HudRectMotion.Place(card.Rect, HolderFor(tier - cur)); continue; }
                HudRectMotion.Place(card.Rect, holderIn);
                HudRectMotion.Move(card.Rect, HolderFor(tier - cur), sec, null);
            }
        }
    }
}
