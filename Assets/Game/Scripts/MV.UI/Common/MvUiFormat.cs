using System;
using System.Globalization;

namespace MV.UI
{
    /// <summary>숫자 표기(05 §5.1.12).</summary>
    public static class MvUiFormat
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

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
