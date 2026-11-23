namespace MV.Data
{
    /// <summary>등급 틀 그림(아틀라스·그림 이름)과 그 등급 번호.</summary>
    public sealed class GradeFrame
    {
        public readonly int grade;
        public readonly string atlas;
        public readonly string sprite;

        public GradeFrame(int grade, string atlas, string sprite)
        {
            this.grade = grade;
            this.atlas = atlas;
            this.sprite = sprite;
        }
    }

    /// <summary>
    /// 등급 틀 그림 고르기. 정본은 <c>15_grade.frameSprite</c>(MV-UI-ICONSRC, 옛 09_uiResource 등급 틀 묶음 대체).
    /// HUD 무기 칸(MV.UI <c>HudGrade</c>)·보상 칸·로비·무기문 위 카드 묶음(MV.View)이 같은 규칙을 쓰도록 여기 하나만 둔다.
    /// </summary>
    public static class MvUiGrade
    {
        /// <summary>
        /// 등급 번호 grade(1부터)의 틀. 그 번호 행이 없으면 그 아래 가장 큰 번호 행(모자란 그림은 이전 그림을 다시 쓴다), 그것도 없으면 가장 낮은 행. 표가 비면 null.
        /// </summary>
        public static GradeFrame Frame(MvTables t, int grade)
        {
            if (t == null) return null;
            GradeData best = null, first = null;
            foreach (var r in t.Grade.Datas)
            {
                if (r == null || string.IsNullOrEmpty(r.frameSprite)) continue;
                if (first == null || r.grade < first.grade) first = r;
                if (r.grade <= grade && (best == null || r.grade > best.grade)) best = r;
            }
            var g = best ?? first;
            return g == null ? null : new GradeFrame(g.grade, MvAtlas.UiCard, g.frameSprite);
        }
    }
}
