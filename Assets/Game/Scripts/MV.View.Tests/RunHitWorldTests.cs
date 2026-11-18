using System.Collections.Generic;
using System.IO;
using System.Linq;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 투사체 판정 = 프리팹 Collider + Unity Physics(<see cref="RunHitWorld"/>) 통합 시험. 가짜 판정 없이 실제 Collider·Raycast·Overlap을 쓴다.
    /// 실제 프리팹(벽 Stage1_Block_Gold, 적 Obj_Enemy)과 시험용 프리팹(이동·회전·배율·여러 Collider)을 함께 본다.
    /// </summary>
    public sealed class RunHitWorldTests
    {
        const string StageMap = "Stage1";
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

        /// <summary>
        /// 판정 세계 + 시험용 판정 형상 공급: 오브젝트마다 프리팹 인스턴스 하나를 시뮬 위치에 두고 그 안 <see cref="RunHitbox"/> Collider를 준다
        /// (게임에서는 <see cref="ObjectViewFactory.HitShape"/>가 화면 뷰 인스턴스로 같은 일을 한다).
        /// </summary>
        RunHitWorld MakeWorld(System.Func<RunObject, string> addressOf, System.Func<string, GameObject> assetOf)
        {
            var go = new GameObject("HitWorldTest");
            _made.Add(go);
            var w = go.AddComponent<RunHitWorld>();
            var inst = new Dictionary<int, GameObject>();
            w.Init((o, wy) =>
            {
                string a = addressOf(o);
                if (a == null) return null;
                if (!inst.TryGetValue(o.Id, out var g))
                {
                    var prefab = assetOf(a);
                    if (prefab == null) return null;
                    g = Object.Instantiate(prefab);
                    g.SetActive(true);
                    _made.Add(g);
                    inst[o.Id] = g;
                }
                g.transform.position = new Vector3((float)o.X, (float)wy, (float)o.Z);
                var cols = new List<Collider>();
                foreach (var hb in g.GetComponentsInChildren<RunHitbox>(true)) cols.AddRange(hb.GetComponentsInChildren<Collider>(true));
                return cols;
            }, addressOf);
            return w;
        }

        /// <summary>실제 게임 경로: 305 → 105.prefab 주소, 프리팹 원본은 에셋.</summary>
        RunHitWorld RealWorld() => MakeWorld(o => ObjectViewFactory.PrefabFor(_t, StageMap, o), LoadPrefab);

        RunState NewState() => new RunState(new RunSetup(), _t, new Pcg32Rng(1));

        RunObject Wall(RunState s, int id, double x, double z)
        {
            var o = RunObjectFactory.Create(_t, _t.ObjectKind.ByKey("wall"), x, z);
            o.Id = id;
            o.Idx = 0;                  // 필드 벽(305 보상 행)
            o.Rew = FieldReward.Gold;   // → Stage1_Block_Gold
            s.Objs.Add(o);
            return o;
        }

        RunObject Kind(RunState s, string key, int id, double x, double z)
        {
            var o = RunObjectFactory.Create(_t, _t.ObjectKind.ByKey(key), x, z);
            o.Id = id;
            s.Objs.Add(o);
            return o;
        }

        static List<ProjectileHit> Cast(RunHitWorld w, RunState s, double x, double z0, double z1)
        {
            var hits = new List<ProjectileHit>();
            w.Cast(s, x, z0, z1, hits);
            return hits;
        }

        static BoxCollider WallBox() => LoadPrefab("Stage1_Block_Gold").GetComponentInChildren<RunHitbox>(true).GetComponent<BoxCollider>();

        [Test]
        public void RealWallPrefab_HitRangeComesFromCollider_NotTableHw()
        {
            var s = NewState();
            var wall = Wall(s, 1, 0, 10);
            var w = RealWorld();
            w.Prepare(s);
            var box = WallBox();
            float half = box.size.x / 2f;
            Assert.That(half, Is.GreaterThan(wall.Hw), "시험 전제: 프리팹 판정 폭이 표 hw보다 넓다(사용자 확인 사례)");
            double between = (wall.Hw + half) / 2;   // 표 hw 밖, Collider 안
            Assert.That(Cast(w, s, between, 0, 30).Select(h => h.Obj.Id), Is.EqualTo(new[] { 1 }), "Collider 안이면 맞는다");
            Assert.That(Cast(w, s, half + 0.05, 0, 30), Is.Empty, "Collider 밖이면 안 맞는다");
            var hit = Cast(w, s, 0, 0, 30)[0];
            Assert.That(hit.Z, Is.EqualTo(10 + box.center.z - box.size.z / 2).Within(1e-3), "진입 z = Collider 앞면");
        }

        [Test]
        public void FastSegment_DoesNotTunnel_AndOrdersFrontFirst()
        {
            var s = NewState();
            Wall(s, 1, 0, 40);   // 뒤(먼저 넣음, 작은 Id)
            Wall(s, 2, 0, 12);   // 앞
            var w = RealWorld();
            w.Prepare(s);
            var hits = Cast(w, s, 0, 0, 100);   // 한 틱에 두 벽을 모두 지나는 고속 구간
            Assert.That(hits.Select(h => h.Obj.Id), Is.EqualTo(new[] { 2, 1 }), "반환 배열 순서가 아니라 진입 z 순");
        }

        [Test]
        public void StartInsideCollider_HitsAtSegmentStart()
        {
            var s = NewState();
            Wall(s, 1, 0, 10);
            var w = RealWorld();
            w.Prepare(s);
            var hits = Cast(w, s, 0, 10, 10.2);
            Assert.That(hits.Count, Is.EqualTo(1), "출발점이 이미 형상 안이어도 맞는다");
            Assert.That(hits[0].Z, Is.EqualTo(10).Within(1e-6));
        }

        [Test]
        public void MovingEnemy_PrepareFollowsSimPosition()
        {
            var s = NewState();
            var e = Kind(s, "enemy", 1, 0, 10);
            var w = RealWorld();
            w.Prepare(s);
            Assert.That(Cast(w, s, 0, 0, 30).Count, Is.EqualTo(1), "Obj_Enemy Hitbox");
            e.X = 5;   // 이동 AI가 옮긴 뒤
            w.Prepare(s);
            Assert.That(Cast(w, s, 0, 0, 30), Is.Empty, "옛 자리에는 없다");
            Assert.That(Cast(w, s, 5, 0, 30).Count, Is.EqualTo(1), "새 자리에서 맞는다");
        }

        [Test]
        public void DeadOrNeverHittable_HasNoHitbox()
        {
            var s = NewState();
            var a = Wall(s, 1, 0, 10);
            Kind(s, "stair", 2, 0, 20);   // hittable Never
            var w = RealWorld();
            w.Prepare(s);
            Assert.That(w.LiveCount, Is.EqualTo(1));
            a.Dead = true;
            w.Prepare(s);
            Assert.That(w.LiveCount, Is.EqualTo(0));
            Assert.That(Cast(w, s, 0, 0, 30), Is.Empty);
        }

        /// <summary>시험용 프리팹(메모리): 뿌리 배율·Hitbox 회전·여러 Collider. 판정이 표·코드 값이 아니라 Collider에서 오는지 본다.</summary>
        GameObject TestPrefab(Vector3 rootScale, Quaternion hitboxRot, params (Vector3 center, Vector3 size)[] boxes)
        {
            var root = new GameObject("TestPrefab");
            _made.Add(root);
            root.transform.localScale = rootScale;
            var hb = new GameObject("Hitbox");
            hb.transform.SetParent(root.transform, false);
            hb.transform.localRotation = hitboxRot;
            hb.AddComponent<RunHitbox>();
            foreach (var b in boxes)
            {
                var c = hb.AddComponent<BoxCollider>();
                c.center = b.center;
                c.size = b.size;
                c.isTrigger = true;
            }
            root.SetActive(false);   // 원본은 장면에서 맞지 않게
            return root;
        }

        RunHitWorld WorldOf(GameObject prefab) => MakeWorld(o => "test", a => prefab);

        [Test]
        public void EditedCollider_ChangesRange_WithoutTableChange()
        {
            var s = NewState();
            var o = Kind(s, "tower", 1, 0, 10);
            double x = 1.9;
            var wide = WorldOf(TestPrefab(Vector3.one, Quaternion.identity, (new Vector3(0, 1, 0), new Vector3(4, 2, 1))));
            wide.Prepare(s);
            Assert.That(Cast(wide, s, x, 0, 30).Count, Is.EqualTo(1), "폭 4 → x 1.9 맞음");
            var narrow = WorldOf(TestPrefab(Vector3.one, Quaternion.identity, (new Vector3(0, 1, 0), new Vector3(2, 2, 1))));
            narrow.Prepare(s);
            Assert.That(Cast(narrow, s, x, 0, 30), Is.Empty, "폭 2 → 같은 표 값(hw " + o.Hw + ")이어도 안 맞음");
        }

        [Test]
        public void PrefabScaleAndRotation_AreApplied()
        {
            var s = NewState();
            Kind(s, "tower", 1, 0, 10);
            var scaled = WorldOf(TestPrefab(new Vector3(2, 2, 2), Quaternion.identity, (new Vector3(0, 0.5f, 0), new Vector3(2, 2, 1))));
            scaled.Prepare(s);
            Assert.That(Cast(scaled, s, 1.9, 0, 30).Count, Is.EqualTo(1), "뿌리 배율 2 → 반폭 2");
            var rotated = WorldOf(TestPrefab(Vector3.one, Quaternion.Euler(0, 90, 0), (new Vector3(0, 1, 0), new Vector3(4, 2, 0.5f))));
            rotated.Prepare(s);
            Assert.That(Cast(rotated, s, 1, 0, 30), Is.Empty, "y 90° 회전 → 가로 0.5");
            var r = Cast(rotated, s, 0, 0, 30);
            Assert.That(r.Count, Is.EqualTo(1));
            Assert.That(r[0].Z, Is.EqualTo(8).Within(1e-3), "회전 뒤 앞면 = z 10 − 2");
        }

        [Test]
        public void SeveralColliders_OneTarget_ReportedOnceAtNearest()
        {
            var s = NewState();
            Kind(s, "tower", 1, 0, 10);
            var w = WorldOf(TestPrefab(Vector3.one, Quaternion.identity,
                (new Vector3(0, 1, 1), new Vector3(2, 2, 1)), (new Vector3(0, 1, -1), new Vector3(2, 2, 1))));
            w.Prepare(s);
            var hits = Cast(w, s, 0, 0, 30);
            Assert.That(hits.Count, Is.EqualTo(1), "대상 단위");
            Assert.That(hits[0].Z, Is.EqualTo(10 - 1.5).Within(1e-3), "가장 앞 Collider 진입 z");
        }

        [Test]
        public void ProjectileHeight_MustBeCoveredByCollider()
        {
            var s = NewState();
            Kind(s, "tower", 1, 0, 10);
            // 투사체 높이(ProjLayout.ProjY) 아래만 덮는 형상 → 안 맞음(그림과 같은 높이로 날아간다)
            float top = (float)ProjLayout.ProjY - 0.2f;
            var low = WorldOf(TestPrefab(Vector3.one, Quaternion.identity, (new Vector3(0, top / 2, 0), new Vector3(2, top, 1))));
            low.Prepare(s);
            Assert.That(Cast(low, s, 0, 0, 30), Is.Empty);
        }

        /// <summary>
        /// 밀집: 한 구간에 대상 Collider 60개 + 판정 대상이 아닌 Collider(화면 프리팹 등) 40개 → Raycast 기본 버퍼(32)·겹침 버퍼(16)를 넘어도
        /// 대상이 빠지지 않고 (진입 z, Id) 순서가 유지된다. 시작점에 대상 아닌 Collider 20개가 겹쳐 있어도 시작 겹침 대상을 잡는다.
        /// </summary>
        [Test]
        public void DenseTargets_OverflowBuffers_NoMissAndOrderKept()
        {
            var s = NewState();
            const int n = 60;
            for (int i = 0; i < n; i++) Wall(s, 1000 - i, 0, 10 + i * 4);   // 뒤로 갈수록 Id가 작다(Id 순서와 z 순서가 반대)
            var w = RealWorld();
            w.Prepare(s);
            for (int i = 0; i < 40; i++)
            {
                var d = new GameObject("Distractor" + i);
                _made.Add(d);
                d.AddComponent<BoxCollider>().size = new Vector3(2, 4, 0.5f);
                d.transform.position = new Vector3(0, (float)ProjLayout.ProjY, 5 + i * 6);
            }
            for (int i = 0; i < 20; i++)
            {
                var d = new GameObject("StartDistractor" + i);
                _made.Add(d);
                d.AddComponent<SphereCollider>().radius = 1f;
                d.transform.position = new Vector3(0, (float)ProjLayout.ProjY, 10);
            }
            Physics.SyncTransforms();
            var hits = Cast(w, s, 0, 0, 10 + n * 4);
            Assert.That(hits.Select(h => h.Obj.Id), Is.EqualTo(Enumerable.Range(0, n).Select(i => 1000 - i)), "대상 60개 모두, 진입 z 순");
            for (int i = 1; i < hits.Count; i++) Assert.That(hits[i].Z, Is.GreaterThan(hits[i - 1].Z));
            var inside = Cast(w, s, 0, 10, 10.2);
            Assert.That(inside.Select(h => h.Obj.Id), Is.EqualTo(new[] { 1000 }), "시작점 겹침: 대상 아닌 Collider 20개와 함께 있어도 잡는다");
            Assert.That(inside[0].Z, Is.EqualTo(10).Within(1e-6));
        }

        /// <summary>시뮬 + 실제 Physics: 관통 1 투사체가 앞 벽 → 뒤 벽 순서로 한 번씩 맞고 사라진다(같은 대상 중복 없음).</summary>
        [Test]
        public void RunSim_WithRealPhysics_PierceOrderAndNoDuplicate()
        {
            var sim = RunSim.Create(new RunSetup { Deck = new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 } }, _t, new Pcg32Rng(1), new RunLevelBuilder());
            var s = sim.State;
            var w = RealWorld();
            sim.HitQuery = w;
            s.Objs.Clear();
            s.Projs.Clear();
            double pz = s.P.Z, px = s.P.X;
            var back = Wall(s, 1001, px, pz + 14);
            var front = Wall(s, 1002, px, pz + 8);
            foreach (var o in new[] { back, front }) { o.Hp = o.Max = 1e12; }
            var b = new RunProjectile { Id = 999999, X = px, Z = pz, Sz = pz, D = 1, Pierce = 1 };
            s.Projs.Add(b);
            for (int i = 0; i < 120 && !b.Dead; i++) sim.Tick(1.0 / 60, RunInput.None);
            Assert.That(b.HitIds, Is.EqualTo(new[] { front.Id, back.Id }), "앞 → 뒤, 한 번씩");
            Assert.That(b.Dead, Is.True, "관통 1 = 두 대상 뒤 소멸");
        }
    }
}
