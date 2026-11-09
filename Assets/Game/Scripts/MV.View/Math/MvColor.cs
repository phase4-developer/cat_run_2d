using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 정본 색 도우미 `shadeC`·`mixC`·`ha`(L2801~2802, 기획서 02 §2.6.1).
    /// sRGB 0~1 double로 그대로 계산하고(선형 변환 없음), 결과는 three r128 `getHex`처럼 ×255 뒤 내림한다.
    /// 결과 문자열은 캐시한다(정본 `_cc`와 같음). 메인 스레드 전용.
    /// </summary>
    public static class MvColor
    {
        static readonly Dictionary<string, string> s_cache = new Dictionary<string, string>(StringComparer.Ordinal);

        public static void Parse(string hex, out double r, out double g, out double b)
        {
            if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length < 7)
                throw new FormatException($"MvColor: '#rrggbb' 형식이 아닙니다: {hex}");
            int v = int.Parse(hex.Substring(1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            r = ((v >> 16) & 255) / 255.0;
            g = ((v >> 8) & 255) / 255.0;
            b = (v & 255) / 255.0;
        }

        /// <summary>three `getHex`: 채널 ×255 뒤 정수 내림(비트 연산).</summary>
        public static string ToHex(double r, double g, double b)
        {
            int ri = Byte(r), gi = Byte(g), bi = Byte(b);
            return "#" + ri.ToString("x2") + gi.ToString("x2") + bi.ToString("x2");
        }

        static int Byte(double c)
        {
            int v = (int)(c * 255);   // JS `<<`는 0 쪽으로 자른다
            return v < 0 ? 0 : v > 255 ? 255 : v;
        }

        /// <summary>shadeC(c, k): k &gt; 0이면 흰색 쪽 lerp(k), 아니면 ×(1+k).</summary>
        public static string Shade(string c, double k)
        {
            string key = c + "|" + k.ToString("R", CultureInfo.InvariantCulture);
            if (s_cache.TryGetValue(key, out var v)) return v;
            Parse(c, out double r, out double g, out double b);
            if (k > 0) { r += (1 - r) * k; g += (1 - g) * k; b += (1 - b) * k; }
            else { double m = 1 + k; r *= m; g *= m; b *= m; }
            v = ToHex(r, g, b);
            s_cache[key] = v;
            return v;
        }

        /// <summary>mixC(a, b, k): k를 1/quant 단위로 내림한 뒤 a→b 선형(정본 quant = 20, `view.mixQuant`).</summary>
        public static string Mix(string a, string b, double k, int quant = 20) => MixStep(a, b, (int)(k * quant), quant);   // JS `|0`

        /// <summary>양자화 단계 q(0~quant)를 직접 받는 mixC. q/quant를 다시 곱해 내리면 부동소수 오차로 한 단계 낮아질 수 있어 분리했다.</summary>
        public static string MixStep(string a, string b, int q, int quant = 20)
        {
            string key = a + ">" + b + "|" + q + "/" + quant;
            if (s_cache.TryGetValue(key, out var v)) return v;
            double t = q / (double)quant;
            Parse(a, out double ar, out double ag, out double ab);
            Parse(b, out double br, out double bg, out double bb);
            ar += (br - ar) * t; ag += (bg - ag) * t; ab += (bb - ab) * t;
            v = ToHex(ar, ag, ab);
            s_cache[key] = v;
            return v;
        }

        /// <summary>ha(c, a): 알파 round(a·255).</summary>
        public static Color32 WithAlpha(string c, double a)
        {
            var col = ToColor32(c);
            col.a = (byte)Mathf.Clamp((int)Math.Floor(a * 255 + 0.5), 0, 255);
            return col;
        }

        public static Color32 ToColor32(string hex)
        {
            Parse(hex, out double r, out double g, out double b);
            return new Color32((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255), 255);
        }

        /// <summary>표의 색 칸이 비면 false(정본 `th.tint` 없음 등).</summary>
        public static bool TryColor32(string hex, out Color32 c)
        {
            if (string.IsNullOrEmpty(hex)) { c = default; return false; }
            c = ToColor32(hex);
            return true;
        }
    }
}
