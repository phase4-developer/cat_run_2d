using System.Collections.Generic;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// MV-UI3-STAIRFX 노션 「World Object Attraction」: 탑 오브(Obj_Tower_Orb)가 우물 비움(WellFlush)마다 계단 경험치 오브(Obj_Stair_Orb)를
    /// 구슬 수만큼 빌려 내보내고, 오브는 끝나면 도착을 알리고 풀로 돌아간다. 화면 모양(이동 곡선·도착 자리)은 사람 확인(NOT_RUN 별도).
    /// </summary>
    public sealed class StairOrbTests
    {
        const string TowerPath = "Assets/Game/Addressables/Local/BG/Prefab/Object/Obj_Tower_Orb.prefab";
        const string OrbPath = "Assets/Game/Addressables/Local/BG/Prefab/Object/Obj_Stair_Orb.prefab";

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            StairOrbFlight.ClearForTests();
        }

        [Test]
        public void TowerOrbPrefab_WiresEmitterToStairOrbAndSpawnArea()
        {
            var tower = AssetDatabase.LoadAssetAtPath<GameObject>(TowerPath);
            Assert.That(tower, Is.Not.Null, TowerPath);
            var em = tower.GetComponentInChildren<StairOrbEmitter>(true);
            Assert.That(em, Is.Not.Null, "Obj_Tower_Orb에 StairOrbEmitter");
            var so = new SerializedObject(em);
            var orb = so.FindProperty("orbPrefab").objectReferenceValue as StairOrb;
            Assert.That(orb, Is.Not.Null, "orbPrefab");
            Assert.AreEqual(OrbPath, AssetDatabase.GetAssetPath(orb), "디자이너 Obj_Stair_Orb 원본");
            var area = so.FindProperty("spawnArea").objectReferenceValue as BoxCollider;
            Assert.That(area, Is.Not.Null, "spawnArea");
            Assert.AreEqual("Orb Spawn Area", area.name);
            Assert.That(so.FindProperty("origin").objectReferenceValue, Is.Not.Null, "origin");
            Assert.That(new SerializedObject(orb).FindProperty("particle").objectReferenceValue, Is.Not.Null, "Obj_Stair_Orb 파티클");
        }

        [Test]
        public void WellFlush_LaunchesOneOrbPerBrick_AndReturnsThemOnArrival()
        {
            var tower = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TowerPath));
            _made.Add(tower);
            var root = new GameObject("ViewsRoot");
            _made.Add(root);
            var viewGo = new GameObject("View");
            viewGo.transform.SetParent(root.transform, false);
            var view = viewGo.AddComponent<RunObjectView>();
            tower.transform.SetParent(viewGo.transform, false);
            var em = tower.GetComponentInChildren<StairOrbEmitter>(true);

            int launched = 0, arrived = 0;
            StairOrbFlight.Launched += n => launched += n;
            StairOrbFlight.Arrived += () => arrived++;
            var target = new Vector2(10, 20);
            StairOrbFlight.Target = (out Vector2 s) => { s = target; return true; };

            em.OnBind(view);
            em.OnRunEvent(view, new RunEvent(RunEventType.WellFlush, objId: 1, amount: 5, a: 3));
            Assert.AreEqual(3, launched, "구슬 수(e.A)만큼 출발 신호");
            Assert.AreEqual(3, StairOrbFlight.InFlight);
            Assert.AreEqual(1, em.ActiveCount, "첫 오브는 바로, 나머지는 간격 뒤");
            em.StepQueue(10f);
            Assert.AreEqual(0, em.QueuedCount);
            Assert.AreEqual(3, em.ActiveCount);

            var orbs = root.GetComponentsInChildren<StairOrb>(false);
            Assert.AreEqual(3, orbs.Length, "오브는 탑 뷰 밖(뷰 관리자 뿌리 아래)");
            foreach (var o in orbs) o.Step(o.TotalSec + 0.01f);
            Assert.AreEqual(3, arrived, "오브마다 도착");
            Assert.AreEqual(0, StairOrbFlight.InFlight);
            Assert.AreEqual(0, em.ActiveCount, "끝난 오브는 풀로");
            Assert.That(root.GetComponentsInChildren<StairOrb>(false).Length, Is.Zero, "돌아간 오브는 꺼진다");

            // 다시 비우면 풀에서 다시 쓴다(새로 만들지 않는다).
            em.OnRunEvent(view, new RunEvent(RunEventType.WellFlush, objId: 1, amount: 5, a: 2));
            em.StepQueue(10f);
            Assert.AreEqual(3, root.GetComponentsInChildren<StairOrb>(true).Length, "재사용");
        }

        [Test]
        public void Orb_DestroyedMidFlight_StillCountsAsArrived()
        {
            var tower = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TowerPath));
            _made.Add(tower);
            var viewGo = new GameObject("View");
            _made.Add(viewGo);
            var view = viewGo.AddComponent<RunObjectView>();
            var em = tower.GetComponentInChildren<StairOrbEmitter>(true);
            em.OnBind(view);
            em.OnRunEvent(view, new RunEvent(RunEventType.WellFlush, objId: 1, amount: 5, a: 2));
            Assert.AreEqual(2, StairOrbFlight.InFlight);
            Object.DestroyImmediate(tower);
            Assert.AreEqual(0, StairOrbFlight.InFlight, "탑이 지워지면 대기·비행 오브도 도착으로 친다(게이지가 멈추지 않게)");
        }
    }
}
