using System.Collections.Generic;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// MV-UI3-STAIRFX 노션 「World Object Attraction」: 탑 오브(Obj_Tower_Orb)가 우물 비움(WellFlush)마다 구슬 수만큼 계단 경험치 오브 출발을 요청한다.
    /// MV-UI3-STAIRFX2·3: 오브 재생은 MV.Fx StairOrbFx(Framework 비행)가 UI Effect 루트에서 맡고, 탑은 수령 번호를 실어 출발 요청만 한다. 화면 모양은 사람 확인(NOT_RUN 별도).
    /// </summary>
    public sealed class StairOrbTests
    {
        const string TowerPath = "Assets/Game/Addressables/Local/BG/Prefab/Object/Obj_Tower_Orb.prefab";

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            StairOrbFlight.ClearForTests();
        }

        (StairOrbEmitter em, RunObjectView view) NewTower()
        {
            var tower = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TowerPath));
            _made.Add(tower);
            var viewGo = new GameObject("View");
            _made.Add(viewGo);
            var view = viewGo.AddComponent<RunObjectView>();
            tower.transform.SetParent(viewGo.transform, false);
            return (tower.GetComponentInChildren<StairOrbEmitter>(true), view);
        }

        [Test]
        public void TowerOrbPrefab_WiresEmitterToOrbKeyAndSpawnArea()
        {
            var tower = AssetDatabase.LoadAssetAtPath<GameObject>(TowerPath);
            Assert.That(tower, Is.Not.Null, TowerPath);
            var em = tower.GetComponentInChildren<StairOrbEmitter>(true);
            Assert.That(em, Is.Not.Null, "Obj_Tower_Orb에 StairOrbEmitter");
            Assert.AreEqual("Obj_Stair_Orb", em.OrbKey, "EffectManager 키 = 디자이너 Obj_Stair_Orb Addressables 주소");
            var so = new SerializedObject(em);
            var area = so.FindProperty("spawnArea").objectReferenceValue as BoxCollider;
            Assert.That(area, Is.Not.Null, "spawnArea");
            Assert.AreEqual("Orb Spawn Area", area.name);
            Assert.That(so.FindProperty("origin").objectReferenceValue, Is.Not.Null, "origin");
            Assert.That(so.FindProperty("sortingOffset").intValue, Is.GreaterThanOrEqualTo(1), "UI 캔버스 위에 그려지게");
        }

        [Test]
        public void WellFlush_RequestsOneOrbPerBrick_WithGap_CarryingReceiveId()
        {
            var (em, view) = NewTower();
            var reqs = new List<StairOrbFlight.OrbLaunch>();
            StairOrbFlight.Launcher = (in StairOrbFlight.OrbLaunch r) => { reqs.Add(r); return true; };
            var arrived = new List<int>();
            StairOrbFlight.Arrived += id => arrived.Add(id);

            em.OnBind(view);
            em.OnRunEvent(view, new RunEvent(RunEventType.WellFlush, objId: 1, amount: 5, a: 3, b: 7));
            Assert.That(StairOrbFlight.TakeClaim(7), Is.True, "수령 7을 오브로 맡았다(런 프레임이 바로 도착으로 치지 않게)");
            Assert.AreEqual(1, reqs.Count, "첫 오브는 바로, 나머지는 간격 뒤");
            em.StepQueue(10f);
            Assert.AreEqual(0, em.QueuedCount);
            Assert.AreEqual(3, reqs.Count, "구슬 수(e.A)만큼");
            foreach (var r in reqs)
            {
                Assert.AreEqual("Obj_Stair_Orb", r.Key);
                Assert.That(r.SortingOffset, Is.GreaterThanOrEqualTo(1));
                Assert.AreEqual(7, r.ReceiveId, "오브마다 수령 번호");
                Assert.AreEqual(StairOrbFlight.Run, r.Run);
            }
            Assert.AreEqual(0, arrived.Count, "도착은 재생기가 알린다");
        }

        [Test]
        public void NoUiRoot_OrbCountsAsArrivedImmediately()
        {
            var (em, view) = NewTower();
            StairOrbFlight.Launcher = (in StairOrbFlight.OrbLaunch r) => false; // UI 판 없음
            em.OnBind(view);
            em.OnRunEvent(view, new RunEvent(RunEventType.WellFlush, objId: 1, amount: 5, a: 2, b: 3));
            em.StepQueue(10f);
            var got = new List<int>();
            StairOrbFlight.DrainArrivals(got.Add);
            Assert.AreEqual(new[] { 3, 3 }, got.ToArray(), "재생 못 한 오브는 바로 도착(게이지가 기다리지 않게)");
        }

        [Test]
        public void NoLauncher_LeavesReceiveUnclaimed()
        {
            var (em, view) = NewTower();
            em.OnBind(view);
            em.OnRunEvent(view, new RunEvent(RunEventType.WellFlush, objId: 1, amount: 5, a: 2, b: 4));
            Assert.That(StairOrbFlight.TakeClaim(4), Is.False, "재생기가 없으면 맡지 않는다(런 프레임이 바로 도착으로 친다)");
        }

        [Test]
        public void TowerRemoved_QueuedOrbsStillCountAsArrived()
        {
            var (em, view) = NewTower();
            StairOrbFlight.Launcher = (in StairOrbFlight.OrbLaunch r) => true;
            em.OnBind(view);
            em.OnRunEvent(view, new RunEvent(RunEventType.WellFlush, objId: 1, amount: 5, a: 3, b: 5));
            Assert.AreEqual(2, em.QueuedCount);
            em.ReleaseAll(); // 실행 중에는 OnDestroy가 부른다(EditMode는 깨우지 않은 스크립트의 OnDestroy를 부르지 않는다)
            var got = new List<int>();
            StairOrbFlight.DrainArrivals(got.Add);
            Assert.AreEqual(2, got.Count, "대기 오브는 도착으로 치고, 이미 나간 오브는 재생기 몫");
        }

        [Test]
        public void ArrivalFromPreviousRun_IsDropped()
        {
            int run = StairOrbFlight.Run;
            StairOrbFlight.BeginRun();
            StairOrbFlight.RaiseArrived(run, 1);
            StairOrbFlight.RaiseArrived(StairOrbFlight.Run, 2);
            var got = new List<int>();
            StairOrbFlight.DrainArrivals(got.Add);
            Assert.AreEqual(new[] { 2 }, got.ToArray(), "지난 런 오브의 도착은 새 런 수령에 들어가지 않는다");
        }
    }
}
