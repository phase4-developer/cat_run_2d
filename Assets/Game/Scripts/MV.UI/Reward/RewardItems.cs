using System.Collections.Generic;
using MV.Data;
using MV.Save;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 공용 보상 팝업(MV-UI3-REWARD)의 칸 목록 만들기. 정크버스터즈 <c>RewardPopupData.Create</c>(<c>ConsumeDataExt.MergeList</c>)처럼
    /// 같은 보상은 한 칸으로 합치고, 받은 순서(처음 나온 순서)를 지킨다.
    /// <list type="bullet">
    /// <item>칸 = 재화(<see cref="ConsumeType.Wealth"/>)만. 아이콘·등급 틀은 <c>01_wealth</c> 행(공용 <see cref="ItemSlot"/>)이 정한다.
    /// 재화가 아닌 보상(퀘스트 포인트 등)은 그릴 그림 행이 없어 칸을 만들지 않고 경고를 남긴다.</item>
    /// <item>수량은 정수로 버린다(AGENTS.md 소수점 규칙). 1 미만이면 칸을 만들지 않는다(결과 팝업과 같음, 사용자 2026-11-19 「0개 획득이면 표시 자체를 안해야하고」).</item>
    /// </list>
    /// </summary>
    public static class RewardItems
    {
        /// <summary>재화 보상 1항목(<c>01_wealth.type</c>, 예: <c>gold</c>). 모르는 type은 id 0(칸 없음, 경고).</summary>
        public static ConsumeData Wealth(string wealthType, double value) =>
            new ConsumeData(ConsumeType.Wealth, MvWealthIds.IdOf(wealthType), value);

        public static List<ItemSlotData> Build(MvTables tables, IEnumerable<ConsumeData> rewards)
        {
            var order = new List<int>();
            var sum = new Dictionary<int, double>();
            if (rewards != null)
                foreach (var r in rewards)
                {
                    if (r.Type != ConsumeType.Wealth)
                    {
                        if (r.Type != ConsumeType.None)
                            Debug.LogWarning("[RewardPopup] 재화가 아닌 보상은 칸이 없어 건너뜀: " + r);
                        continue;
                    }
                    if (MvWealthIds.TypeOf(r.Id) == null)
                    {
                        Debug.LogWarning("[RewardPopup] 모르는 재화 id라 건너뜀: " + r);
                        continue;
                    }
                    if (sum.TryGetValue(r.Id, out double v)) sum[r.Id] = v + r.Value;
                    else { sum[r.Id] = r.Value; order.Add(r.Id); }
                }

            var list = new List<ItemSlotData>(order.Count);
            foreach (int id in order)
            {
                double n = System.Math.Floor(sum[id]);
                if (n < 1) continue;
                list.Add(new ItemSlotData(tables, MvWealthIds.TypeOf(id), MvUiFormat.Fmt(n)));
            }
            return list;
        }
    }
}
