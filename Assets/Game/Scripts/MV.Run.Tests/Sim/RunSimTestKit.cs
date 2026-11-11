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
            s.God = t.God.ByKey(setup.God); s.El = s.God.element;
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
