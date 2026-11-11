using System.Collections.Generic;
using MV.Data;
using NUnit.Framework;
using static MV.Run.Tests.Gen.GenTestKit;

namespace MV.Run.Tests.Gen
{
    /// <summary>
    /// D-27 골든(`QA/Dumps/gen/FIELD_{preset}.json`, Area 1~30 × Floor 1~3) 대조.
    /// ① hash 입력·결과 완전 일치(호출 순서 포함) ② 정본 FINAL 값으로 돌린 fieldSpec 칸 전부 일치
    /// ③ Baked(D-61) 경로가 같은 칸을 내는지 ④ 표 구동 FieldSpec이 표 값이 정본 FINAL과 같은 Area에서 칸 전부 일치.
    /// Area 16~30은 303 표가 08 원작 값(D-71)이라 정본 FINAL[14]와 HP가 달라 ④에서 뺀다(①~③은 30×3 전부).
    /// </summary>
    public sealed class FieldGoldenTests
    {
        [Test] public void Easy() => Run("easy");
        [Test] public void Mid() => Run("mid");
        [Test] public void Orig() => Run("orig");

        static void Run(string preset)
        {
            var t = Tables();
            var dump = LoadDump($"FIELD_{preset}.json");
            Assert.AreEqual(preset, (string)dump["preset"]);
            var bal = RunLevelBuilder.Preset(t, preset);
            // 프로토 덤프의 scale로 ①~③(FieldGen 정본 일치)을 본다. 표 행이 새 기준으로 바뀐 프리셋(easy = 기본 난이도,
            // MV-SPEC-02 Q-03·MV-SPEC2-FIX1: 0.95/10/0)은 ④ 표 구동 대조를 건너뛴다(덤프는 프로토 easy scale 3).
            double dumpScale = Num(Obj(dump["bal"])["scale"]);
            bool tableMatchesDump = dumpScale == bal.scale;
            var g = t.FieldGen.ByKey("default");
            double diaCap = LevelGen.DiaCap(t, 0);

            var fields = Arr(dump["fields"]);
            int hashTotal = 0, hashOk = 0, cellsOk = 0, bakedOk = 0, tableOk = 0, tableTried = 0;
            string first = null;
            void Fail(string msg) { if (first == null) first = msg; }

            foreach (var fo in fields)
            {
                var f = Obj(fo);
                int area = Int(f["area"]), floor = Int(f["floor"]);
                string at = $"[{preset}] Area {area} Floor {floor}";
                var fin = Arr(f["final"]);

                // ① hash 결과(덤프의 x를 그대로 넣는다)
                var soulHash = Arr(f["soulHash"]);
                var diaHash = Arr(f["diaHash"]);
                foreach (var list in new[] { soulHash, diaHash })
                    foreach (var pr in list)
                    {
                        var pair = Arr(pr);
                        double x = Num(pair[0]), h = Num(pair[1]);
                        hashTotal++;
                        double u = FieldGen.Hash(x);
                        if (u == h) hashOk++;
                        else Fail($"{at} hash({Show(x)}) 덤프 {Show(h)} / unity {Show(u)}");
                    }

                // ② 정본 FINAL 값 + 같은 시드로 fieldSpec
                var input = new FieldGen.Input
                {
                    H0 = Num(fin[0]), H1 = Num(fin[1]), Rows = Int(fin[2]), Souls = Int(fin[3]),
                    SeedArea = area - 1, Floor = floor - 1, ZoneIndex = t.ZoneIndexOfArea(area),
                    Scale = dumpScale, DiaCap = diaCap, Mode = CellMode.Hash,
                };
                if (Num(f["seed"]) != (input.SeedArea + 1) * g.seedA + input.Floor * g.seedF + g.seedC) Fail($"{at} seed {Num(f["seed"])}");
                var log = new FieldGen.HashLog();
                var cells = FieldGen.Build(g, input, log);
                var want = Arr(f["cells"]);
                // 덤프 diaHash는 모든 idx를 적지만 정본은 생선 칸에서 hash를 부르지 않는다(`else if`). 생선 칸을 빼고 순서를 맞춘다.
                var diaCalled = new List<object>();
                for (int i = 0; i < diaHash.Count && i < want.Count; i++)
                    if ((string)Obj(want[i])["rew"] != "soul") diaCalled.Add(diaHash[i]);
                string err = CompareCells(want, cells) ?? CompareLog(soulHash, log.Soul, "soulHash") ?? CompareLog(diaCalled, log.Dia, "diaHash");
                if (err == null) cellsOk++;
                else Fail($"{at} 정본 FINAL 입력: {err}");

                // ③ Baked(D-61): 덤프 칸을 굳힘 값으로 넣으면 같은 칸
                var soulCells = new List<int>();
                var soulAmts = new List<int>();
                var diaCells = new List<int>();
                foreach (var co in want)
                {
                    var c = Obj(co);
                    string rew = (string)c["rew"];
                    if (rew == "soul") { soulCells.Add(Int(c["idx"])); soulAmts.Add(Int(c["amt"])); }
                    else if (rew == "dia") diaCells.Add(Int(c["idx"]));
                }
                var baked = input;
                baked.Mode = CellMode.Baked;
                baked.SoulCells = soulCells;
                baked.SoulAmts = soulAmts;
                baked.DiaCells = diaCells;
                string berr = CompareCells(want, FieldGen.Build(g, baked));
                if (berr == null) bakedOk++;
                else Fail($"{at} Baked: {berr}");

                // ④ 표 구동(302·303·313·06·14) — 표 값이 정본 FINAL과 같을 때만
                var areaRow = LevelGen.AreaRow(t, area - 1);
                AreaFloorData row = null;
                foreach (var r in t.AreaFloor.GetGroup(areaRow.floorGroup)) if (r.floor == floor) row = r;
                if (tableMatchesDump && row != null && row.h0 == Num(fin[0]) && row.h1 == Num(fin[1]) && row.rows == Int(fin[2]) && row.souls == Int(fin[3]))
                {
                    tableTried++;
                    string terr = CompareCells(want, RunLevelBuilder.FieldSpec(t, bal, 0, area - 1, floor - 1));
                    if (terr == null) tableOk++;
                    else Fail($"{at} 표 구동: {terr}");
                }
            }

            UnityEngine.Debug.Log($"[FIELD] {preset}: hash {hashOk}/{hashTotal}, 칸(정본 입력) {cellsOk}/{fields.Count}, " +
                                  $"Baked {bakedOk}/{fields.Count}, 표 구동 {tableOk}/{tableTried}");
            Assert.AreEqual(90, fields.Count, "골든 칸 수(Area 30 × Floor 3)");
            Assert.IsNull(first, first);
            Assert.AreEqual(tableMatchesDump ? 45 : 0, tableTried, "표 구동 대조 수(Area 1~15 × 3, 표 scale이 덤프와 다르면 0)");
        }

        static string CompareCells(List<object> want, List<FieldCell> got)
        {
            if (want.Count != got.Count) return $"칸 수 덤프 {want.Count} / unity {got.Count}";
            for (int i = 0; i < want.Count; i++)
            {
                var c = Obj(want[i]);
                var u = got[i];
                string rew = u.Rew == FieldReward.Soul ? "soul" : u.Rew == FieldReward.Dia ? "dia" : "gold";
                if (Int(c["idx"]) != u.Idx || Int(c["r"]) != u.R || Int(c["l"]) != u.L || Num(c["hp"]) != u.Hp ||
                    (string)c["rew"] != rew || Int(c["amt"]) != u.Amt)
                    return $"칸 {i} 덤프 idx {c["idx"]} r {c["r"]} l {c["l"]} hp {Show(Num(c["hp"]))} {c["rew"]} {c["amt"]} / " +
                           $"unity idx {u.Idx} r {u.R} l {u.L} hp {Show(u.Hp)} {rew} {u.Amt}";
            }
            return null;
        }

        static string CompareLog(List<object> want, List<(double x, double h)> got, string name)
        {
            if (want.Count != got.Count) return $"{name} 호출 수 덤프 {want.Count} / unity {got.Count}";
            for (int i = 0; i < want.Count; i++)
            {
                var p = Arr(want[i]);
                if (Num(p[0]) != got[i].x || Num(p[1]) != got[i].h)
                    return $"{name}[{i}] 덤프 ({Show(Num(p[0]))}, {Show(Num(p[1]))}) / unity ({Show(got[i].x)}, {Show(got[i].h)})";
            }
            return null;
        }
    }
}
