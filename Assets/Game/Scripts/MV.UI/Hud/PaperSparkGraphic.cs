using MV.Run;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 종이 막 별 무늬: 40개, 자리 (hash(i·3.3)·W, hash(i·8.1)·H), 반지름 6 + hash(i)·8 px, `rgba(176,122,232,.3)`(정본 pSparkC 네 갈래 별).
    /// 화면에 고정이고 막이 지나가며 드러난다(RectMask2D).
    /// </summary>
    public sealed class PaperSparkGraphic : MaskableGraphic
    {
        const float Px = 3f;
        public const int Count = 40;
        const int Seg = 4;
        static readonly Color Spark = new Color(176 / 255f, 122 / 255f, 232 / 255f, .3f);

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            for (int i = 0; i < Count; i++)
            {
                float x = (float)FieldGen.Hash(i * 3.3) * r.width, y = (float)FieldGen.Hash(i * 8.1) * r.height;
                float rad = (6 + (float)FieldGen.Hash(i) * 8) * Px;
                var c = new Vector2(r.xMin + x, r.yMax - y);
                int center = vh.currentVertCount;
                vh.AddVert(c, Spark, Vector2.zero);
                // 꼭짓점 위 → 오른쪽 → 아래 → 왼쪽, 사이는 가운데를 조절점으로 한 2차 곡선.
                Vector2[] tips = { new Vector2(0, rad), new Vector2(rad, 0), new Vector2(0, -rad), new Vector2(-rad, 0) };
                int first = vh.currentVertCount;
                for (int q = 0; q < 4; q++)
                {
                    Vector2 a = tips[q], b = tips[(q + 1) % 4];
                    for (int s = 0; s < Seg; s++)
                    {
                        float t = s / (float)Seg, u = 1 - t;
                        vh.AddVert(c + u * u * a + t * t * b, Spark, Vector2.zero);   // 조절점 = 가운데(0)
                    }
                }
                int n = vh.currentVertCount - first;
                for (int k = 0; k < n; k++) vh.AddTriangle(center, first + k, first + (k + 1) % n);
            }
        }
    }
}
