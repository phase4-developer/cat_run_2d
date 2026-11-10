using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MV.Data;
using NUnit.Framework;
using static MV.Run.Tests.Gen.GenTestKit;

namespace MV.Run.Tests.Gen
{
    /// <summary>
    /// 프로토 생성 덤프(`QA/Dumps/gen/GEN_{preset}.json`, 12 §12.3) 전 필드 대조: Level 1~20 × Area 1~15 × Floor 1~3.
    /// 덤프 난수는 상수 0.5(04 §4.17.2-5). 그림 위상 `ph`만 뺀다. 좌표·크기·속도는 1e-4, 정수(hp·보상·칸 번호·레벨)는 완전 일치(12 §12.3.3).
    /// </summary>
    public sealed class GenDumpTests
    {
        const double Tol = 1e-4;

        [Test, Ignore("Q-04: 프로토 덤프 대조 — 새 기준 대조로 교체 예정")] public void Easy() => Run("easy");
        [Test, Ignore("Q-04: 프로토 덤프 대조 — 새 기준 대조로 교체 예정")] public void Mid() => Run("mid");
        [Test, Ignore("Q-04: 프로토 덤프 대조 — 새 기준 대조로 교체 예정")] public void Orig() => Run("orig");

        static void Run(string preset)
        {
            var t = Tables();
            var dump = LoadDump($"GEN_{preset}.json");
            Assert.AreEqual(preset, (string)dump["preset"]);
            var bal = Obj(dump["bal"]);
            var p = RunLevelBuilder.Preset(t, preset);
            Assert.AreEqual(Num(bal["scale"]), p.scale, "06_balancePreset.scale");
            double c = Num(Obj(dump["rng"])["c"]);
            var saveBase = Obj(dump["saveBase"]);
            var deck = Arr(saveBase["deck"]);
            var objKeys = Obj(dump["objKeys"]);

            var entries = Arr(dump["entries"]);
            int matched = 0;
            string first = null;
            foreach (var eo in entries)
            {
                var e = Obj(eo);
                int level = Int(e["level"]), area = Int(e["area"]), floor = Int(e["floor"]);
                var setup = new RunSetup
                {
                    Area = area - 1, Zone = (area - 1) / 5, Level = level, Cp = Int(e["cp"]), Bars = null, Preset = preset,
                };
                for (int i = 0; i < deck.Count && i < setup.Deck.Length; i++) setup.Deck[i] = Int(deck[i]);

                var s = new RunLevelBuilder().NewRun(setup, t, new ConstRng(c));
                string err = Compare(t, s, e, objKeys);
                if (err == null) matched++;
                else if (first == null) first = $"[{preset}] Level {level} Area {area} Floor {floor}: {err}";
            }
            UnityEngine.Debug.Log($"[GEN] {preset}: 일치 {matched}/{entries.Count}");
            Assert.AreEqual(entries.Count, matched, $"GEN {preset} 일치 {matched}/{entries.Count}. 첫 불일치 {first}");
            Assert.AreEqual(900, entries.Count, "덤프 칸 수");
        }

        static string Compare(MvTables t, RunState s, Dictionary<string, object> e, Dictionary<string, object> objKeys)
        {
            string tplKey = t.Template.Get(s.Tpl)?.key;
            if (tplKey != "tpl" + Int(e["tpl"])) return $"tpl 덤프 {Int(e["tpl"])} / unity {tplKey}";
            if (!Near(Num(e["base"]), s.Base)) return $"base 덤프 {Num(e["base"])} / unity {s.Base}";
            if (!Near(Num(e["gz"]), s.Gz)) return $"gz 덤프 {Show(Num(e["gz"]))} / unity {Show(s.Gz)}";
            var z = Obj(e["Z"]);
            if (Num(z["hp"]) != s.ZoneHp) return $"Z.hp 덤프 {Num(z["hp"])} / unity {s.ZoneHp}";
            if (Int(z["zi"]) != s.ZoneIndex) return $"Z.zi 덤프 {Int(z["zi"])} / unity {s.ZoneIndex}";
            if (Int(e["cp"]) != s.Cp || s.CpRun != s.Cp) return $"cp 덤프 {Int(e["cp"])} / unity {s.Cp},{s.CpRun}";
            if (Int(e["level"]) != s.Level) return $"level 덤프 {Int(e["level"])} / unity {s.Level}";

            var gates = Arr(e["gates"]);
            if (gates.Count != s.Gates.Count) return $"gates 수 덤프 {gates.Count} / unity {s.Gates.Count}";
            for (int i = 0; i < gates.Count; i++)
            {
                var g = Obj(gates[i]);
                if (!Near(Num(g["z"]), s.Gates[i].Z) || Int(g["floor"]) != s.Gates[i].Floor)
                    return $"gates[{i}] 덤프 z {Show(Num(g["z"]))} floor {Int(g["floor"])} / unity z {Show(s.Gates[i].Z)} floor {s.Gates[i].Floor}";
            }

            var objs = Arr(e["objs"]);
            if (objs.Count != s.Objs.Count) return $"objs 수 덤프 {objs.Count} / unity {s.Objs.Count}";
            for (int i = 0; i < objs.Count; i++)
            {
                var row = Arr(objs[i]);
                string k = (string)row[0];
                var o = s.Objs[i];
                if (o.Id != i + 1) return $"objs[{i}] id {o.Id}";
                string uk = ProtoKind(o);
                if (uk != k) return $"objs[{i}] 종류 덤프 {k} / unity {o.Kind.key}";
                var keys = Arr(objKeys[k]);
                var vals = new Dictionary<string, object>(StringComparer.Ordinal);
                for (int j = 0; j < keys.Count; j++) vals[(string)keys[j]] = row[j + 1];
                string err = CompareObj(k, vals, o);
                if (err != null) return $"objs[{i}] {k}.{err}";
            }
            return null;
        }

        /// <summary>표 종류 → 정본 kind(보너스 상자·Area 아치는 정본에서 coin/gem/arch).</summary>
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

        static readonly Regex Digits = new Regex(@"\d+");

        static string CompareObj(string k, Dictionary<string, object> v, RunObject o)
        {
            foreach (var kv in v)
            {
                string f = kv.Key;
                object d = kv.Value;
                string err = null;
                switch (f)
                {
                    case "ph": break; // 그림 위상(FxRng), 대조 안 함
                    case "x": err = NearF(d, o.X); break;
                    case "z": err = NearF(d, o.Z); break;
                    case "hw": err = NearF(d, o.Hw); break;
                    case "hd": err = NearF(d, o.Hd); break;
                    case "spd": err = NearF(d, o.Spd); break;
                    case "hp": err = !o.HasHp ? "체력 없음" : ExactF(d, o.Hp); break;
                    case "max": err = !o.HasHp ? "체력 없음" : ExactF(d, o.Max); break;
                    case "lvl": err = ExactF(d, o.Lvl); break;
                    case "pend": err = ExactF(d, o.Pend); break;
                    case "ft": err = ExactF(d, o.Ft); break;
                    case "F": err = ExactF(d, o.F); break;
                    case "n": err = ExactF(d, o.N); break;
                    case "made": err = ExactF(d, o.Made); break;
                    case "sink": err = ExactF(d, o.Sink); break;
                    case "done": err = (bool)d == o.Done ? null : $"덤프 {d} / unity {o.Done}"; break;
                    case "used": err = (bool)d == o.Used ? null : $"덤프 {d} / unity {o.Used}"; break;
                    case "reward":
                    case "dia":
                        err = BoxReward(v, o);
                        break;
                    case "gate":
                    {
                        bool gate = d is bool b && b;
                        string want = gate ? "ui.run.areaGate" : "ui.run.floorN";
                        if (o.LabelKey != want) err = $"덤프 gate {gate} / unity LabelKey {o.LabelKey}";
                        break;
                    }
                    case "label":
                    {
                        var m = Digits.Match((string)d);
                        if (!m.Success || int.Parse(m.Value) != o.LabelArg) err = $"덤프 {d} / unity {o.LabelKey}({o.LabelArg})";
                        break;
                    }
                    case "bar": err = ExactF(d, o.Bar); break;
                    case "row": err = ExactF(d, o.Row); break;
                    case "lane": err = ExactF(d, o.Lane); break;
                    case "idx": err = ExactF(d, o.Idx); break;
                    case "rew":
                    {
                        var rew = (string)d == "soul" ? FieldReward.Soul : (string)d == "dia" ? FieldReward.Dia : FieldReward.Gold;
                        var type = rew == FieldReward.Soul ? ConsumeType.RunSouls : rew == FieldReward.Dia ? ConsumeType.RunDia : ConsumeType.RunGold;
                        if (o.Rew != rew || o.RewardType != type) err = $"덤프 {d} / unity {o.Rew} {o.RewardType}";
                        break;
                    }
                    case "amt": err = ExactF(d, o.RewardAmount); break;
                    default: err = "비교 규칙이 없는 덤프 키"; break;
                }
                if (err != null) return $"{f}: {err}";
            }
            return null;
        }

        /// <summary>상자·쥐 보상: 정본은 파괴 때 dia > 0이면 캣닢 dia, 아니면 츄르 reward(× goldMul). Unity는 한 항목으로 푼다.</summary>
        static string BoxReward(Dictionary<string, object> v, RunObject o)
        {
            double dia = v.TryGetValue("dia", out var dv) && dv != null ? Num(dv) : 0;
            double reward = v.TryGetValue("reward", out var rv) && rv != null ? Num(rv) : 0;
            var type = dia > 0 ? ConsumeType.RunDia : ConsumeType.RunGold;
            double amount = dia > 0 ? dia : reward;
            if (o.RewardType != type || o.RewardAmount != amount)
                return $"덤프 reward {reward} dia {dia} / unity {o.RewardType} {o.RewardAmount}";
            return null;
        }

        static bool Near(double a, double b) => Math.Abs(a - b) <= Tol;
        static string NearF(object d, double u) => d != null && Near(Num(d), u) ? null : $"덤프 {(d == null ? "null" : Show(Num(d)))} / unity {Show(u)}";
        static string ExactF(object d, double u) => d != null && Num(d) == u ? null : $"덤프 {(d == null ? "null" : Show(Num(d)))} / unity {Show(u)}";
    }
}
