using System;
using System.Collections.Generic;

namespace MV.Run
{
    // 난수 주입(기획서 04 §4.17). MV.Run은 규칙 난수 RunRng 하나만 뽑는다. 그림 난수는 구독자(MV.Fx/View/UI)가 FxRng로.
    // Framework 대체: MV.Core에 IRng 자리가 있으나(10 §10.4) 아직 비어 있다. MV.Core가 IRng를 두면 이 인터페이스를 그쪽으로 옮긴다.

    /// <summary>[0,1) 균등 난수 한 줄기.</summary>
    public interface IRng
    {
        double NextDouble();
    }

    /// <summary>규칙 난수 사용처(값 = 정본 호출 줄, 04 §4.17.1). 덤프 `rng[seq, line, value]`의 line과 같다.</summary>
    public enum RngUse
    {
        EnemySpeed = 1323,
        EnemyDrop = 1927,
        Proc = 1986,
        Storm = 2004,
        Blast = 2005,
        Crit = 2081,
        DoubleShot = 2090,
        StormPick = 2167,
        StormJitter = 2168,
        CardDraw = 2301,
        /// <summary>모드 규칙 줄기(ModeRng). 정본 경로에서는 뽑지 않는다(04 §4.20.1).</summary>
        Mode = 0,
    }

    /// <summary>한 번 뽑은 규칙 난수 기록(재현·대조용).</summary>
    public readonly struct RngDraw
    {
        public readonly RngUse Use;
        public readonly double Value;
        public RngDraw(RngUse use, double value) { Use = use; Value = value; }
        public override string ToString() => $"{(int)Use}:{Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}";
    }

    public static class RngExtensions
    {
        public static double Range(this IRng r, double a, double b) => a + r.NextDouble() * (b - a);
        public static bool Chance(this IRng r, double p) => r.NextDouble() < p;
        public static int Index(this IRng r, int n) => (int)Math.Floor(r.NextDouble() * n);
    }

    /// <summary>PCG32(XSH-RR). 시드 ulong. 04 §4.17.2-2.</summary>
    public sealed class Pcg32Rng : IRng
    {
        ulong _state;
        readonly ulong _inc;

        public Pcg32Rng(ulong seed, ulong stream = 0xda3e39cb94b95bdbUL)
        {
            _inc = (stream << 1) | 1UL;
            _state = 0;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * 6364136223846793005UL + _inc);
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        /// <summary>53비트 정밀도 [0,1).</summary>
        public double NextDouble()
        {
            ulong hi = NextUInt() >> 5, lo = NextUInt() >> 6;
            return (hi * 67108864.0 + lo) / 9007199254740992.0;
        }
    }

    /// <summary>
    /// RunSetup.Seed에서 파생한 별도 줄기(04 §4.20.1). 같은 시드의 PCG32를 줄기(stream) 번호만 달리 만든다.
    /// 정본 경로는 이 둘을 한 번도 뽑지 않는다. 번호는 고정이다(바꾸면 모드·동행 재현이 바뀐다).
    /// </summary>
    public static class RunRngStreams
    {
        /// <summary>ModeRng 줄기("mode").</summary>
        public const ulong Mode = 0x6d6f6465UL;
        /// <summary>CompanionRng 줄기("comp").</summary>
        public const ulong Companion = 0x636f6d70UL;
    }

    /// <summary>항상 같은 값 c. 프로토 대조(04 §4.17.2-5 `Math.random = () => c`).</summary>
    public sealed class ConstRng : IRng
    {
        public readonly double C;
        public ConstRng(double c) { C = c; }
        public double NextDouble() => C;
    }

    /// <summary>덤프에 기록된 규칙 난수를 순서대로 돌려준다. 더 뽑으면 예외(12 §12.3.2).</summary>
    public sealed class ReplayRng : IRng
    {
        readonly IReadOnlyList<double> _values;
        public int Position { get; private set; }
        public ReplayRng(IReadOnlyList<double> values) { _values = values ?? throw new ArgumentNullException(nameof(values)); }
        public bool Exhausted => Position >= _values.Count;
        public double NextDouble()
        {
            if (Position >= _values.Count) throw new InvalidOperationException($"ReplayRng: 기록된 난수 {_values.Count}개를 넘게 뽑았습니다");
            return _values[Position++];
        }
    }
}
