using System;
using System.Collections.Generic;
using System.Linq;
using jjevol;
using MV.Save;
using Newtonsoft.Json.Linq;

namespace MV.Meta
{
    /// <summary>
    /// 콘텐츠 처리기(G7)용 재화 변경 묶음. <see cref="MvLocalMetaBackend"/> 안의 WealthDelta와 같은 규칙이다:
    /// 수량 확인은 Framework <see cref="ResourceService"/>(예약 제외 <c>Get</c>), 결과 행은 <c>WealthContainer</c> 형식으로
    /// 다른 컨테이너와 같은 <c>ApplyAndSave</c>에 넣는다(09 §9.4 — 재화만 먼저 저장하지 않는다).
    /// Framework 대체: <c>LocalRewardService.PrepareMutation/Commit</c>은 재화 컨테이너만 저장한다. 교체 지점: 백엔드 WealthDelta와 하나로 합칠 때 이 클래스를 쓴다.
    /// </summary>
    public sealed class MvWealthChange
    {
        private readonly Dictionary<int, long> _cost = new Dictionary<int, long>();
        private readonly Dictionary<int, long> _gain = new Dictionary<int, long>();

        public bool IsEmpty => _cost.Count == 0 && _gain.Count == 0;

        public void Cost(int id, long amount) => Add(_cost, id, amount);
        public void Gain(int id, long amount) => Add(_gain, id, amount);

        private static void Add(Dictionary<int, long> map, int id, long amount)
        {
            if (amount <= 0) return;
            map.TryGetValue(id, out long now);
            map[id] = checked(now + amount);
        }

        /// <summary>바뀐 재화 행을 <paramref name="data"/>에 넣는다. 모르는 재화·비용 부족이면 false와 이유.</summary>
        public bool TryWrite(ResourceService res, JObject data, out string fail)
        {
            if (res == null) throw new ArgumentNullException(nameof(res));
            if (data == null) throw new ArgumentNullException(nameof(data));
            fail = null;
            var rows = new JArray();
            foreach (int id in _cost.Keys.Union(_gain.Keys).OrderBy(id => id))
            {
                string type = MvWealthIds.TypeOf(id);
                if (type == null) { fail = "모르는 재화 " + id; return false; }
                _cost.TryGetValue(id, out long cost);
                _gain.TryGetValue(id, out long gain);
                long raw = res.GetRaw(id);
                if (res.Get(id) < cost) { fail = $"재화 {id} 부족: {res.Get(id)} < {cost}"; return false; }
                long next = checked(raw - cost + gain);
                if (next == raw) continue;
                rows.Add(new JObject { [WealthContainer.TypeKey] = type, [WealthContainer.QuantityKey] = next });
            }
            if (rows.Count > 0) data[MvSaveKeys.Wealth] = rows;
            return true;
        }
    }
}
