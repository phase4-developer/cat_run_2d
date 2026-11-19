using System;
using System.Collections.Generic;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 손패 연출 시간 값(04 §4.10, 05 §5.3.2 기본값). 연출 값이라 표가 아니라 무기 섹션 프리팹의 직렬화 필드에 둔다
    /// (2026-11-19 사용자 결정, 300 <c>ui.hand.*</c> 삭제, MV-UI3-HUD3). 머지 장수는 규칙 값이라 표(<c>MERGE_N</c>)에서 받는다.
    /// </summary>
    [Serializable]
    public sealed class WeaponHandTiming
    {
        [Tooltip("한 묶음(전달+머지)을 이 초 안에 끝냄 (L1162)")] public float budget = 3f;
        [Tooltip("남은 예산 하한 (L1171)")] public float leftMin = 0.4f;
        [Tooltip("1장 전달 = 이 단위 (L1172)")] public float dealCost = 0.12f;
        [Tooltip("1단위 시간 하한 (L1173)")] public float unitMin = 0.07f;
        [Tooltip("1단위 시간 상한 (L1173)")] public float unitMax = 0.54f;
        [Tooltip("전달 간격 = min(dealGapMax, u × dealGapMul) (L1180)")] public float dealGapMax = 0.05f;
        [Tooltip("전달 간격 계수 (L1180)")] public float dealGapMul = 0.12f;
        [Tooltip("머지 시간 md = u × mergeMul (L1188)")] public float mergeMul = 0.92f;
        [Tooltip("모으는 이동 = md × moveMul (L1189)")] public float moveMul = 0.9f;
        [Tooltip("머지 뒤 쉼 = min(afterMax, u × afterMul) (L1194)")] public float afterMax = 0.04f;
        [Tooltip("머지 뒤 쉼 계수 (L1194)")] public float afterMul = 0.08f;
    }

    /// <summary>손패 카드 1장. Id가 작을수록 먼저 받은 카드.</summary>
    public readonly struct HandCard
    {
        public readonly int Id;
        public readonly int Tier;
        public HandCard(int id, int tier) { Id = id; Tier = tier; }
    }

    public enum HandActionKind
    {
        /// <summary>대기열 1장이 손패에 들어왔다(<see cref="HandAction.Card"/>).</summary>
        Dealt,
        /// <summary>같은 등급 MERGE_N장이 모이기 시작한다(<see cref="HandAction.Gathered"/>, 가운데 = 인덱스 1). 시간 = Duration(md), 이동 = MoveSec.</summary>
        GatherStart,
        /// <summary>모인 카드가 빠지고 다음 등급 1장(<see cref="HandAction.Card"/>)이 들어왔다.</summary>
        Merged,
    }

    /// <summary>모델이 낸 일 1건. 섹션이 이것으로 슬롯 연출을 고른다.</summary>
    public readonly struct HandAction
    {
        public readonly HandActionKind Kind;
        public readonly HandCard Card;
        public readonly int[] Gathered;
        /// <summary>이 일에 걸리는 시간(전달 간격·md·머지 뒤 쉼).</summary>
        public readonly float Duration;
        /// <summary>모으기 이동 시간(md × moveMul). GatherStart에만.</summary>
        public readonly float MoveSec;
        /// <summary>이 일을 낼 때의 1단위 시간 u(머지 결과 연출 max(0.15, u) 등에 쓴다).</summary>
        public readonly float Unit;
        /// <summary>이 일로 손패 최고 등급(장착)이 올랐다.</summary>
        public readonly bool TopTierUp;

        public HandAction(HandActionKind kind, HandCard card, int[] gathered, float duration, float moveSec, float unit, bool topTierUp)
        {
            Kind = kind; Card = card; Gathered = gathered; Duration = duration; MoveSec = moveSec; Unit = unit; TopTierUp = topTierUp;
        }
    }

    /// <summary>
    /// 손패 3장 머지 표시 모델(04 §4.10 handReset·handDeliver·handSort·handTier·handWork·handUnit·stepHand, MV-UI3-HUD3).
    /// 시뮬은 즉시 머지하고 <c>WeaponDeliver{n, tier}</c>만 낸다. 이 모델은 그 결과를 실시간으로 나눠 보이는 순서·시간만 정하고 그림은 모른다.
    /// Unity 시간을 읽지 않는다: 섹션이 지금 시각과 dt를 넘긴다.
    /// </summary>
    public sealed class WeaponHandModel
    {
        struct Job
        {
            public bool Deal;
            public int N, Tier;
        }

        readonly WeaponHandTiming _timing;
        readonly int _mergeN;
        readonly int _maxTier;
        readonly List<HandCard> _cards = new List<HandCard>();
        /// <summary>카드 Id → 제자리에 정렬되는 시각(전달 카드만, 없으면 이미 제자리).</summary>
        readonly Dictionary<int, double> _readyAt = new Dictionary<int, double>();
        readonly List<Job> _queue = new List<Job>();
        int _nextId = 1;
        double _t0;
        /// <summary>다음 전달까지 남은 초(전달 간격).</summary>
        double _dealWait;
        /// <summary>모으는 중 = 머지 끝까지, 아니면 머지 뒤 쉼이 끝나기까지 남은 초.</summary>
        double _mergeWait;
        double _settle;
        int[] _gather;
        int _gatherTier;

        /// <param name="mergeN">300 <c>MERGE_N</c>.</param>
        /// <param name="maxTier">103_item 최고 단계(이 단계는 머지하지 않고 쌓인다).</param>
        public WeaponHandModel(WeaponHandTiming timing, int mergeN, int maxTier)
        {
            _timing = timing ?? throw new ArgumentNullException(nameof(timing));
            if (mergeN < 2) throw new ArgumentOutOfRangeException(nameof(mergeN), mergeN, "MERGE_N ≥ 2");
            if (maxTier < 1) throw new ArgumentOutOfRangeException(nameof(maxTier), maxTier, "최고 단계 ≥ 1");
            _mergeN = mergeN;
            _maxTier = maxTier;
        }

        /// <summary>정렬된 손패(등급 내림차순, 같은 등급은 먼저 받은 것이 앞).</summary>
        public IReadOnlyList<HandCard> Cards => _cards;
        /// <summary>
        /// 전달 연출 초(줄 자리에서 곧장 위로 합류). 다음 전달도 머지도 이 시간을 기다리지 않는다(사용자 2026-11-19 「딜레이만 살짝있게 연속으로」, 「3개 배치되면 그때 바로 머지」).
        /// <see cref="Settling"/>으로 남은 이동 시간만 알려 준다. 섹션이 프리팹 연출 값으로 넣는다.
        /// </summary>
        public float DealStraightSec { get; set; }

        /// <summary>전달한 카드가 모두 자리를 찾기까지 남은 초(머지는 기다리지 않는다).</summary>
        public double Settling => _settle;

        /// <summary>대기열이 비고 모으는 중·머지 뒤 쉼이 아니며 더 머지할 것이 없다.</summary>
        public bool Idle => _queue.Count == 0 && _gather == null && _mergeWait <= 0 && NextMergeTier() < 1;
        /// <summary>handTier: 손패 최고 등급(비면 1). 장착 칸·고양이 외형은 이 값.</summary>
        public int TopTier => _cards.Count > 0 ? _cards[0].Tier : 1;

        /// <summary>handReset: 런 시작 손패. counts[t] = 단계 t 장수(인덱스 0은 안 씀), 높은 단계부터 만든다.</summary>
        public void Reset(IReadOnlyList<int> counts, double now)
        {
            _cards.Clear();
            _queue.Clear();
            _gather = null;
            _dealWait = 0;
            _mergeWait = 0;
            _settle = 0;
            _readyAt.Clear();
            _t0 = now;
            _nextId = 1;
            if (counts == null) return;
            for (int t = Math.Min(counts.Count - 1, _maxTier); t >= 1; t--)
                for (int k = 0; k < counts[t]; k++) _cards.Add(new HandCard(_nextId++, t));
            Sort();
        }

        /// <summary>
        /// handDeliver: 대기열에 전달 1장(단계 t)을 n번 넣는다. 전달(줄 → 위로 → 제자리 정렬)은 머지와 상관없이 전달 간격(dealGap)으로 시간차로 이어진다.
        /// 머지는 카드가 제자리에 정렬되는 순간(<see cref="DealStraightSec"/> 뒤) 위에서부터 세어 같은 등급 연속 3장이 모두 제자리에 있으면 한 번에 1개씩 일어난다
        /// (사용자 2026-11-19 「카드 아래에 와서 위로 올라가 올라가서 장착칸으로 가서 정렬되 이 동작은 그냥 시간차로 이루어져 그리고 정렬되는 타이밍에 카드가 순서대로 3장이 같은거면
        /// 3개중 제일 위 카드로 합쳐지면서 머지가 일어나고 뒤에 있던 카드들이 정렬돼」 「머지는 한번에 한개씩 발동」 「정렬되는건 머지하고 상관없이 시간차」 「위에서부터 3개 머지 세어야지」).
        /// </summary>
        public void Deliver(int n, int tier)
        {
            if (n <= 0) return;
            tier = Math.Max(1, Math.Min(_maxTier, tier));
            for (int i = 0; i < n; i++)
            {
                _queue.Add(new Job { Deal = true, N = 1, Tier = tier });
            }
        }

        /// <summary>handUnit: 지금 1단위 시간 u.</summary>
        public float Unit(double now)
        {
            double left = Math.Max(_timing.leftMin, _timing.budget - (now - _t0));
            Work(out int deals, out int merges);
            double u = merges + deals * (double)_timing.dealCost;
            if (u <= 0) return _timing.unitMax;
            return (float)Math.Max(_timing.unitMin, Math.Min(_timing.unitMax, left / u));
        }

        /// <summary>handWork: 손패 + 대기 전달을 합쳐 끝까지 머지했을 때 남은 전달 장수·머지 수.</summary>
        public void Work(out int deals, out int merges)
        {
            var c = new int[_maxTier + 2];
            foreach (var card in _cards) c[card.Tier]++;
            deals = 0;
            foreach (var j in _queue)
                if (j.Deal) { c[j.Tier] += j.N; deals += j.N; }
            merges = 0;
            for (int k = 1; k < _maxTier; k++)
            {
                int m = c[k] / _mergeN;
                c[k + 1] += m;
                c[k] -= _mergeN * m;
                merges += m;
            }
        }

        /// <summary>stepHand: 실시간 dt만큼 진행하고, 이번에 생긴 일을 순서대로 actions에 더한다.</summary>
        public void Step(double now, double dt, List<HandAction> actions)
        {
            _settle = Math.Max(0, _settle - dt);
            if (Idle)
            {
                _t0 = now;
                _dealWait = 0;
                _mergeWait = 0;
                return;
            }
            _dealWait -= dt;
            _mergeWait -= dt;
            // 전달은 간격마다 다음 장(머지와 무관), 머지는 모으기·쉼이 끝나면 제자리에 정렬된 연속 3장을 위에서부터 찾아 하나씩.
            for (bool more = true; more;)
            {
                more = false;
                if (_gather != null && _mergeWait <= 0) { FinishMerge(now, actions); more = true; }
                if (_gather == null && _mergeWait <= 0 && StartMerge(now, actions)) more = true;
                if (_dealWait <= 0 && _queue.Count > 0) { Deal(now, _queue[0], actions); more = true; }
            }
            if (_queue.Count == 0 && _dealWait < 0) _dealWait = 0;
            if (_gather == null && _mergeWait < 0 && NextMergeTier() < 1) _mergeWait = 0;
        }



        void Deal(double now, Job head, List<HandAction> actions)
        {
            float u = Unit(now);
            int before = TopTier;
            var card = new HandCard(_nextId++, head.Tier);
            _cards.Add(card);
            _readyAt[card.Id] = now + Math.Max(0f, DealStraightSec);   // 줄에서 올라가 제자리에 정렬되는 순간
            Sort();
            head.N--;
            if (head.N <= 0) _queue.RemoveAt(0);
            else _queue[0] = head;
            float gap = Math.Min(_timing.dealGapMax, u * _timing.dealGapMul);
            _settle = Math.Max(_settle, Math.Max(0f, DealStraightSec));
            _dealWait += gap;
            actions?.Add(new HandAction(HandActionKind.Dealt, card, null, gap, 0, u, TopTier > before));
        }

        bool StartMerge(double now, List<HandAction> actions)
        {
            // 위에서부터 센다: 같은 등급 묶음마다 맨 위 MERGE_N장이 모두 제자리에 정렬돼 있으면 그 3장(위 → 아래)을 3장 중 맨 위 카드로 모은다.
            // 사용자 2026-11-19 「위에서부터 3개 머지 세어야지 중간에 꼬이지 않고 정렬되고」, 「3개중 제일 위 카드로 합쳐지면서」.
            int at = -1, tier = 0;
            for (int i = 0; i < _cards.Count;)
            {
                int t = _cards[i].Tier, j = i;
                while (j < _cards.Count && _cards[j].Tier == t) j++;
                if (t < _maxTier && j - i >= _mergeN)
                {
                    bool placed = true;
                    for (int q = i; q < i + _mergeN && placed; q++)
                        if (_readyAt.TryGetValue(_cards[q].Id, out var r) && r > now) placed = false;
                    if (placed) { at = i; tier = t; break; }
                }
                i = j;
            }
            if (at < 0) return false;
            float u = Unit(now);
            var ids = new int[_mergeN];
            for (int k = 0; k < _mergeN; k++) ids[k] = _cards[at + k].Id;
            _gather = ids;
            _gatherTier = tier;
            float md = u * _timing.mergeMul;
            _mergeWait = Math.Max(0, _mergeWait) + md;
            actions?.Add(new HandAction(HandActionKind.GatherStart, default, ids, md, md * _timing.moveMul, u, false));
            return true;
        }

        void FinishMerge(double now, List<HandAction> actions)
        {
            float u = Unit(now);
            int before = TopTier;
            var ids = _gather;
            _gather = null;
            _cards.RemoveAll(c => Array.IndexOf(ids, c.Id) >= 0);
            foreach (var id in ids) _readyAt.Remove(id);
            var card = new HandCard(_nextId++, _gatherTier + 1);
            _cards.Add(card);
            Sort();
            float after = Math.Min(_timing.afterMax, u * _timing.afterMul);
            _mergeWait += after;
            actions?.Add(new HandAction(HandActionKind.Merged, card, ids, after, 0, u, TopTier > before));
        }

        /// <summary>
        /// 다음에 합칠 등급: 3장 이상인 등급 중 **가장 높은 등급**(화면 위쪽 묶음부터). 0이면 없음.
        /// 05 §5.3.3 「가장 낮은 등급」 대신 사용자 결정 2026-11-19: 전달이 머지를 기다리지 않아 밑에 새 카드가 계속 들어오므로, 낮은 등급부터 고르면
        /// 위에 이미 3장이 된 묶음을 두고 밑에서 번쩍여 순서가 뒤섞인다(「순서대로 위에 3개 모이면 … 아래 두개가 위로 올라가면서 합쳐지고 밑에꺼도 재정렬되어야하는데 지금 번쩍이는거 보니까 순서가 뒤죽박죽」).
        /// 높은 등급부터 고르면 밑에서 합쳐진 결과가 위 묶음과 3장이 되는 즉시 이어서 합쳐진다(「밑에서부터 3개 합쳐지고 그게 또 위에꺼하고 합쳐지고 차곡차곡」).
        /// </summary>
        int NextMergeTier()
        {
            for (int t = _maxTier - 1; t >= 1; t--)
            {
                int n = 0;
                foreach (var c in _cards) if (c.Tier == t) n++;
                if (n >= _mergeN) return t;
            }
            return 0;
        }

        void Sort() => _cards.Sort((a, b) => a.Tier != b.Tier ? b.Tier.CompareTo(a.Tier) : a.Id.CompareTo(b.Id));
    }
}
