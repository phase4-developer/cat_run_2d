using System;
using System.Globalization;
using jjevol;

namespace MV.UI
{
    /// <summary>숫자 표기(05 §5.1.12).</summary>
    public static class MvUiFormat
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// 같은 규칙을 Framework 숫자 표기 프로필로 옮긴 것. <see cref="MvUiBoot"/>가 게임 기본값으로 넣어 PriceLabel·PriceBtn 등
        /// Framework 라벨이 <see cref="Fmt"/>와 같은 글자를 쓴다(MV-FW-NUMFMT, 사용자 카드 「k포맷은 … 오면 일괄적용」).
        /// </summary>
        public static NumberFormatProfile Profile { get; } = new NumberFormatProfile(NumberNotation.Abbreviated, 1000, new[] { "K", "M" },
            abbreviateFrom: 10000, decimalPlaces: 1, groupDigits: false, rounding: NumberRounding.HalfUp, keepTrailingZeros: true);

        /// <summary><see cref="Profile"/>로 쓴 글자(시험에서 <see cref="Fmt"/>와 대조).</summary>
        public static string FmtFramework(long n) => NumberFormat.Format(n, Profile, Inv);

        /// <summary>정본 <c>fmt(n)</c>: floor 뒤 1,000,000 이상 x.xM, 10,000 이상 x.xK, 그 밖 천 단위 쉼표.</summary>
        public static string Fmt(double n)
        {
            if (double.IsNaN(n) || double.IsInfinity(n)) n = 0;
            n = Math.Floor(n);
            if (n >= 1000000) return Fixed1(n / 1000000) + "M";
            if (n >= 10000) return Fixed1(n / 1000) + "K";
            return n.ToString("#,0", Inv);
        }

        /// <summary>JS <c>toFixed(1)</c> 근사(0.5 올림).</summary>
        public static string Fixed1(double v) => (Math.Floor(v * 10 + 0.5) / 10).ToString("0.0", Inv);
    }
}
