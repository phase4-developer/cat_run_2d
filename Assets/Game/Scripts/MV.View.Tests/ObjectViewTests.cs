using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 티켓 04: 오브젝트 뷰(02 §2.10, 03 §3.5.3)와 월드 프리팹 연결. 골든 값은 정본 `drawJunk`·우물 생성·`drawWarpGate` 식을 Node(V8)로 돌린 결과다.
    /// 아틀라스를 로드하지 않으므로 스프라이트는 비어 있고, 뷰 구조·배치·정렬만 본다.
    /// </summary>
    public sealed class ObjectViewTests
    {
        const string PrefabPath = "Assets/Game/Addressables/Remote/World/WorldRoot.prefab";
        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        // (lane, row, bar, idx) → 스프라이트 번호, x 편차, 크기. 시험 이름은 정수만(실행기 기대 목록).
        static readonly int[,] s_junkIn = { { 0, 0, 0, 0, 0 }, { 2, 1, 0, 3, 1 }, { 1, 2, 2, 5, 1 } };
        static readonly double[,] s_junkOut = { { 0.111759106, 1.013346391 }, { 0.050974881, 0.965285031 }, { -0.150897754, 0.908919907 } };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Junk_VariantAndJitter_MatchProto(int c)
        {
            int lane = s_junkIn[c, 0], row = s_junkIn[c, 1], bar = s_junkIn[c, 2], idx = s_junkIn[c, 3], sprite = s_junkIn[c, 4];
            double jx = s_junkOut[c, 0], scale = s_junkOut[c, 1];
            Assert.That(ObjectLayout.JunkSpriteIndex(lane, row, bar, idx), Is.EqualTo(sprite));
            ObjectLayout.JunkJitter(idx, bar, out double gx, out double gs);
            Assert.That(gx, Is.EqualTo(jx).Within(1e-8));
            Assert.That(gs, Is.EqualTo(scale).Within(1e-8));
        }

        [Test]
        public void WellBricks_RingMatchesProto()
        {
            var list = new List<WellBrick>();
            ObjectLayout.WellBricks(list);
            Assert.That(list.Count, Is.EqualTo(53));
            Assert.That(list.Count(b => b.Junk), Is.EqualTo(9));
            void Check(int k, double x, double y, double z, int v)
            {
                Assert.That(list[k].X, Is.EqualTo(x).Within(1e-8), "x " + k);
                Assert.That(list[k].Y, Is.EqualTo(y).Within(1e-8), "y " + k);
                Assert.That(list[k].Z, Is.EqualTo(z).Within(1e-8), "z " + k);
                Assert.That(list[k].Variant, Is.EqualTo(v), "v " + k);
            }
            Check(0, 1, 0.17, 0, 0);
            Check(5, -0.959492974, 0.17, -0.281732557, 1);
            Check(12, 0.654860734, 0.49, -0.755749574, 2);
            Check(43, 0.959492974, 1.13, 0.281732557, 1);
        }

        static readonly double[] s_warpPz = { -5, -0.6, -0.1, 2.9, 3.1 }, s_warpAlpha = { 1, 1, 0.3, 0, 0 };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void WarpGate_AlphaNearPlayer_MatchesProto(int c)
        {
            double pz = s_warpPz[c], alpha = s_warpAlpha[c];
            bool shown = c != 4;
            Assert.That(WarpGateLayout.Alpha(pz, out double a), Is.EqualTo(shown));
            Assert.That(a, Is.EqualTo(alpha).Within(1e-9));
        }

        [Test]
        public void ShapeOf_CoversProtoKinds()
        {
            var t = ViewTestTables.Load();
            Assert.That(ObjectViewFactory.ShapeOf(t.ObjectKind.ByKey("wall")), Is.EqualTo(ObjectShape.Junk));
            Assert.That(ObjectViewFactory.ShapeOf(t.ObjectKind.ByKey("tower")), Is.EqualTo(ObjectShape.Well));
            Assert.That(ObjectViewFactory.ShapeOf(t.ObjectKind.ByKey("arch")), Is.EqualTo(ObjectShape.WarpGate));
            Assert.That(ObjectViewFactory.ShapeOf(t.ObjectKind.ByKey("stair")), Is.EqualTo(ObjectShape.Stair));
            Assert.That(ObjectViewFactory.ShapeOf(t.ObjectKind.ByKey("sgate")), Is.EqualTo(ObjectShape.RollGate));
            Assert.That(ObjectViewFactory.ShapeOf(t.ObjectKind.ByKey("coin")), Is.EqualTo(ObjectShape.CoinPile));
            Assert.That(ObjectViewFactory.ShapeOf(t.ObjectKind.ByKey("gem")), Is.EqualTo(ObjectShape.CatnipPot));
            Assert.That(ObjectViewFactory.ShapeOf(t.ObjectKind.ByKey("enemy")), Is.EqualTo(ObjectShape.Mouse));
        }

        [Test]
        public void ShapeOf_ReadsViewShapeColumn()
        {
            foreach (ObjectViewShape v in System.Enum.GetValues(typeof(ObjectViewShape)))
                Assert.That(System.Enum.TryParse(v.ToString(), out ObjectShape _), Is.True, "ObjectViewShape ↔ ObjectShape 1:1: " + v);
            foreach (var k in ViewTestTables.Load().ObjectKind.Sorted)
                Assert.That(ObjectViewFactory.ShapeOf(k).ToString(), Is.EqualTo(k.viewShape.ToString()), k.key);

            var edited = ViewTestTables.WithCell(ObjectKindData.Table, "cardbox", "viewShape", "Well");
            Assert.That(ObjectViewFactory.ShapeOf(edited.ObjectKind.ByKey("cardbox")), Is.EqualTo(ObjectShape.Well), "표만 바꿔도 모양이 바뀐다");
        }

        ObjectViewFactory MakeFactory(out Camera cam)
        {
            var camGo = new GameObject("TestCam");
            _made.Add(camGo);
            cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(0, 13, -9);
            camGo.transform.LookAt(new Vector3(0, 0.5f, 5.5f));
            var go = new GameObject("Objects");
            _made.Add(go);
            var f = go.AddComponent<ObjectViewFactory>();
            f.Init(ViewTestTables.Config(), camGo.transform, new WorldSpriteCache(), WorldView.FxAtlas);
            f.SetTheme(ViewTestTables.Theme("market"));
            f.LabelText = _ => null;   // TMP 글꼴 없이 구조만 본다
            return f;
        }

        [Test]
        public void Sync_ShowsLiveObjectsInRange_AndReleasesDeadOrPassed()
        {
            var t = ViewTestTables.Load();
            var cfg = ViewTestTables.Config();
            var f = MakeFactory(out _);
            var objs = new List<RunObject>();
            int id = 1;
            foreach (var key in new[] { "wall", "tower", "arch", "stair", "sgate", "coin", "gem" })
            {
                var o = RunObjectFactory.Create(t, t.ObjectKind.ByKey(key), 0, 10 + id * 5);
                o.Id = id++;
                objs.Add(o);
            }
            var far = RunObjectFactory.Create(t, t.ObjectKind.ByKey("wall"), 0, cfg.ObjDrawFront + 50);
            far.Id = id++;
            objs.Add(far);

            f.Sync(objs, 0, 0, 0);
            Assert.That(f.ViewCount, Is.EqualTo(7), "그리기 범위 밖은 뷰 없음");
            var wall = f.ViewOf(1);
            Assert.That(wall, Is.Not.Null);
            Assert.That(wall.transform.position.z, Is.EqualTo(15f).Within(1e-4));
            var ds = wall.GetComponent<DepthSortable>();
            Assert.That(ds.Rank, Is.EqualTo(DepthMath.RankObject));
            Assert.That(ds.SortOffsetY, Is.EqualTo((float)cfg.ObjProbeY));

            objs[0].Dead = true;
            f.Sync(objs, 0, 0, 0);
            Assert.That(f.ViewOf(1), Is.Null, "죽은 오브젝트는 뷰를 돌려준다");
            Assert.That(f.ViewCount, Is.EqualTo(6));

            f.Sync(objs, 200, 200, 0);
            Assert.That(f.ViewCount, Is.EqualTo(0), "지나간 오브젝트는 뷰를 돌려준다");
        }

        [Test]
        public void Well_BricksSortedFarFirstInsideGroup()
        {
            var t = ViewTestTables.Load();
            var f = MakeFactory(out var cam);
            var o = RunObjectFactory.Create(t, t.ObjectKind.ByKey("tower"), 0, 20);
            o.Id = 1;
            f.Sync(new List<RunObject> { o }, 0, 0, 0);
            var root = f.ViewOf(1).transform;
            var parts = root.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.sortingOrder >= 1 && r.name != "P0").ToList();
            Assert.That(parts.Count, Is.EqualTo(53));
            Assert.That(parts.Select(p => p.sortingOrder).OrderBy(x => x), Is.EqualTo(Enumerable.Range(1, 53)));
            var fwd = cam.transform.forward;
            float D(SpriteRenderer r) => Vector3.Dot(r.transform.position - cam.transform.position, fwd);
            var byOrder = parts.OrderBy(p => p.sortingOrder).ToList();
            for (int i = 1; i < byOrder.Count; i++)
                Assert.That(D(byOrder[i - 1]), Is.GreaterThanOrEqualTo(D(byOrder[i]) - 1e-4f), "먼 벽돌이 먼저");
        }

        [Test]
        public void HpLabel_ShowsCeilOfHp()
        {
            var t = ViewTestTables.Load();
            var kind = t.ObjectKind.Sorted.FirstOrDefault(k => k.labelKind == LabelKind.EnemyHp || k.labelKind == LabelKind.BoxHp);
            Assume.That(kind, Is.Not.Null, "체력 글자판 종류가 표에 없음");
            var o = RunObjectFactory.Create(t, kind, 0, 10);
            o.HasHp = true;
            o.Hp = 12.2;
            Assert.That(ObjectViewFactory.DefaultLabel(o), Is.EqualTo("13"));
        }

        [Test]
        public void WorldRootPrefab_WiresAllParts()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            var wv = prefab.GetComponent<WorldView>();
            Assert.That(wv, Is.Not.Null);
            var so = new SerializedObject(wv);
            foreach (var field in new[] { "_rig", "_sorter", "_ground", "_props", "_tint", "_screenMultiply", "_objects", "_screen" })
                Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(prefab.GetComponentInChildren<CameraRig>().GetComponent<Camera>(), Is.Not.Null);
            var props = new SerializedObject(prefab.GetComponentInChildren<PropStreamer>());
            foreach (var field in new[] { "_spriteMaterial", "_additiveMaterial", "_vertexColorMaterial" })
                Assert.That(props.FindProperty(field).objectReferenceValue, Is.Not.Null, "PropStreamer." + field);
            var objs = new SerializedObject(prefab.GetComponentInChildren<ObjectViewFactory>());
            foreach (var field in new[] { "_spriteMaterial", "_vertexColorMaterial" })
                Assert.That(objs.FindProperty(field).objectReferenceValue, Is.Not.Null, "ObjectViewFactory." + field);
            Assert.That(prefab.GetComponentInChildren<GroundStripMesh>().GetComponent<MeshRenderer>().sharedMaterial, Is.Not.Null);
        }
    }
}
