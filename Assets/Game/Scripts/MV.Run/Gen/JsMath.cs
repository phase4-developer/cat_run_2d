using System;

namespace MV.Run
{
    /// <summary>
    /// 정본(브라우저 V8) `Math.sin`과 비트 단위로 같은 sin(D-27, 04 §4.14.3).
    /// V8은 올바르게 반올림된 sin을 낸다(glibc 계열). .NET `Math.Sin`은 플랫폼 libm을 따라가서 Windows 에디터에서
    /// 골든 해시 4619개 중 118개가 마지막 비트에서 달랐다(칸 결과는 같음). 그래서 double-double(약 106비트)로 계산해
    /// double 하나로 반올림한다. 순수 IEEE double 덧셈·곱셈만 쓰고 FMA는 쓰지 않는다(Dekker 분할).
    /// 범위 축소는 π/2를 double 4개(약 212비트)로 나눠 쓴다. |x| ≲ 1e9에서 충분하다(hash 인자는 1e6 미만).
    /// </summary>
    public static class JsMath
    {
        // π/2 = P1 + P2 + P3 + P4 (각각 앞 항의 반올림 나머지).
        static readonly double P1 = BitConverter.Int64BitsToDouble(0x3FF921FB54442D18L);
        static readonly double P2 = BitConverter.Int64BitsToDouble(0x3C91A62633145C07L);
        static readonly double P3 = BitConverter.Int64BitsToDouble(unchecked((long)0xB91F1976B7ED8FBCUL));
        static readonly double P4 = BitConverter.Int64BitsToDouble(0x35B4CF98E804177DL);

        public static double Sin(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return double.NaN;
            if (x == 0) return x;
            double n = Math.Floor(x / P1 + 0.5);
            double rh = x, rl = 0;
            TwoProd(n, P1, out double ph, out double pl); AddDD(rh, rl, -ph, -pl, out rh, out rl);
            TwoProd(n, P2, out ph, out pl); AddDD(rh, rl, -ph, -pl, out rh, out rl);
            TwoProd(n, P3, out ph, out pl); AddDD(rh, rl, -ph, -pl, out rh, out rl);
            AddDD(rh, rl, -(n * P4), 0, out rh, out rl);

            double vh, vl;
            long q = (long)n & 3;
            if (q == 0 || q == 2) SinSeries(rh, rl, out vh, out vl);
            else CosSeries(rh, rl, out vh, out vl);
            if (q >= 2) { vh = -vh; vl = -vl; }
            return vh + vl;
        }

        static void SinSeries(double rh, double rl, out double sh, out double sl)
        {
            MulDD(rh, rl, rh, rl, out double r2h, out double r2l);
            double th = rh, tl = rl;
            sh = rh; sl = rl;
            for (int k = 2; k < 80; k += 2)
            {
                MulDD(th, tl, r2h, r2l, out th, out tl);
                DivDD(th, tl, (double)k * (k + 1), out th, out tl);
                th = -th; tl = -tl;
                if (th == 0 || Math.Abs(th) < 1e-40 * Math.Abs(sh)) break;
                AddDD(sh, sl, th, tl, out sh, out sl);
            }
        }

        static void CosSeries(double rh, double rl, out double sh, out double sl)
        {
            MulDD(rh, rl, rh, rl, out double r2h, out double r2l);
            double th = 1, tl = 0;
            sh = 1; sl = 0;
            for (int k = 1; k < 80; k += 2)
            {
                MulDD(th, tl, r2h, r2l, out th, out tl);
                DivDD(th, tl, (double)k * (k + 1), out th, out tl);
                th = -th; tl = -tl;
                if (th == 0 || Math.Abs(th) < 1e-40) break;
                AddDD(sh, sl, th, tl, out sh, out sl);
            }
        }

        // ---- double-double 기본 연산(식을 나눠 써서 컴파일러가 FMA로 합치지 않게 한다) ----

        static void TwoSum(double a, double b, out double s, out double e)
        {
            s = a + b;
            double bb = s - a;
            double t1 = s - bb;
            double t2 = a - t1;
            double t3 = b - bb;
            e = t2 + t3;
        }

        static void QuickTwoSum(double a, double b, out double s, out double e)
        {
            s = a + b;
            double t = s - a;
            e = b - t;
        }

        static void Split(double a, out double hi, out double lo)
        {
            double t = 134217729.0 * a;
            double u = t - a;
            hi = t - u;
            lo = a - hi;
        }

        static void TwoProd(double a, double b, out double p, out double e)
        {
            p = a * b;
            Split(a, out double ah, out double al);
            Split(b, out double bh, out double bl);
            double m1 = ah * bh;
            double m2 = ah * bl;
            double m3 = al * bh;
            double m4 = al * bl;
            double t = m1 - p;
            t = t + m2;
            t = t + m3;
            e = t + m4;
        }

        static void AddDD(double ah, double al, double bh, double bl, out double sh, out double sl)
        {
            TwoSum(ah, bh, out double s, out double e);
            TwoSum(al, bl, out double t, out double f);
            e = e + t;
            QuickTwoSum(s, e, out s, out e);
            e = e + f;
            QuickTwoSum(s, e, out sh, out sl);
        }

        static void MulDD(double ah, double al, double bh, double bl, out double ph, out double pl)
        {
            TwoProd(ah, bh, out double p, out double e);
            double c1 = ah * bl;
            double c2 = al * bh;
            double c = c1 + c2;
            e = e + c;
            QuickTwoSum(p, e, out ph, out pl);
        }

        static void DivDD(double ah, double al, double b, out double qh, out double ql)
        {
            double q1 = ah / b;
            TwoProd(q1, b, out double p, out double e);
            TwoSum(ah, -p, out double s, out double f);
            f = f - e;
            f = f + al;
            double q2 = (s + f) / b;
            QuickTwoSum(q1, q2, out qh, out ql);
        }
    }
}
