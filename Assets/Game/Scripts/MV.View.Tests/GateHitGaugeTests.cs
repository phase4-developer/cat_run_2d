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
        readonly List<HitWorldRig> _rigs = new List<HitWorldRig>();
        MvTables _t;

        [SetUp]
        public void SetUp() => _t = ViewTestTables.Load();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in _rigs) r.Dispose();
            _rigs.Clear();
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

        /// <summary>
        /// 노션 Obj_StairGate UI: 문 안에 계단 아이콘(Icon_Floor), 그 아래 숫자 「x{value}」와 막대(아래로 아이콘 → 숫자 → 막대).
        /// 무기문(cgate)은 계단문 변형이라 아이콘을 물려받으므로 끄고, 문 안 무기 카드 아래에 막대만 둔다(숫자 없음).
        /// </summary>
        [Test]
        public void StairGate_InnerFloorIcon_AboveNumberAndGauge()
        {
            var sg = Object.Instantiate(LoadPrefab("Obj_StairGate"));
            _made.Add(sg);
            var icon = sg.transform.Find("Billboard/StairIcon");
            Assert.That(icon, Is.Not.Null, "Obj_StairGate Billboard/StairIcon");
            Assert.That(icon.gameObject.activeSelf, Is.True, "계단문 아이콘 켜짐");
            var sr = icon.GetComponent<SpriteRenderer>();
            Assert.That(sr != null && sr.sprite != null && sr.sprite.name == "Icon_Floor", Is.True, "Icon_Floor 그림");
            var label = CountLabel.Find(sg.transform);
            Assert.That(label.Number.GetComponent<Renderer>().sortingOrder, Is.GreaterThan(sr.sortingOrder), "숫자가 아이콘 위에 그려진다");
            float iconY = icon.position.y, numY = label.Number.transform.position.y, gaugeY = label.GaugeRoot.transform.position.y;
            Assert.That(iconY > numY && numY > gaugeY, Is.True, $"아래로 아이콘({iconY}) → 숫자({numY}) → 막대({gaugeY})");

            var cg = Object.Instantiate(LoadPrefab("Obj_CraftGate"));
            _made.Add(cg);
            var cgIcon = cg.transform.Find("Billboard/StairIcon");
            Assert.That(cgIcon, Is.Not.Null, "변형이 물려받은 StairIcon");
            Assert.That(cgIcon.gameObject.activeSelf, Is.False, "무기문에는 계단 아이콘을 끈다");
            var cgLabel = CountLabel.Find(cg.transform);
            var bundle = cg.GetComponentInChildren<CraftGateBundle>(true);
            Assert.That(bundle != null && bundle.LabelIcon != null, Is.True, "무기문 문 안 카드");
            float cardBottom = bundle.LabelIcon.transform.position.y - bundle.LabelIconSize.y * 0.5f;
            Assert.That(cgLabel.GaugeRoot.transform.position.y, Is.LessThan(cardBottom), "무기문: 문 안 카드 아래 막대(노션 시안)");
        }

        /// <summary>
        /// 노션 Obj_StairZone UI: 계단 구역 위에 별자리 카드(Obj_Card_StairZone, 팀원 프리팹)를 띄운다. 등급 틀 Grade_1·그림 1장 고정.
        /// 카드는 글자판과 같은 빌보드 묶음 안, 글자판(「계단」·숫자·막대) 아래에 있다. 등급 글자·필요 계단 수·이름 칸은 없다.
        /// </summary>
        [Test]
        public void StairZone_ShowsFixedGrade1Card_UnderLabel()
        {
            var zone = Object.Instantiate(LoadPrefab("Obj_StairZone"));
            _made.Add(zone);
            var card = zone.transform.Find("Billboard/Obj_Card_StairZone");
            Assert.That(card, Is.Not.Null, "Obj_StairZone Billboard/Obj_Card_StairZone");
            Assert.That(card.gameObject.activeSelf, Is.True, "카드 켜짐");
            var frame = card.Find("Frame")?.GetComponent<SpriteRenderer>();
            var tex = card.Find("Mask/Texture")?.GetComponent<SpriteRenderer>();
            Assert.That(frame != null && frame.sprite != null && frame.sprite.name == "Grade_1", Is.True, "틀 = Grade_1 고정");
            Assert.That(tex != null && tex.sprite != null && tex.sprite.name == "Texture_Card_Grade_1", Is.True, "그림 1장 고정");
            Assert.That(card.GetComponentsInChildren<TMPro.TMP_Text>(true), Is.Empty, "카드에 글자 칸(등급·필요 계단 수·이름) 없음");
            var label = CountLabel.Find(zone.transform);
            Assert.That(label, Is.Not.Null, "글자판");
            Assert.That(frame.bounds.max.y, Is.LessThan(label.GaugeRoot.transform.position.y), "카드 위 끝이 글자판 막대 아래(시안: 글자판 → 카드 순)");
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
            Assert.That(label.GetComponentInParent<Billboard>(true), Is.Not.Null, $"{address} 글자판은 그림과 같은 빌보드 묶음 안(카메라 평면)");
            foreach (var hb in inst.GetComponentsInChildren<RunHitbox>(true))
                Assert.That(hb.GetComponentInParent<Billboard>(true), Is.Null, $"{address} Hitbox는 빌보드 밖(연출이 판정을 움직이지 않게)");

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
            var rig = new HitWorldRig(_t);   // 게임과 같은 판정 경로(관리자 → 뷰 연결 → 게이트 프리팹 Hitbox 등록)
            _rigs.Add(rig);
            rig.Frame(s.P.Z);
            var w = rig.World;
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
