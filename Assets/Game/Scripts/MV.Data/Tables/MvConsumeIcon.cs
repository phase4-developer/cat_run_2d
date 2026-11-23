namespace MV.Data
{
    /// <summary>
    /// 소모 종류 → 아이콘(아틀라스·그림 이름). 정본은 <c>07_consumeType</c>의 <c>iconAtlas</c>·<c>iconColumn</c>(기획서 07: 「consumeType 아이콘은
    /// 코드 switch가 아니라 표에서」): <c>iconColumn</c>이 <c>=이름</c>이면 그 그림, 열 이름이면 <c>idTable</c>의 그 행(id) 열 값.
    /// 화면은 재화·보상·가격 아이콘을 프리팹에 직접 두지 않고 여기서 읽는다(MV-UI-ICONSRC, 사용자 2026-11-23 「아이콘들은 다 데이터가 존재하는 정보잖아」).
    /// Framework 가격 부품에는 MV.UI <c>MvConsumePresentation</c>(IConsumePresentationProvider)이 같은 값을 넘긴다.
    /// </summary>
    public static class MvConsumeIcon
    {
        /// <summary>종류·id의 아이콘. 표에 아이콘이 없으면 false(그림 없음 — 화면은 숨긴다).</summary>
        public static bool TryGet(MvTables t, int typeId, int itemId, out string atlas, out string sprite)
        {
            atlas = sprite = null;
            var ct = t?.ConsumeType.Get(typeId);
            if (ct == null || string.IsNullOrEmpty(ct.iconAtlas) || string.IsNullOrEmpty(ct.iconColumn)) return false;
            var fixedIcon = ct.FixedIcon;
            if (fixedIcon != null) sprite = fixedIcon;
            else if (ct.UsesId && ct.idKind == IdKind.row)
            {
                var table = t.Find(ct.idTable);
                if (table != null)
                    foreach (var r in table.RawRows)
                        if (r != null && r.GetInt("id") == itemId) { sprite = r.Get(ct.iconColumn); break; }
            }
            if (string.IsNullOrEmpty(sprite)) { sprite = null; return false; }
            atlas = ct.iconAtlas;
            return true;
        }

        /// <summary>재화(<c>01_wealth.type</c>) 아이콘 = 종류 <see cref="ConsumeType.Wealth"/>의 그 행.</summary>
        public static bool TryWealth(MvTables t, string wealthType, out string atlas, out string sprite)
        {
            atlas = sprite = null;
            if (t == null || string.IsNullOrEmpty(wealthType)) return false;
            int id = t.Wealth.GetId(wealthType);
            return id != 0 && TryGet(t, (int)ConsumeType.Wealth, id, out atlas, out sprite);
        }
    }
}
