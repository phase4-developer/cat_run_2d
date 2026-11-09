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
    /// 골든 = 「프레임 수 / 이벤트 수 / RunRng 소비 수 / 종료 사유 / SHA256 앞 16자」(커밋 c2a69fa에서 기록, 잡 43876e9a0589).
    /// 두 경로를 다 본다: 기존 진입점 <c>RunSim.Create(setup, …)</c>(규칙 생략 = MainRunRule)과
    /// <c>RunSim.Create(RunContext, MainRunRule, …)</c>(① Build → ② BuildCourse, 모디파이어 0·동행 없음).
    /// </summary>
    public sealed class MainParityTests
    {
        /// <summary>확장 지점 전 코드로 만든 값. 키 A{area}L{level}(area 1기반).</summary>
        static readonly Dictionary<string, string> Golden = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["A1L1"] = "f3181 e473 r238 Wall 71bb533fd4b11561",
            ["A1L8"] = "f4036 e637 r302 Wall 4c77a7619eec48f3",
            ["A1L15"] = "f4047 e681 r290 Wall 9b979a6c5766a80c",
            ["A2L1"] = "f3773 e838 r261 Wall 5adcb29567c32a6d",
            ["A2L8"] = "f3991 e579 r299 Wall 6bd20b477d519b6b",
            ["A2L15"] = "f3721 e522 r279 Wall b4530fd749af4407",
            ["A3L1"] = "f3538 e1052 r570 Wall 25e8706e54d73849",
            ["A3L8"] = "f3923 e1020 r580 Wall 9ea3a878b363ea7b",
            ["A3L15"] = "f4042 e1297 r569 Wall 2d4976b6f72b973e",
            ["A4L1"] = "f3273 e875 r481 Wall bdc1cd89e6597266",
            ["A4L8"] = "f3959 e1037 r582 Wall 8747be5aa7c79fe3",
            ["A4L15"] = "f3798 e1018 r561 Wall ddcff8a0719fb060",
            ["A5L1"] = "f3621 e1087 r563 Wall 43bbfa08f3284f00",
            ["A5L8"] = "f4223 e1092 r606 Wall 2deaa4ad4d22a2c1",
            ["A5L15"] = "f3805 e939 r562 Wall 179ba6fe1587e267",
        };

        [Test] public void Area1() => Check(0);
        [Test] public void Area2() => Check(1);
        [Test] public void Area3() => Check(2);
        [Test] public void Area4() => Check(3);
        [Test] public void Area5() => Check(4);

        static void Check(int area)
        {
            var t = RunTestTables.Load();
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
