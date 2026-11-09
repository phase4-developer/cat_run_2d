using System;
using System.Collections.Generic;
using UnityEngine;

namespace MV.View
{
    /// <summary>`drawBox` 결과 치수(로컬 m, 앵커 = 앞면 아래 가운데, y 위가 +).</summary>
    public struct BoxShape
    {
        public double W, H, D;
        /// <summary>윗면 두께 T(m).</summary>
        public double T;
        /// <summary>외곽선 굵기(m).</summary>
        public double Line;
        /// <summary>위 하이라이트 높이(m).</summary>
        public double Highlight;
        /// <summary>오른쪽 그늘 띠 폭(m).</summary>
        public double ShadeW;
    }

    /// <summary>
    /// 정본 `drawBox`(L2809~2820, 02 §2.6.4): 「정면 + 오른쪽으로 기운 윗면」 그림 한 장을 빌보드 로컬 메쉬로 만든다.
    /// px 하한·상한(1 px, 3 px, 2 px …)은 앵커의 s(px/m)로 m에 옮긴다. s = ∞면 하한·상한 없이 비율만 쓴다.
    /// 그리는 순서(정점 순서): 그늘 띠 → 윗면 → 윗면 외곽선 → 정면 → 하이라이트 → 정면 외곽선.
    /// </summary>
    public static class BoxBillboardBuilder
    {
        static readonly Color32 s_highlight = new Color32(255, 255, 255, 46);   // rgba(255,255,255,.18) → round(0.18·255)

        public static BoxShape Shape(double w, double h, double d, double s)
        {
            if (w <= 0 || h <= 0 || d <= 0) throw new ArgumentOutOfRangeException(nameof(w), "상자 치수는 양수여야 합니다");
            double inv = double.IsInfinity(s) || s <= 0 ? 0 : 1 / s;
            return new BoxShape
            {
                W = w, H = h, D = d,
                // T px = max(2, min(d·s·0.32, H2·0.6 + 6))
                T = Math.Max(2 * inv, Math.Min(0.32 * d, 0.6 * h + 6 * inv)),
                // lw px = max(1, min(3, s·0.035))
                Line = Clamp(0.035, inv, 3 * inv),
                // 하이라이트 px = max(1, min(3, H2·0.06))
                Highlight = Clamp(0.06 * h, inv, 3 * inv),
                // 그늘 띠 px = max(1, W2·0.04)
                ShadeW = Math.Max(inv, 0.04 * w),
            };
        }

        static double Clamp(double v, double lo, double hi)
        {
            if (hi <= 0) return v;           // s = ∞: 하한·상한 없음
            return Math.Max(lo, Math.Min(hi, v));
        }

        /// <summary>
        /// 버퍼에 상자 그림을 더한다(Clear하지 않음). <paramref name="topCol"/>이 비면 shadeC(col, 0.18).
        /// <paramref name="outline"/>은 정본 `ol` 인자(기본 참). <paramref name="outlineCol"/>은 `view.outlineColor`.
        /// </summary>
        public static BoxShape Build(MeshBuffer mb, double w, double h, double d, string col, string topCol, double s, string outlineCol, bool outline = true)
        {
            var b = Shape(w, h, d, s);
            float hw = (float)(w / 2), fh = (float)h, T = (float)b.T, lw = (float)b.Line;
            Color32 shade = MvColor.ToColor32(MvColor.Shade(col, -0.28));
            Color32 top = MvColor.ToColor32(string.IsNullOrEmpty(topCol) ? MvColor.Shade(col, 0.18) : topCol);
            Color32 frontTop = MvColor.ToColor32(MvColor.Shade(col, 0.04));
            Color32 frontBot = MvColor.ToColor32(MvColor.Shade(col, -0.14));
            Color32 ol = MvColor.ToColor32(outlineCol);

            // 1. 오른쪽 그늘 띠: x w/2 ~ w/2 + shadeW, y 0 ~ h + T/2
            float sw = (float)b.ShadeW;
            mb.AddQuad(new Vector3(hw, 0), new Vector3(hw + sw, 0), new Vector3(hw + sw, fh + T * 0.5f), new Vector3(hw, fh + T * 0.5f), shade);

            // 2. 윗면 평행사변형
            var topPts = new List<Vector2>(4)
            {
                new Vector2(-hw, fh),
                new Vector2(-hw + (float)(0.06 * w), fh + T),
                new Vector2(hw + (float)(0.04 * w), fh + T),
                new Vector2(hw, fh),
            };
            mb.AddPolygon2D(topPts, top);
            if (outline) mb.AddOutline2D(topPts, lw, ol);

            // 3. 정면(세로 그라데이션: 위 shadeC(col, .04) → 아래 shadeC(col, −.14))
            mb.AddQuad(new Vector3(-hw, 0), new Vector3(hw, 0), new Vector3(hw, fh), new Vector3(-hw, fh), frontBot, frontBot, frontTop, frontTop);

            // 4. 위 하이라이트
            float hl = (float)b.Highlight;
            mb.AddQuad(new Vector3(-hw, fh - hl), new Vector3(hw, fh - hl), new Vector3(hw, fh), new Vector3(-hw, fh), s_highlight);

            // 5. 정면 외곽선
            if (outline)
            {
                var front = new List<Vector2>(4) { new Vector2(-hw, 0), new Vector2(hw, 0), new Vector2(hw, fh), new Vector2(-hw, fh) };
                mb.AddOutline2D(front, lw, ol);
            }
            return b;
        }
    }
}
