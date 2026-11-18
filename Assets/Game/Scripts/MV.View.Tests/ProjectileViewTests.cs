using System.Collections.Generic;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 투사체 관리자(<see cref="ProjectileView"/>)는 WorldRoot 프리팹의 자식이고, 종류별 프리팹 모듈(Proj_Toy·Proj_Spear·Proj_OrbBlade)을 풀로 쓴다.
    /// 그림 캐시 없이 돌려 그림은 붙지 않는다(보이는 수 0). 실제 화면 그림은 사람 확인(NOT_RUN).
    /// </summary>
    public sealed class ProjectileViewTests
    {
        const string WorldRootPath = "Assets/Game/Prefabs/WorldRoot.prefab";
        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        static ProjectileView PrefabView()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRootPath);
            Assert.That(root, Is.Not.Null, WorldRootPath);
            var world = root.GetComponent<WorldView>();
            Assert.That(world.Projectiles, Is.Not.Null, "WorldView._projs = 자식 Projectiles");
            return world.Projectiles;
        }

        [Test]
        public void WorldRoot_Projectiles_ConnectsKindPrefabs()
        {
            var so = new SerializedObject(PrefabView());
            foreach (var field in new[] { "_toyPrefab", "_spearPrefab", "_bladePrefab" })
            {
                var slot = so.FindProperty(field).objectReferenceValue as ProjectileSlot;
                Assert.That(slot, Is.Not.Null, field);
                Assert.That(slot.HasRefs, Is.True, field + ": 프리팹 안 직렬화 참조");
                Assert.That(slot.GetComponent<DepthSortable>().Rank, Is.EqualTo(DepthMath.RankUnit), field + ": 정렬 순위 Unit");
                Assert.That(slot.GetComponent<DepthSortable>().Fog, Is.False, field + ": 안개 알파 없음");
            }
        }

        [Test]
        public void Sync_RentsOneSlotPerKind_BladesFromTableCount()
        {
            var t = ViewTestTables.Load();
            var view = Object.Instantiate(PrefabView().gameObject).GetComponent<ProjectileView>();
            _made.Add(view.gameObject);
            view.Init(t, ViewTestTables.Config(), null, null, WorldView.FxAtlas);
            Assert.That(view.Ready, Is.True);
            int orbs = OrbConfig.From(t.RunConfig).Count;
            Assert.That(view.GetComponentsInChildren<OrbBlade>(true).Length, Is.EqualTo(orbs), "회전 장난감 수 = 표 ORB_COUNT");

            var s = new RunState(null, t, new Pcg32Rng(1));
            var toy = new RunProjectile { Id = 1, X = 0, Z = 5 };
            s.Projs.Add(toy);
            s.Projs.Add(new RunProjectile { Id = 2, X = 0, Z = 6, Spear = true });
            view.Sync(s, 0);
            Assert.That(view.PoolMade, Is.EqualTo(2));
            Assert.That(view.GetComponentsInChildren<ToyProjectile>(true).Length, Is.EqualTo(1), "던진 장난감 모듈");
            Assert.That(view.GetComponentsInChildren<SpearProjectile>(true).Length, Is.EqualTo(1), "창 모듈");
            Assert.That(view.VisibleProjs, Is.EqualTo(0), "그림이 읽히기 전에는 그리지 않는다");

            toy.Spear = true;   // 같은 번호가 창으로 다시 쓰이면 창 모듈로 바꾼다
            view.Sync(s, 0);
            Assert.That(view.GetComponentsInChildren<SpearProjectile>(true).Length, Is.EqualTo(2));
            Assert.That(view.PoolMade, Is.EqualTo(3));

            s.Projs.Clear();
            view.Sync(s, 0);
            view.Sync(s, 0);
            Assert.That(view.PoolMade, Is.EqualTo(3), "사라진 투사체는 풀로 돌아가고 새로 만들지 않는다");
            s.Projs.Add(new RunProjectile { Id = 7, Z = 3, Spear = true });
            s.Projs.Add(new RunProjectile { Id = 8, Z = 4 });
            view.Sync(s, 0);
            Assert.That(view.PoolMade, Is.EqualTo(3), "풀에서 다시 꺼낸다");
        }
    }
}
