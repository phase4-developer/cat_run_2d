using System.Collections.Generic;

namespace MV.Data
{
    /// <summary>
    /// 116_charLook에서 외형 행 고르기(MV-SPEC-02 ArtResourcePlan §2). 순수 함수라 시험한다.
    /// 자리: slot이 표에 없으면 그 캐릭터의 마지막 slot. 등급: 그 slot 안에서 fromTier가 등급 이하인 행 중 가장 큰 것.
    /// 등급이 모든 행보다 작으면 fromTier가 가장 작은 행. 그 캐릭터 행이 없으면 null.
    /// </summary>
    public static class CharLookResolver
    {
        public static CharLookData Resolve(IEnumerable<CharLookData> rows, int catId, int slot, int tier)
        {
            if (rows == null) return null;
            int lastSlot = -1;
            foreach (var row in rows)
                if (row.catId == catId && row.slot > lastSlot) lastSlot = row.slot;
            if (lastSlot < 0) return null;
            int useSlot = slot < 0 ? 0 : slot > lastSlot ? lastSlot : slot;

            CharLookData best = null, lowest = null;
            foreach (var row in rows)
            {
                if (row.catId != catId || row.slot != useSlot) continue;
                if (lowest == null || row.fromTier < lowest.fromTier) lowest = row;
                if (row.fromTier <= tier && (best == null || row.fromTier > best.fromTier)) best = row;
            }
            return best ?? lowest;
        }

        /// <summary>그 캐릭터가 표에 가진 slot 목록(작은 순, 중복 없음). 분신 외형 종류 수 = 이 목록 길이.</summary>
        public static void Slots(IEnumerable<CharLookData> rows, int catId, List<int> into)
        {
            into.Clear();
            if (rows == null) return;
            foreach (var row in rows)
                if (row.catId == catId && !into.Contains(row.slot)) into.Add(row.slot);
            into.Sort();
        }
    }
}
