using System.Collections.Generic;
using System.IO;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 계단 게이트(sgate)·지팡이 공방 게이트(cgate) 진단: 실제 프리팹 + 실제 Physics로 투사체가 맞는지,
    /// 맞으면 충전(N·Made·Prog)이 오르는지, 글자판(<see cref="CountLabel"/>)의 게이지가 켜지고 채워지는지 본다.
    /// </summary>
    public sealed class GateHitGaugeTests
    {
        const string StageMap = "Stage1";
        static readonly string[] GateKeys = { "sgate", "cgate" };
        readonly List<GameObject> _made = new List<GameObject>();
        MvTables _t;

        [SetUp]
        public void SetUp() => _t = ViewTestTables.Load();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        static GameObject LoadPrefab(string address)
        {
            foreach (var guid in AssetDatabase.FindAssets(address + " t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == address) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        RunObject Gate(MvTables t, string key, double x, double z)
        {
            var o = RunObjectFactory.Create(t, t.ObjectKind.ByKey(key), x, z);
            Assert.That(o, Is.Not.Null, key);
            return o;
        }

        [Test]
        public void GatePrefabs_HaveHitbox_AndCountLabelGauge([ValueSource(nameof(GateKeys))] string key)
        {
            var o = Gate(_t, key, 0, 10);
            string address = ObjectViewFactory.PrefabFor(_t, StageMap, o);
            var prefab = LoadPrefab(address);
            Assert.That(prefab, Is.Not.Null, $"{key} 프리팹 {address}");
            Assert.That(prefab.GetComponentsInChildren<RunHitbox>(true), Is.Not.Empty, $"{address} RunHitbox");

            var inst = Object.Instantiate(prefab);
            _made.Add(inst);
            var label = CountLabel.Find(inst.transform);
            Assert.That(label, Is.Not.Null, $"{address} 글자판(Count_Number)");
            Assert.That(label.GaugeRoot, Is.Not.Null, $"{address} Count_Gauge");
            Assert.That(label.Pivot, Is.Not.Null, $"{address} Gauge_Pivot");
            TestContext.WriteLine($"{address}: 글자판 부모 = {label.transform.parent.name}, Billboard 아래 = {label.GetComponentInParent<Billboard>(true) != null}");

            float baseX = label.Pivot.localScale.x;
            o.Prog = 0.5;
            o.N = 3;
            o.Made = 3;
            var c = ObjectLabel.Of(o, 0, 3, 0, 1);
            Assert.That(c.Visible, Is.True, "글자판 보임");
            Assert.That(c.Gauge, Is.EqualTo(0.5f).Within(1e-6), "게이지 값 = Prog");
            label.Apply(c);
            Assert.That(label.GaugeRoot.activeInHierarchy, Is.True, "게이지 켜짐");
            Assert.That(label.Pivot.localScale.x, Is.EqualTo(baseX * 0.5f).Within(1e-5), "게이지 채움 = Prog");
            var bar = label.GaugeRoot.GetComponent<SpriteRenderer>();
            var fill = label.Pivot.GetComponentInChildren<SpriteRenderer>(true);
            foreach (var r in new[] { bar, fill })
            {
                Assert.That(r, Is.Not.Null, "게이지 SpriteRenderer");
                TestContext.WriteLine($"  {r.name}: sprite={(r.sprite != null ? r.sprite.name : "null")}, mat={(r.sharedMaterial != null ? r.sharedMaterial.name : "null")}, color={r.color}, order={r.sortingOrder}, enabled={r.enabled}, worldPos={r.transform.position}");
                Assert.That(r.enabled && r.sprite != null && r.sharedMaterial != null && r.color.a > 0, Is.True, $"{r.name} 그려질 조건");
            }
        }

        /// <summary>실제 RunSim + 실제 Physics: 게이트 앞에서 쏜 투사체가 게이트에 맞고, 충전이 오르는지.</summary>
        [Test]
        public void RunSim_WithRealPhysics_GateChargesOnHit([ValueSource(nameof(GateKeys))] string key)
        {
            var sim = RunSim.Create(new RunSetup { Deck = new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 } }, _t, new Pcg32Rng(1), new RunLevelBuilder());
            var s = sim.State;
            var go = new GameObject("HitWorldTest");
            _made.Add(go);
            var w = go.AddComponent<RunHitWorld>();
            w.Init(o => ObjectViewFactory.PrefabFor(_t, StageMap, o), LoadPrefab);
            sim.HitQuery = w;
            s.Objs.Clear();
            s.Projs.Clear();
            double pz = s.P.Z, px = s.P.X;
            var gate = Gate(_t, key, px, pz + 8);
            gate.Id = 2001;
            s.Objs.Add(gate);

            var shots = new List<RunProjectile>();
            for (int k = 0; k < 6; k++)
            {
                var b = new RunProjectile { Id = 900000 + k, X = px, Z = pz, Sz = pz, D = 5, Pierce = 0 };
                shots.Add(b);
                s.Projs.Add(b);
                for (int i = 0; i < 120 && !b.Dead; i++) sim.Tick(1.0 / 60, RunInput.None);
                TestContext.WriteLine($"{key} 탄 {k}: hit={(b.HitIds != null ? string.Join(",", b.HitIds) : "-")}, dead={b.Dead}, z={b.Z:F2}, playerZ={s.P.Z:F2}, gateZ={gate.Z:F2}, done={gate.Done}, F={gate.F:F2}, N={gate.N}, Made={gate.Made}, Prog={gate.Prog:F3}");
            }
            Assert.That(shots[0].HitIds, Does.Contain(gate.Id), "첫 탄이 게이트에 맞음");
            if (key == "sgate") Assert.That(gate.N, Is.GreaterThan(0), "계단 수 증가");
            else Assert.That(gate.Made + gate.Extra, Is.GreaterThan(0), "지팡이 수 증가");
        }
    }
}
