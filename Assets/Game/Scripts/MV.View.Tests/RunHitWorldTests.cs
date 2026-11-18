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
        const string StageMap = HitWorldRig.StageMap;
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

        static GameObject LoadPrefab(string address) => HitWorldRig.LoadPrefab(address);

        /// <summary>게임과 같은 판정 경로(관리자 → 뷰 연결 → 프리팹 Hitbox 등록 → 질의). <paramref name="assetOf"/>가 null이면 실제 프리팹 에셋.</summary>
        RunHitWorld MakeWorld(System.Func<string, GameObject> assetOf = null, double pz = 0)
        {
            var rig = new HitWorldRig(_t, assetOf);
            _rigs.Add(rig);
            rig.Frame(pz);
            return rig.World;
        }

        /// <summary>실제 게임 경로: 305 → 105.prefab 주소, 프리팹 원본은 에셋.</summary>
        RunHitWorld RealWorld(double pz = 0) => MakeWorld(null, pz);

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
            root.transform.position = new Vector3(0, -1000, 0);   // 원본은 장면에서 맞지 않게(인스턴스는 뷰 뿌리에 0으로 놓인다)
            new GameObject("Billboard", typeof(Billboard)).transform.SetParent(root.transform, false);   // 프리팹 규칙: 그림 묶음
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
            return root;
        }

        RunHitWorld WorldOf(GameObject prefab) => MakeWorld(a => prefab);

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
            const double gap = 1.45;   // 벽 Collider 깊이 1.1보다 넓게, 60개가 그리기 범위(pz 8 → 6~98 m) 안에
            for (int i = 0; i < n; i++) Wall(s, 1000 - i, 0, 10 + i * gap);   // 뒤로 갈수록 Id가 작다(Id 순서와 z 순서가 반대)
            var w = RealWorld(8);
            w.Prepare(s);
            for (int i = 0; i < 40; i++)
            {
                var d = new GameObject("Distractor" + i);
                _made.Add(d);
                d.AddComponent<BoxCollider>().size = new Vector3(2, 4, 0.5f);
                d.transform.position = new Vector3(0, (float)ProjLayout.ProjY, (float)(9.5 + i * 2.2));
            }
            for (int i = 0; i < 20; i++)
            {
                var d = new GameObject("StartDistractor" + i);
                _made.Add(d);
                d.AddComponent<SphereCollider>().radius = 1f;
                d.transform.position = new Vector3(0, (float)ProjLayout.ProjY, 10);
            }
            Physics.SyncTransforms();
            var hits = Cast(w, s, 0, 0, 10 + n * gap);
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
            var w = RealWorld(s.P.Z);
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

        /// <summary>
        /// 프레임당 여러 틱(게임 40틱, MvGameLoop.TicksPerFrame): 화면 갱신(Sync) 없이 도는 틱 사이에 생긴 대상도 그 틱의 판정 직전에 뷰·Hitbox가 준비돼 맞는다.
        /// </summary>
        [Test]
        public void TicksWithinOneFrame_NewTargetHittableBeforeNextFrame()
        {
            var sim = RunSim.Create(new RunSetup { Deck = new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 } }, _t, new Pcg32Rng(1), new RunLevelBuilder());
            var s = sim.State;
            var w = RealWorld(s.P.Z);   // 프레임 한 번(이후 Sync 없음)
            sim.HitQuery = w;
            s.Objs.Clear();
            s.Projs.Clear();
            double px = s.P.X;
            var b = new RunProjectile { Id = 999990, X = px, Z = s.P.Z, Sz = s.P.Z, D = 1, Pierce = 0 };
            s.Projs.Add(b);
            const int ticksPerFrame = 40;
            RunObject wall = null;
            for (int i = 0; i < ticksPerFrame && !b.Dead; i++)
            {
                if (i == 5)
                {
                    wall = Wall(s, 1003, px, b.Z + 2);   // 프레임 중간 틱에 생김
                    wall.Hp = wall.Max = 1e12;
                }
                sim.Tick(1.0 / 60, RunInput.None);
            }
            Assert.That(wall, Is.Not.Null);
            Assert.That(b.HitIds, Does.Contain(wall.Id), "같은 프레임 안 다음 틱에서 맞는다");
        }

        /// <summary>
        /// 회수 흐름: 쓰러지는 동안(ObjectMotion이 회수를 막음)은 뷰가 남지만 판정 대상이 아니고, 회수되면 등록이 풀리며,
        /// 풀에서 다시 쓴 뷰는 새 오브젝트로 다시 등록돼 맞는다.
        /// </summary>
        [Test]
        public void BreakHoldsRelease_ThenUnregisters_AndPoolReuseRegistersNewObject()
        {
            var rig = new HitWorldRig(_t);
            _rigs.Add(rig);
            var s = NewState();
            var a = Wall(s, 1, 0, 10);
            rig.Frame(0, s.Objs);
            Assert.That(rig.World.RegisteredCount, Is.EqualTo(1), "연결 때 Hitbox 등록");
            var first = rig.Factory.ViewOf(1);
            Assert.That(first.GetComponent<RunObjectView>().Module<ObjectMotion>(), Is.Not.Null, "블록 프리팹에 ObjectMotion 모듈");

            rig.Factory.OnRunEvents(new List<RunEvent> { new RunEvent(RunEventType.Break, objId: 1, kindId: a.Kind.id) });
            a.Dead = true;
            rig.Frame(0, s.Objs);
            Assert.That(rig.Factory.ViewOf(1), Is.Not.Null, "쓰러지는 동안 회수 막음");
            rig.World.Prepare(s);
            Assert.That(rig.World.LiveCount, Is.EqualTo(0), "죽은 오브젝트는 판정 대상 아님");
            Assert.That(Cast(rig.World, s, 0, 0, 30), Is.Empty);
            for (int i = 0; i < 12 && rig.Factory.ViewOf(1) != null; i++)
            {
                System.Threading.Thread.Sleep(110);   // Sync dt는 실시간(한 번에 최대 0.1초)
                rig.Frame(0, s.Objs);
            }
            Assert.That(rig.Factory.ViewOf(1), Is.Null, "다 쓰러지면 회수");
            Assert.That(rig.World.RegisteredCount, Is.EqualTo(0), "회수 때 등록 해제");

            var b = Wall(s, 2, 0, 12);
            rig.Frame(0, s.Objs);
            Assert.That(rig.Factory.ViewOf(2), Is.SameAs(first), "풀에서 같은 뷰를 다시 씀");
            Assert.That(rig.World.RegisteredCount, Is.EqualTo(1), "새 오브젝트로 다시 등록");
            rig.World.Prepare(s);
            var hits = Cast(rig.World, s, 0, 0, 30);
            Assert.That(hits.Select(h => h.Obj.Id), Is.EqualTo(new[] { b.Id }), "다시 쓴 뷰의 Hitbox는 새 오브젝트로 맞는다");
        }

        /// <summary>
        /// 이벤트 대상 정본은 <see cref="RunEvent.TargetsObject"/> 한 곳이다. 오브젝트 번호와 투사체 번호는 따로 1부터 세므로 늘 겹친다(RunState.NextObjId·NextProjId).
        /// 관리자가 모르는 새 모듈도 프리팹에 붙이기만 하면 오브젝트 대상 이벤트(관리자에 나열 없는 GateFill·Steal·BurnTick 포함)를 받고,
        /// 같은 번호의 투사체 이벤트·번호 없는 이벤트·뷰 없는 번호는 받지 않는다.
        /// </summary>
        [Test]
        public void ObjectEvents_ReachUnknownPrefabModule_ProjectileIdsDoNot()
        {
            var prefab = TestPrefab(Vector3.one, Quaternion.identity, (new Vector3(0, 1, 0), new Vector3(2, 2, 1)));
            prefab.AddComponent<RecordingObjectModule>();
            var rig = new HitWorldRig(_t, a => prefab);
            _rigs.Add(rig);
            var s = NewState();
            var a1 = Kind(s, "tower", 1, 0, 10);
            Kind(s, "tower", 2, 0, 14);
            rig.Frame(0, s.Objs);
            var rec = rig.Factory.ViewOf(1).GetComponent<RunObjectView>().Module<RecordingObjectModule>();
            var other = rig.Factory.ViewOf(2).GetComponent<RunObjectView>().Module<RecordingObjectModule>();
            Assert.That(rec, Is.Not.Null, "프리팹 인스턴스의 새 모듈을 뷰가 읽음");

            rig.Factory.OnRunEvents(new List<RunEvent>
            {
                new RunEvent(RunEventType.Fire, objId: 1),          // 투사체 1번
                new RunEvent(RunEventType.ProjImpact, objId: 1),
                new RunEvent(RunEventType.ProjRemoved, objId: 1),
                new RunEvent(RunEventType.Hitstop, amount: 0.1),     // 번호 없음
                new RunEvent(RunEventType.Hit, objId: 0),
                new RunEvent(RunEventType.Hit, objId: 99),           // 뷰 없음
                new RunEvent(RunEventType.Hit, objId: 1, kindId: a1.Kind.id),
                new RunEvent(RunEventType.GateFill, objId: 1),
                new RunEvent(RunEventType.Steal, objId: 1),
                new RunEvent(RunEventType.BurnTick, objId: 1),
                new RunEvent(RunEventType.Break, objId: 1, kindId: a1.Kind.id),
            });
            Assert.That(rec.Got, Is.EqualTo(new[] { RunEventType.Hit, RunEventType.GateFill, RunEventType.Steal, RunEventType.BurnTick, RunEventType.Break }),
                "오브젝트 대상만, 발생 순서 그대로");
            Assert.That(rec.GotFor, Is.All.SameAs(a1), "그 오브젝트에 연결된 뷰로만");
            Assert.That(other.Got, Is.Empty, "다른 오브젝트 뷰는 받지 않음");
        }

        /// <summary>연출(흔들림·눌림·자리 흔들기)은 그림 묶음만 움직이고 Hitbox는 시뮬 위치 그대로다.</summary>
        [Test]
        public void HitMotion_MovesBoardsOnly_HitboxStays()
        {
            var rig = new HitWorldRig(_t);
            _rigs.Add(rig);
            var s = NewState();
            var a = Wall(s, 1, 0, 10);
            rig.Frame(0, s.Objs);
            var root = rig.Factory.ViewOf(1).transform;
            var hb = root.GetComponentInChildren<RunHitbox>(true).transform;
            var board = root.GetComponentInChildren<Billboard>(true).transform;
            Vector3 hbPos = hb.position, hbScale = hb.lossyScale, boardBase = board.localPosition;
            a.Bar = 3; a.Idx = 2;   // 자리 흔들기가 0이 아니게
            rig.Factory.OnRunEvents(new List<RunEvent> { new RunEvent(RunEventType.Hit, objId: 1, kindId: a.Kind.id) });
            System.Threading.Thread.Sleep(20);
            rig.Frame(0, s.Objs);
            Assert.That(board.localPosition, Is.Not.EqualTo(boardBase), "그림 묶음은 움직인다");
            Assert.That(hb.position, Is.EqualTo(hbPos), "Hitbox 위치 그대로");
            Assert.That(hb.lossyScale, Is.EqualTo(hbScale), "Hitbox 크기 그대로(눌림 없음)");
        }
    }
}
