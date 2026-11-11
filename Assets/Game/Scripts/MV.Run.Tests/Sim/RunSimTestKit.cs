using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MV.Data;

namespace MV.Run.Tests
{
    /// <summary>시험용 표 로드와 손으로 놓는 레벨(GEN 없이 SIM 규칙만 시험한다).</summary>
    internal static class RunTestTables
    {
        static MvTables _cached;

        public static MvTables Load()
        {
            if (_cached != null) return _cached;
            _cached = Load(null);
            return _cached;
        }

        /// <summary>override: 표 이름 → TSV 전체 텍스트(행 추가 시험).</summary>
        public static MvTables Load(Dictionary<string, string> overrides)
        {
            var dir = new MvTsvDirectorySource(MvTableLoader.TsvDirectory);
            jjevol.ITableSource src = dir;
            if (overrides != null)
            {
                var mem = new MvMemoryTableSource(dir);
                foreach (var kv in overrides) mem.Set(kv.Key, kv.Value);
                src = mem;
            }
            var t = new MvTables();
            t.LoadAll(src);
            t.BuildIndexes();
            return t;
        }

        public static string ReadTsv(string table) =>
            File.ReadAllText(Path.Combine(MvTableLoader.TsvDirectory, table + ".txt"), new UTF8Encoding(false));

        static MvTables _protoEasy;

        /// <summary>
        /// 프로토 easy 값(L832~833: rate 2.0, scale 3, diff 0.02, gauge 1). 06_balancePreset easy 행은 기획서 값(MV-SPEC2-FIX2)이라
        /// 프로토 덤프 대조·프로토 발사 횟수에 기대는 시험만 이 사본을 쓴다(표 파일·행 수는 바꾸지 않는다).
        /// </summary>
        public static readonly Dictionary<string, string> ProtoEasyValues = new Dictionary<string, string>
        {
            ["rate"] = "2", ["scale"] = "3", ["diff"] = "0.02", ["gauge"] = "1",
        };

        /// <summary>06 easy 행만 <see cref="ProtoEasyValues"/>로 바꾼 표(공유, 읽기 전용).</summary>
        public static MvTables ProtoEasy() => _protoEasy ??= Load(WithProtoEasy(null));

        /// <summary>override 묶음에 06 easy 행 프로토 값을 더한다.</summary>
        public static Dictionary<string, string> WithProtoEasy(Dictionary<string, string> overrides) =>
            WithPresetValues(overrides, "easy", ProtoEasyValues);

        /// <summary>override 묶음에 06 key 행 값 바꾸기를 더한다(시험 안에서만, 행 추가 없음).</summary>
        public static Dictionary<string, string> WithPresetValues(Dictionary<string, string> overrides, string key, IReadOnlyDictionary<string, string> values)
        {
            var o = overrides != null ? new Dictionary<string, string>(overrides) : new Dictionary<string, string>();
            string text = o.TryGetValue("06_balancePreset", out var cur) ? cur : ReadTsv("06_balancePreset");
            var lines = new List<string>(text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
            var header = lines[0].Split('\t');
            int ki = Array.IndexOf(header, "key");
            bool found = false;
            for (int i = 1; i < lines.Count; i++)
            {
                var c = lines[i].Split('\t');
                if (c.Length <= ki || c[ki] != key) continue;
                foreach (var kv in values) c[Array.IndexOf(header, kv.Key)] = kv.Value;
                lines[i] = string.Join("\t", c);
                found = true;
            }
            if (!found) throw new ArgumentException("06_balancePreset에 key `" + key + "` 행이 없습니다");
            o["06_balancePreset"] = string.Join("\n", lines) + "\n";
            return o;
        }

        /// <summary>key 행을 복사해 열 값을 바꾼 새 행을 끝에 붙인다.</summary>
        public static string CloneRow(string tsv, string srcKey, Dictionary<string, string> set)
        {
            var lines = new List<string>(tsv.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
            var header = lines[0].Split('\t');
            string[] src = null;
            foreach (var l in lines) { var c = l.Split('\t'); if (c.Length > 1 && Array.IndexOf(header, "key") is int ki && ki >= 0 && c[ki] == srcKey) src = c; }
            if (src == null) throw new ArgumentException(srcKey);
            var row = (string[])src.Clone();
            foreach (var kv in set) row[Array.IndexOf(header, kv.Key)] = kv.Value;
            lines.Add(string.Join("\t", row));
            return string.Join("\n", lines) + "\n";
        }
    }

    /// <summary>오브젝트를 직접 놓는 레벨 생성기(GEN 대역). 생성 규칙 자체는 MV-RUN-GEN 시험 몫.</summary>
    internal sealed class HandLevel : IRunLevelBuilder
    {
        public readonly List<Action<RunState>> Place = new List<Action<RunState>>();
        public double Gz = 400;

        public HandLevel Add(string kindKey, double x, double z, Action<RunObject> tweak = null)
        {
            Place.Add(s =>
            {
                var t = s.Tables;
                var kind = t.ObjectKind.ByKey(kindKey) ?? throw new ArgumentException(kindKey);
                var o = RunObjectFactory.Create(t, kind, x, z);
                if (o.HpRule != null && o.HpRule.type == HpRuleType.AreaHpMul)
                {
                    o.HasHp = true; o.Hp = o.Max = RunFormulas.JsRound(s.ZoneHp * o.HpRule.mul);
                }
                var rewards = o.Enemy != null ? o.Enemy.reward : kind.reward;
                if (rewards != null && rewards.Count > 0)
                {
                    var r = rewards[0];
                    o.RewardType = r.Type; o.RewardId = r.Id;
                    o.RewardAmount = r.Rule == RewardValueRule.DiaCap ? RunFormulas.DiaCap(t, s.Setup.PerkDcap) : r.ValueAtZone(s.ZoneIndex);
                }
                if (o.Move != null && o.Move.speedMax > 0) o.Spd = o.Move.speedMin + s.Draw(RngUse.EnemySpeed) * (o.Move.speedMax - o.Move.speedMin);
                tweak?.Invoke(o);
                s.AddObj(o);
            });
            return this;
        }

        public HandLevel Wall(double x, double z, double hp, FieldReward rew = FieldReward.Gold, int amt = 10, int bar = 0, int idx = 0)
        {
            return Add("wall", x, z, o =>
            {
                o.HasHp = true; o.Hp = o.Max = hp; o.Bar = bar; o.Idx = idx; o.Rew = rew;
                o.RewardType = rew == FieldReward.Soul ? ConsumeType.RunSouls : rew == FieldReward.Dia ? ConsumeType.RunDia : ConsumeType.RunGold;
                o.RewardAmount = amt;
            });
        }

        public RunState NewRun(RunSetup setup, MvTables t, IRng rng)
        {
            var s = new RunState(setup, t, rng)
            {
                Bal = RunLevelBuilder.Preset(t, setup.Preset), Area = setup.Area, ZoneIndex = setup.Zone, StartLevel = setup.Level, Cp = setup.Cp,
            };
            foreach (var a in t.Area.Datas) if (a.areaNo == setup.Area + 1) s.ZoneHp = setup.ZoneHp ?? a.hp;
            s.God = RunLevelBuilder.StartGod(t, setup.God); s.El = s.God.element;
            var pool = new List<CardData>();
            foreach (var c in t.Card.Datas) if (c.active && (c.group == 0 || c.group == s.God.cardGroup)) pool.Add(c);
            pool.Sort((a, b) => a.group != b.group ? (a.group == 0 ? -1 : b.group == 0 ? 1 : a.group.CompareTo(b.group)) : a.sort.CompareTo(b.sort));
            s.CardPool.AddRange(pool);
            s.Bt = 1;
            for (int i = 0; i < 9; i++) s.W[i] = setup.Deck[i];
            s.Clones = Math.Min(4, setup.AupClone);
            var ph = t.RunConfig.List(RunConfigKeys.FIRE_PHASE);
            for (int i = 0; i < s.FireT.Length && i < ph.Count; i++) s.FireT[i] = ph[i];
            BuildLevel(s, 0, setup.Level, setup.Bars);
            return s;
        }

        public void BuildLevel(RunState s, double baseZ, int level, BarRecord saved)
        {
            s.Level = level; s.Base = baseZ; s.Gz = Gz; s.CpRun = s.Cp; s.SavedBars = saved;
            foreach (var p in Place) p(s);
            foreach (var o in s.Objs) o.Lvl = level;
        }

        public IReadOnlyList<FieldCell> FieldSpec(int area, int floor) => Array.Empty<FieldCell>();
    }
}
