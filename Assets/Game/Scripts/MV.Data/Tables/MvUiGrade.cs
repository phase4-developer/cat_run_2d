namespace MV.Data
{
    /// <summary>
    /// 등급 틀 그림 고르기(09_uiResource 「등급 틀」 묶음). HUD 무기 칸(MV.UI <c>HudGrade</c>)과 무기문 위 카드 묶음(MV.View)이 같은 규칙을 쓰도록 여기 하나만 둔다.
    /// </summary>
    public static class MvUiGrade
    {
        /// <summary>09_uiResource 「등급 틀」 묶음. 행의 sort = 등급 번호(1부터, 디자이너 Grade_N).</summary>
        public const int FrameGroup = 5;

        /// <summary>
        /// 등급 번호 grade(1부터)의 틀 그림 행. 그 번호 행이 없으면 그 아래 가장 큰 번호 행(모자란 그림은 이전 그림을 다시 쓴다), 그것도 없으면 첫 행. 표에 묶음이 없으면 null.
        /// </summary>
        public static UiResourceData Frame(MvTables t, int grade)
        {
            if (t == null) return null;
            UiResourceData best = null, first = null;
            foreach (var r in t.UiResource.GetGroup(FrameGroup))
            {
                if (!r.active) continue;
                if (first == null || r.sort < first.sort) first = r;
                if (r.sort <= grade && (best == null || r.sort > best.sort)) best = r;
            }
            return best ?? first;
        }
    }
}
