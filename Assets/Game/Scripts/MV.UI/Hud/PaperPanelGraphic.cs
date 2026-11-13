using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>종이 막 바탕: 지그재그 양쪽 가장자리(이 14 px), 종이색 `#fff4e2`, 잉크 테두리 `#3a2440` 3 px(정본 paperPanel).</summary>
    public sealed class PaperPanelGraphic : MaskableGraphic
    {
        const float Px = 3f, Edge = 14 * Px, Line = 3 * Px, Over = 10 * Px;
        public static readonly Color Paper = MvUi.Hex("#fff4e2"), Ink = MvUi.Hex("#3a2440"), Title = MvUi.Hex("#7a4ad0");

        public float X0 { get; private set; }
        public float X1 { get; private set; }

        public void SetSpan(float x0, float x1)
        {
            if (Mathf.Approximately(x0, X0) && Mathf.Approximately(x1, X1)) return;
            X0 = x0; X1 = x1;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float H = r.height;
            // 정본 좌표(왼쪽 위 0, y 아래로) → 이 사각형 좌표.
            Vector2 P(float x, float y) => new Vector2(r.xMin + x, r.yMax - y);
            float yTop = -Over, yBot = H + Over;
            Quad(vh, P(X0, yTop), P(X1, yTop), P(X1, yBot), P(X0, yBot), Paper);
            for (float y = yTop; y <= yBot; y += Edge * 2)
            {
                Tri(vh, P(X0, y), P(X0 - Edge * .6f, y + Edge), P(X0, y + Edge * 2), Paper);
                Tri(vh, P(X1, y), P(X1 + Edge * .6f, y + Edge), P(X1, y + Edge * 2), Paper);
            }
            for (float y = yTop; y <= yBot; y += Edge * 2)
            {
                Seg(vh, P(X0, y), P(X0 - Edge * .6f, y + Edge));
                Seg(vh, P(X0 - Edge * .6f, y + Edge), P(X0, y + Edge * 2));
                Seg(vh, P(X1, y), P(X1 + Edge * .6f, y + Edge));
                Seg(vh, P(X1 + Edge * .6f, y + Edge), P(X1, y + Edge * 2));
            }
        }

        static void Seg(VertexHelper vh, Vector2 a, Vector2 b)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            var n = new Vector2(-d.y, d.x).normalized * (Line / 2);
            // 이음매가 비지 않게 양 끝을 반 굵기만큼 늘린다.
            var e = d.normalized * (Line / 2);
            Quad(vh, a - e + n, b + e + n, b + e - n, a - e - n, Ink);
        }

        internal static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector2.zero); vh.AddVert(b, col, Vector2.zero); vh.AddVert(c, col, Vector2.zero); vh.AddVert(d, col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        internal static void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector2.zero); vh.AddVert(b, col, Vector2.zero); vh.AddVert(c, col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
        }
    }
}
