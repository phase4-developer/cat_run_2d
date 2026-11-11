using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MV.Data;
using MV.Run.Tests.Gen;
using NUnit.Framework;

namespace MV.Run.Tests
{
    /// <summary>
    /// SIM 덤프 재생 대조(12 §12.3). 같은 save·입력 기록·로직 난수(ReplayRng)로 RunSim을 돌려
    /// 프레임마다 플레이어·재화·게이지·투사체 수·오브젝트(x·z·hp·dead)·사건·난수 사용 수를 맞춘다.
    /// 덤프는 저장소 `QA/Dumps/sim/`을 직접 읽는다(형식 `QA/Dumps/README.md`).
    /// </summary>
    public sealed class SimReplayTests
    {
        [Test, Ignore("Q-04: 프로토 덤프 대조 — 새 기준 대조로 교체 예정")] public void Replay_BASE() => Run("SIM_BASE.json");
        [Test, Ignore("Q-04: 프로토 덤프 대조 — 새 기준 대조로 교체 예정")] public void Replay_RUN01() => Run("SIM_RUN-01.json");
        [Test, Ignore("Q-04: 프로토 덤프 대조 — 새 기준 대조로 교체 예정")] public void Replay_RUN02() => Run("SIM_RUN-02.json");
        [Test, Ignore("Q-04: 프로토 덤프 대조 — 새 기준 대조로 교체 예정")] public void Replay_RUN03() => Run("SIM_RUN-03.json");

        static void Run(string file)
        {
            var dump = SimReplay.Load(file);
            var r = SimReplay.Play(RunTestTables.ProtoEasy(), dump); // 프로토 덤프 = 프로토 easy 값(MV-SPEC2-FIX2)
            UnityEngine.Debug.Log($"[SIM] {file}: 프레임 일치 {r.Matched}/{r.Total}" + (r.FirstMismatch != null ? $", 첫 불일치 {r.FirstMismatch}" : ""));
            Assert.IsNull(r.FirstMismatch, $"{file} 프레임 일치 {r.Matched}/{r.Total}. 첫 불일치 {r.FirstMismatch}");
            Assert.AreEqual(r.Total, r.Matched);
        }
    }

    /// <summary>
    /// 덤프 재생기. 정본 하네스(Tools/MvCapture)와 같은 순서: 프레임 f의 입력 → 한 프레임(dt) → 상태 기록(f+1).
    /// 시간: 가짜 시계 now(1000ms부터 1000/60씩 더함), dt = (now − last)/1000, 첫 프레임은 정본 boot가 last = now라 0.
    /// 입력: pointerdown(로비면 startRun) → drag{sx, px=tx}, pointermove → tx = px + (x − sx)/(W/7.2), W = 스테이지 1080. 방향키는 축.
    /// </summary>
    internal static class SimReplay
    {
        const double Eps = 1e-4;
        const double StageW = 1080;

        public sealed class Outcome
        {
            public int Total, Matched;
            public string FirstMismatch;
        }

        /// <summary>
        /// 덤프 사건 ↔ RunEvent 대응표. 덤프 사건은 정본 상태 차이로 만든 것이라(QA/Dumps/README) Unity 쪽도 같은 규칙으로 상태 차이를 만들어 맞추고,
        /// 아래 RunEvent가 나오면 같은 프레임에 짝 사건이 덤프에 있어야 한다.
        /// </summary>
        static readonly Dictionary<RunEventType, Func<RunEvent, (string kind, string last)>> EventMap =
            new Dictionary<RunEventType, Func<RunEvent, (string, string)>>
            {
                [RunEventType.RunStarted] = e => ("mode", "run"),
                [RunEventType.ClimbStart] = e => ("mode", "cannon"),
                [RunEventType.ClimbEnd] = e => ("mode", "run"),
                [RunEventType.RunFinished] = e => ("finish", null),
                [RunEventType.FloorEnter] = e => ("floor", e.A.ToString(CultureInfo.InvariantCulture)),
                [RunEventType.StairsReceived] = e => e.A > 0 ? ("stairs", e.B.ToString(CultureInfo.InvariantCulture)) : (null, null),
                [RunEventType.CardApplied] = e => ("cards", null),
                [RunEventType.Break] = e => ("dead", null),
                [RunEventType.Steal] = e => ("dead", null),
            };

        public static Dictionary<string, object> Load(string file)
        {
            var path = Path.Combine(GenTestKit.RepoRoot, "QA", "Dumps", "sim", file);
            if (!File.Exists(path)) throw new FileNotFoundException("SIM 덤프가 없습니다", path);
            return (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(path, new UTF8Encoding(false)));
        }

        // ---------------- save → RunSetup ----------------

        public static RunSetup SetupFromScene(Dictionary<string, object> scene)
        {
            var save = scene.TryGetValue("save", out var sv) ? sv as Dictionary<string, object> : null;
            var setup = new RunSetup
            {
                Seed = (ulong)Num(scene["seed"]),
                Preset = scene.TryGetValue("preset", out var p) && p is string ps ? ps : "easy",
                Level = 1, Area = 0, Zone = 0, God = "thunder",
            };
            setup.Deck[1] = 1; // DEF().deck
            if (save == null) return setup; // DEF()
            setup.Level = Int(save["level"]);
            setup.Area = Int(save["area"]);
            setup.Zone = Int(save["zone"]);
            setup.Souls = Int(save["souls"]);
            setup.Cp = Int(save["cp"]);
            setup.God = (string)save["god"];
            var up = Obj(save["up"]);
            setup.UpStair = Int(up["stair"]); setup.UpWeapon = Int(up["weapon"]); setup.UpIncome = Int(up["income"]);
            var deck = Arr(save["deck"]);
            for (int i = 0; i < setup.Deck.Length; i++) setup.Deck[i] = i < deck.Count ? Int(deck[i]) : 0;
            var aup = Obj(save["aup"]);
            setup.AupRate = Int(aup["rate"]); setup.AupRange = Int(aup["range"]); setup.AupClone = Int(aup["clone"]);
            setup.AupCrit = Int(aup["crit"]); setup.AupDbl = Int(aup["dbl"]);
            var perks = Obj(Obj(save["rb"])["perks"]);
            setup.PerkPower = Int(perks["power"]); setup.PerkCrit = Int(perks["crit"]); setup.PerkObst = Int(perks["obst"]); setup.PerkDcap = Int(perks["dcap"]);
            if (save.TryGetValue("bars", out var bars) && bars is Dictionary<string, object> bd && bd.Count > 0)
                throw new NotSupportedException("bars가 있는 save 재생은 아직 없다(덤프 3판 모두 빈 bars)");
            return setup;
        }

        // ---------------- 재생 ----------------

        public static Outcome Play(MvTables t, Dictionary<string, object> dump)
        {
            var scene = Obj(dump["scene"]);
            var rows = Arr(dump["rows"]);
            var inputs = scene.TryGetValue("inputs", out var ii) && ii != null ? Arr(ii) : new List<object>();
            var setup = SetupFromScene(scene);

            // 로직 난수(덤프 순서 그대로)
            var rngVals = new List<double>();
            var rngLines = new List<int>();
            var rngUpTo = new int[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                foreach (var x in Arr(Obj(rows[i])["rng"])) { var a = Arr(x); rngLines.Add(Int(a[1])); rngVals.Add(Num(a[2])); }
                rngUpTo[i] = rngVals.Count;
            }
            var rng = new ReplayRng(rngVals);

            var cardKey = new Dictionary<int, string>();
            foreach (var c in t.Card.Datas) cardKey[c.id] = c.key;

            var outc = new Outcome { Total = rows.Count };
            RunSim sim = null;
            int logStart = 0;
            bool dragging = false; double dragSx = 0, dragPx = 0;
            int keyL = 0, keyR = 0;
            const double frameMs = 1000.0 / 60;
            double now = 1000, last = 1000;
            bool first = true;

            // 비교 상태
            var ids = new Dictionary<RunObject, int>();
            int nextId = 1;
            var mirror = new Dictionary<int, double[]>(); // id → x, z, hp(NaN=null), dead
            var mirrorKind = new Dictionary<int, string>();
            string pm = null; int? pl = null, pf = null, pst = null, pcl = null; string pc = ""; bool pfin = false;

            for (int fi = 0; fi < rows.Count; fi++)
            {
                var row = Obj(rows[fi]);
                int f = Int(row["f"]);
                string at = $"f{f}";
                double? dragTarget = null;

                // ① 입력(프레임 fi)
                foreach (var io in inputs)
                {
                    var e = Obj(io);
                    if (Int(e["frame"]) != fi) continue;
                    string type = (string)e["type"];
                    if (type == "key")
                    {
                        int v = (string)e["state"] == "down" ? 1 : 0;
                        if ((string)e["key"] == "ArrowLeft") keyL = v; else keyR = v;
                        continue;
                    }
                    double x = Num(e["x"]);
                    if (type == "down" || type == "tap")
                    {
                        if (sim == null)
                        {
                            sim = RunSim.Create(setup, t, rng, new RunLevelBuilder());
                            sim.State.RngLog = new List<RngDraw>();
                            logStart = rng.Position;
                        }
                        dragging = true; dragSx = x; dragPx = dragTarget ?? sim.State.P.Tx;
                    }
                    if (type == "move" && dragging && sim != null) dragTarget = dragPx + (x - dragSx) / (StageW / 7.2);
                    if (type == "up" || type == "tap") dragging = false;
                }
                if (sim == null) { outc.FirstMismatch = $"{at}: 로비에서 런이 시작되지 않았다(입력 기록 확인)"; return outc; }

                // ② 한 프레임
                now += frameMs;
                if (first) { last = now; first = false; }
                double dt = (now - last) / 1000; last = now;
                var input = new RunInput { DragTargetX = dragTarget, KeyAxis = keyR - keyL };
                try { sim.Tick(dt, input); }
                catch (Exception ex) { outc.FirstMismatch = $"{at}: 예외 {ex.GetType().Name} {ex.Message}"; return outc; }
                var s = sim.State;
                var events = sim.DrainEvents();

                // ③ 비교
                string err = CheckRng(s, rng, rngUpTo[fi], rngLines, logStart, row);
                if (err == null) err = CheckMode(s, (string)row["mode"]);
                if (err == null && row.TryGetValue("s", out var sObj) && sObj != null) err = CheckS(s, Arr(sObj));
                if (err == null) err = CheckObjects(s, row, ids, ref nextId, mirror, mirrorKind);
                var ours = new List<string>();
                if (err == null)
                {
                    DeriveEvents(s, ids, cardKey, ours, ref pm, ref pl, ref pf, ref pst, ref pcl, ref pc, ref pfin);
                    err = CheckEvents(row, ours, events, ids);
                }
                if (err != null) { outc.FirstMismatch = $"{at}: {err}"; return outc; }
                outc.Matched++;
            }
            if (!rng.Exhausted) outc.FirstMismatch = $"끝: 난수를 덜 뽑았다({rng.Position}/{rngVals.Count})";
            return outc;
        }

        static string CheckRng(RunState s, ReplayRng rng, int want, List<int> lines, int logStart, Dictionary<string, object> row)
        {
            for (int i = 0; i < s.RngLog.Count; i++)
            {
                int k = logStart + i;
                if (k < lines.Count && (int)s.RngLog[i].Use != lines[k])
                    return $"난수 {k}번째 사용처 덤프 {lines[k]} / unity {(int)s.RngLog[i].Use}({s.RngLog[i].Use})";
            }
            if (rng.Position != want)
            {
                var dl = string.Join(",", Arr(row["rng"]).Select(x => Int(Arr(x)[1]).ToString(CultureInfo.InvariantCulture)));
                var ul = string.Join(",", s.RngLog.Skip(Math.Max(0, s.RngLog.Count - 6)).Select(d => ((int)d.Use).ToString(CultureInfo.InvariantCulture)));
                return $"난수 사용 수 덤프 {want} / unity {rng.Position} (덤프 이 프레임 [{dl}], unity 최근 [{ul}])";
            }
            return null;
        }

        static string PhaseName(RunState s)
        {
            switch (s.Phase)
            {
                case RunPhase.Lobby: return "lobby";
                case RunPhase.Climb: return "cannon";
                case RunPhase.Result: return "result";
                default: return "run";
            }
        }

        static string CheckMode(RunState s, string mode) =>
            PhaseName(s) == mode ? null : $"mode 덤프 {mode} / unity {PhaseName(s)}";

        static string CheckS(RunState s, List<object> v)
        {
            // p.x, p.z, p.tx, R.gold, R.souls, R.dia, gauge.lv, gauge.fill, projs, R.stairs, R.climbed, save.level, R.floor, R.time
            string e;
            if ((e = Near("p.x", v[0], s.P.X)) != null) return e;
            if ((e = Near("p.z", v[1], s.P.Z)) != null) return e;
            if ((e = Near("p.tx", v[2], s.P.Tx)) != null) return e;
            if ((e = Near("R.gold", v[3], s.Gold)) != null) return e;
            if ((e = Exact("R.souls", v[4], s.Souls)) != null) return e;
            if ((e = Exact("R.dia", v[5], s.Dia)) != null) return e;
            if ((e = Exact("gauge.lv", v[6], s.GaugeLv)) != null) return e;
            if ((e = Near("gauge.fill", v[7], s.GaugeFill)) != null) return e;
            if ((e = Exact("projs", v[8], s.Projs.Count)) != null) return e;
            if ((e = Exact("R.stairs", v[9], s.Stairs)) != null) return e;
            if ((e = Exact("R.climbed", v[10], s.Climbed)) != null) return e;
            if ((e = Exact("save.level", v[11], s.Level)) != null) return e;
            if ((e = Exact("R.floor", v[12], s.Floor)) != null) return e;
            if ((e = Near("R.time", v[13], s.Time)) != null) return e;
            return null;
        }

        /// <summary>표 종류 → 정본 k(보너스 상자·Area 아치는 정본에서 coin/gem/arch). 시험 쪽 표기 맞춤일 뿐 규칙 분기가 아니다.</summary>
        static string ProtoKind(RunObject o)
        {
            switch (o.Kind.key)
            {
                case "coinBonus": return "coin";
                case "gemBonus": return "gem";
                case "archGoal": return "arch";
                default: return o.Kind.key;
            }
        }

        static string CheckObjects(RunState s, Dictionary<string, object> row, Dictionary<RunObject, int> ids, ref int nextId,
            Dictionary<int, double[]> mirror, Dictionary<int, string> mirrorKind)
        {
            // 덤프 쪽 거울 갱신
            var dAdd = new List<int>();
            foreach (var x in Arr(row["add"]))
            {
                var a = Arr(x); int id = Int(a[0]);
                mirror[id] = new[] { Num(a[2]), Num(a[3]), a[4] == null ? double.NaN : Num(a[4]), Num(a[5]), a[6] == null ? double.NaN : Num(a[6]) };
                mirrorKind[id] = (string)a[1];
                dAdd.Add(id);
            }
            foreach (var x in Arr(row["chg"]))
            {
                var a = Arr(x); int id = Int(a[0]);
                var m = mirror[id];
                m[0] = Num(a[1]); m[1] = Num(a[2]); m[2] = a[3] == null ? double.NaN : Num(a[3]); m[3] = Num(a[4]);
            }
            var dRm = new HashSet<int>();
            foreach (var x in Arr(row["rm"])) { int id = Int(x); dRm.Add(id); mirror.Remove(id); mirrorKind.Remove(id); }

            // Unity 쪽: 새로 나타난 순서대로 id
            var uAdd = new List<int>();
            var seen = new HashSet<int>();
            foreach (var o in s.Objs)
            {
                if (!ids.TryGetValue(o, out int id)) { id = nextId++; ids[o] = id; uAdd.Add(id); }
                seen.Add(id);
            }
            var uRm = new HashSet<int>();
            foreach (var kv in ids.ToList()) if (!seen.Contains(kv.Value)) { uRm.Add(kv.Value); ids.Remove(kv.Key); }

            if (!dAdd.SequenceEqual(uAdd)) return $"새 오브젝트 덤프 [{string.Join(",", dAdd)}] / unity [{string.Join(",", uAdd)}]";
            if (!dRm.SetEquals(uRm)) return $"빠진 오브젝트 덤프 [{string.Join(",", dRm)}] / unity [{string.Join(",", uRm)}]";

            foreach (var o in s.Objs)
            {
                int id = ids[o];
                if (!mirror.TryGetValue(id, out var m)) return $"obj{id}: 덤프에 없음";
                string k = mirrorKind[id], uk = ProtoKind(o);
                if (k != uk) return $"obj{id} 종류 덤프 {k} / unity {uk}({o.Kind.key})";
                string e;
                if ((e = Near($"obj{id}({k}).x", m[0], o.X)) != null) return e;
                if ((e = Near($"obj{id}({k}).z", m[1], o.Z)) != null) return e;
                if (!double.IsNaN(m[2]) && (e = Near($"obj{id}({k}).hp", m[2], o.Hp)) != null) return e;
                if ((m[3] != 0) != o.Dead) return $"obj{id}({k}).dead 덤프 {m[3]} / unity {o.Dead}";
                if (uAdd.Contains(id) && !double.IsNaN(m[4]) && (e = Near($"obj{id}({k}).max", m[4], o.Max)) != null) return e;
            }
            return null;
        }

        /// <summary>덤프 하네스(sim.js PAGE_PROBE)와 같은 규칙으로 Unity 상태 차이에서 사건을 만든다.</summary>
        static void DeriveEvents(RunState s, Dictionary<RunObject, int> ids, Dictionary<int, string> cardKey, List<string> ev,
            ref string pm, ref int? pl, ref int? pf, ref int? pst, ref int? pcl, ref string pc, ref bool pfin)
        {
            string mode = PhaseName(s);
            if (mode != pm) { ev.Add($"mode|{pm ?? "null"}|{mode}"); pm = mode; }
            if (s.Level != pl) { if (pl != null) ev.Add($"level|{pl}|{s.Level}"); pl = s.Level; }
            if (s.Floor != pf) { if (pf != null) ev.Add($"floor|{pf}|{s.Floor}"); pf = s.Floor; }
            if (s.Stairs != pst) { if (pst != null) ev.Add($"stairs|{pst}|{s.Stairs}"); pst = s.Stairs; }
            if (s.Climbed != pcl) { if (pcl != null) ev.Add($"climbed|{pcl}|{s.Climbed}"); pcl = s.Climbed; }
            string cs = CardsKey(s.Cards.Select(kv => new KeyValuePair<string, int>(cardKey.TryGetValue(kv.Key, out var k) ? k : kv.Key.ToString(), kv.Value)));
            if (cs != pc) { ev.Add("cards|" + cs); pc = cs; }
            if (s.Fin && !pfin) { ev.Add($"finish|{s.Reason.ToString().ToLowerInvariant()}|{(s.Result != null && s.Result.Pass ? 1 : 0)}"); pfin = true; }
            // dead 사건은 만들지 않는다: CheckObjects가 프레임마다 모든 오브젝트의 dead를 맞추므로 같은 내용이다.
        }

        static string CardsKey(IEnumerable<KeyValuePair<string, int>> kv) =>
            "{" + string.Join(",", kv.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value)) + "}";

        static string DumpEvent(List<object> e)
        {
            string k = (string)e[0];
            switch (k)
            {
                case "cards":
                    var d = Obj(e[1]);
                    return "cards|" + CardsKey(d.Select(p => new KeyValuePair<string, int>(p.Key, Int(p.Value))));
                case "finish": return $"finish|{(string)e[1]}|{((bool)e[2] ? 1 : 0)}";
                case "dead": return $"dead|{Int(e[1])}";
                default: return $"{k}|{(e[1] == null ? "null" : Str(e[1]))}|{Str(e[2])}";
            }
        }

        static string CheckEvents(Dictionary<string, object> row, List<string> ours, List<RunEvent> events, Dictionary<RunObject, int> ids)
        {
            var dump = Arr(row["ev"]).Select(x => DumpEvent(Arr(x))).ToList();
            var dumpNoDead = dump.Where(x => !x.StartsWith("dead|", StringComparison.Ordinal)).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var oursSorted = ours.OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (!dumpNoDead.SequenceEqual(oursSorted))
                return $"사건 덤프 [{string.Join(" ; ", dumpNoDead)}] / unity [{string.Join(" ; ", oursSorted)}]";

            // RunEvent → 덤프 사건(대응표): 같은 프레임에 짝 사건이 있어야 한다.
            var byObjId = new Dictionary<int, int>();
            foreach (var kv in ids) byObjId[kv.Key.Id] = kv.Value;
            foreach (var e in events)
            {
                if (!EventMap.TryGetValue(e.Type, out var map)) continue;
                var (kind, lastVal) = map(e);
                if (kind == null) continue;
                if (kind == "dead")
                {
                    if (!byObjId.TryGetValue(e.ObjId, out int did)) return $"RunEvent {e.Type} obj {e.ObjId}: 대상이 없다";
                    lastVal = did.ToString(CultureInfo.InvariantCulture);
                }
                bool ok = dump.Any(d => d.StartsWith(kind + "|", StringComparison.Ordinal) && (lastVal == null || d.EndsWith("|" + lastVal, StringComparison.Ordinal)));
                if (!ok) return $"RunEvent {e.Type} 짝 사건({kind} {lastVal})이 덤프 이 프레임에 없다 [{string.Join(" ; ", dump)}]";
            }
            return null;
        }

        // ---------------- 숫자 ----------------

        static string Near(string what, object dump, double unity) => Near(what, Num(dump), unity);

        static string Near(string what, double d, double u) =>
            Math.Abs(d - u) <= Eps ? null : $"{what} 덤프 {Show(d)} / unity {Show(u)}";

        static string Exact(string what, object dump, double unity)
        {
            double d = Num(dump);
            return d == unity ? null : $"{what} 덤프 {Show(d)} / unity {Show(unity)}";
        }

        static string Str(object o) => o is string s ? s : Show(Num(o));
        static string Show(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;
        static List<object> Arr(object o) => (List<object>)o;
        static double Num(object o) => o == null ? double.NaN : Convert.ToDouble(o, CultureInfo.InvariantCulture);
        static int Int(object o) => (int)Num(o);
    }
}
