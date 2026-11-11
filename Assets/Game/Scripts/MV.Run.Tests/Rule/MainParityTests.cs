using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MV.Data;
using NUnit.Framework;

namespace MV.Run.Tests.Rule
{
    /// <summary>
    /// 본편 동일 시험(04 §4.20.6-1). Area 1~5 × Level 1·8·15를 같은 시드·같은 입력으로 돌려
    /// RunResult·RunEvent 열·RunRng 소비 횟수와 순서를 확장 지점을 넣기 전 커밋(dev 1ad0b23)의 골든 요약과 맞춘다.
    /// 골든 = 「프레임 수 / 이벤트 수 / RunRng 소비 수 / 종료 사유 / SHA256 앞 16자」(커밋 c2a69fa에서 기록, 잡 43876e9a0589. MV-SPEC2-FIX1에서 다시 기록: 103 피해 원작 단위·내부 피해 버림 정수·Area 시작 골드·하수인 무리 z·원석 타격 골드 제거·치명 피해 base 2, 잡 533b045164ab의 실제 값. MV-RUN-STARTCUR에서 Area 2~5 9개 다시 기록: PC-15 하수인 훔치기가 시작 골드를 남김 — 프레임·이벤트·난수 수는 같고 골드·훔친 양만 다름, 잡 a45c76f7a8fc의 실제 값. MV-RUN-SPLITREWARD에서 15개 다시 기록: 금화 더미 타격당 골드 → 피해 비율 10등분 지급 — 프레임·난수 수·종료 사유는 같고 Gain 이벤트 수·골드만 다름, 잡 356c29aac6b4의 실제 값. MV-RUN-MIDZONE에서 15개 다시 기록: 중간 구역 = 계단 오르는 구간(임시), 310_template midFromZ·midToZ 빈칸이라 평지 감속 2.5가 없어짐 — 더 빨리 달려 프레임·이벤트·난수 수·해시가 바뀌고 종료 사유는 모두 Wall 그대로, 잡 1454ec93d701의 실제 값).
    /// 두 경로를 다 본다: 기존 진입점 <c>RunSim.Create(setup, …)</c>(규칙 생략 = MainRunRule)과
    /// <c>RunSim.Create(RunContext, MainRunRule, …)</c>(① Build → ② BuildCourse, 모디파이어 0·동행 없음).
    /// </summary>
    public sealed class MainParityTests
    {
        /// <summary>확장 지점 전 코드로 만든 값. 키 A{area}L{level}(area 1기반).</summary>
        static readonly Dictionary<string, string> Golden = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["A1L1"] = "f3126 e424 r235 Wall 4a10c80899b0edc6",
            ["A1L8"] = "f3890 e576 r291 Wall dc8e3f5784b69e70",
            ["A1L15"] = "f3778 e485 r281 Wall 5dd8a69189f6d817",
            ["A2L1"] = "f3287 e417 r243 Wall b44d5aee2ef737ed",
            ["A2L8"] = "f3886 e547 r291 Wall d67585aca514800a",
            ["A2L15"] = "f3676 e496 r275 Wall 3d5886a184a9bb00",
            ["A3L1"] = "f3398 e837 r493 Wall 33bb7420e92df7e4",
            ["A3L8"] = "f3890 e988 r577 Wall c6cb96099cd9255a",
            ["A3L15"] = "f3818 e926 r559 Wall 5602630034dfb410",
            ["A4L1"] = "f3155 e803 r469 Wall 54cfe749c2ef432f",
            ["A4L8"] = "f3864 e946 r573 Wall 39929c3dd2d90a15",
            ["A4L15"] = "f3680 e944 r545 Wall 13b7db96dc957f80",
            ["A5L1"] = "f3151 e838 r469 Wall bae734eeccaf5ca7",
            ["A5L8"] = "f3890 e997 r577 Wall 76f3922407c390e8",
            ["A5L15"] = "f3676 e855 r545 Wall 91ff466ddcb4906c",
        };

        [Test] public void Area1() => Check(0);
        [Test] public void Area2() => Check(1);
        [Test] public void Area3() => Check(2);
        [Test] public void Area4() => Check(3);
        [Test] public void Area5() => Check(4);

        static void Check(int area)
        {
            var t = RunTestTables.ProtoEasy(); // 골든은 프로토 easy 값으로 기록됨(MV-SPEC2-FIX2: 표 easy는 기획서 값)
            var bad = new List<string>();
            foreach (int level in ParityRun.Levels)
            {
                string key = $"A{area + 1}L{level}";
                Golden.TryGetValue(key, out var want);
                string legacy = ParityRun.Play(t, area, level, (setup, rng) => RunSim.Create(setup, t, rng, new RunLevelBuilder()));
                string viaRule = ParityRun.Play(t, area, level, (setup, rng) =>
                {
                    var b = new RunLevelBuilder();
                    var ctx = new RunContext { Tables = t, Snapshot = setup, MetaModifiers = RunModifiers.Zero };
                    return RunSim.Create(ctx, new MainRunRule(b), rng, b);
                });
                UnityEngine.Debug.Log($"[PARITY] {key} {legacy} / {viaRule}");
                if (want != legacy) bad.Add($"{key} 기존 진입점: 골든 {(want ?? "없음")} / 지금 {legacy}");
                if (want != viaRule) bad.Add($"{key} RunContext+MainRunRule: 골든 {(want ?? "없음")} / 지금 {viaRule}");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }

    /// <summary>본편 동일 시험의 고정 save·입력·요약.</summary>
    internal static class ParityRun
    {
        public static readonly int[] Levels = { 1, 8, 15 };
        public const int MaxFrames = 7200;
        public const double Dt = 1.0 / 60;

        /// <summary>Area(0기반)·Level별 고정 save 스냅숏. 강화·덱을 조금씩 달리해 카드·계단·신 가호 경로를 지나게 한다.</summary>
        public static RunSetup Setup(int area, int level)
        {
            var s = new RunSetup
            {
                Seed = (ulong)(7001 + area * 100 + level), Area = area, Zone = 0, Level = level,
                God = area % 2 == 0 ? "thunder" : "flame", Preset = "easy",
                UpStair = area * 3, UpWeapon = area * 2, UpIncome = area,
                AupRate = 1, AupRange = 1, AupClone = area >= 2 ? 1 : 0, AupCrit = 1, AupDbl = 1,
                PerkPower = 1, PerkCrit = 1, PerkObst = 1, PerkDcap = 1,
            };
            s.Deck[1] = 1;
            s.Deck[Math.Min(8, 1 + area)] += 1;
            return s;
        }

        /// <summary>프레임 f의 드래그 목표. RunRng와 무관한 고정 곡선.</summary>
        public static double DragX(int area, int f) => 2.8 * Math.Sin(f * 0.011 + area * 0.7) * Math.Cos(f * 0.0031);

        /// <summary>한 판을 돌려 요약 문자열을 만든다. create는 (setup, RunRng) → RunSim.</summary>
        public static string Play(MvTables t, int area, int level, Func<RunSetup, IRng, RunSim> create)
        {
            var rng = new LoggingRng(new Pcg32Rng((ulong)(7001 + area * 100 + level)));
            var sim = create(Setup(area, level), rng);
            var s = sim.State;
            s.RngLog = new List<RngDraw>();
            var sb = new StringBuilder();
            int events = 0, f = 0;
            Append(sb, sim.DrainEvents(), 0, ref events);
            for (; f < MaxFrames && !s.Fin; f++)
            {
                sim.Tick(Dt, RunInput.Drag(DragX(area, f)));
                Append(sb, sim.DrainEvents(), f + 1, ref events);
            }
            sb.Append("#rng\n");
            foreach (var v in rng.Values) sb.Append(R(v)).Append('\n');
            sb.Append("#use\n");
            foreach (var d in s.RngLog) sb.Append(d).Append('\n');
            sb.Append("#result\n").Append(Result(s.Result)).Append('\n');
            sb.Append("#state\n").Append(R(s.P.X)).Append('|').Append(R(s.P.Z)).Append('|').Append(R(s.Gold)).Append('|').Append(s.Souls)
              .Append('|').Append(s.Dia).Append('|').Append(s.Objs.Count).Append('|').Append(R(s.Time)).Append('\n');
            string reason = s.Result != null ? s.Result.Reason.ToString() : "none";
            return $"f{f} e{events} r{rng.Values.Count} {reason} {Sha16(sb.ToString())}";
        }

        static void Append(StringBuilder sb, List<RunEvent> evs, int frame, ref int count)
        {
            foreach (var e in evs) { sb.Append(frame).Append(':').Append(e).Append('\n'); count++; }
        }

        public static string Result(RunResult r)
        {
            if (r == null) return "null";
            var sb = new StringBuilder();
            sb.Append(r.Reason).Append('|').Append(R(r.Gold)).Append('|').Append(r.Souls).Append('|').Append(r.Dia).Append('|').Append(r.Tok)
              .Append('|').Append(r.Pass).Append('|').Append(r.ZoneEnd).Append('|').Append(r.CpRun).Append('|').Append(r.Climbed).Append('|').Append(r.Walls)
              .Append('|').Append(r.GaugeLv).Append('|').Append(r.EqTier).Append('|').Append(r.Clones).Append('|').Append(r.CardCount).Append('|').Append(r.HitWallBar);
            sb.Append("|bars");
            if (r.BarRec != null)
                for (int fl = 0; fl < r.BarRec.Floors.Length; fl++)
                {
                    var keys = new List<int>(r.BarRec.Floors[fl].Keys);
                    keys.Sort();
                    foreach (var k in keys) sb.Append(';').Append(fl).Append(':').Append(k).Append('=').Append(R(r.BarRec.Floors[fl][k]));
                }
            else sb.Append(":null");
            sb.Append("|settle");
            foreach (var c in r.PendingSettle) sb.Append(';').Append(c);
            return sb.ToString();
        }

        static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        static string Sha16(string text)
        {
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder();
                for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }
    }

    /// <summary>RunRng에서 뽑은 값을 모두 기록한다(RunState.RngLog를 붙이기 전 생성 단계 포함).</summary>
    internal sealed class LoggingRng : IRng
    {
        readonly IRng _inner;
        public readonly List<double> Values = new List<double>();
        public LoggingRng(IRng inner) { _inner = inner; }
        public double NextDouble() { double v = _inner.NextDouble(); Values.Add(v); return v; }
    }
}
