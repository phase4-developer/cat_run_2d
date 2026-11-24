using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>티켓 02 실행 구성 요소: DepthSorter가 근평면 숨김·sortingOrder·안개 알파(SpriteRenderer.color)를 매긴다(02 §2.3.2, 03 §3.8.9).</summary>
    public sealed class DepthSorterTests
    {
        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) Object.DestroyImmediate(go);
            _made.Clear();
        }

        DepthSortable Make(Vector3 pos, int rank, float bias, float probeY, bool fog)
        {
            var go = new GameObject("Sortable");
            _made.Add(go);
            go.transform.position = pos;
            var child = new GameObject("Sprite");
            child.transform.SetParent(go.transform, false);
            child.AddComponent<SpriteRenderer>().color = new Color(1f, 0.5f, 0.25f, 0.8f);
            var s = go.AddComponent<DepthSortable>();
            s.Rank = rank;
            s.Bias = bias;
            s.SortOffsetY = probeY;
            s.Fog = fog;
            s.Refresh();
            return s;
        }

        [Test]
        public void SortAll_OrdersByDepthHidesNearAndWritesFogAlphaToSpriteColor()
        {
            var cfg = ViewTestTables.Config();
            var cam = new Vector3(0, 13, -9);
            var fwd = (new Vector3(0, 0.5f, 5.5f) - cam).normalized;
            var near = Make(new Vector3(0, 0, 0), DepthMath.RankObject, 0, (float)cfg.ObjProbeY, true);
            var far = Make(new Vector3(0, 0, 60), DepthMath.RankObject, 0, (float)cfg.ObjProbeY, true);
            var cat = Make(new Vector3(0, 0, 60), DepthMath.RankUnit, 0, (float)cfg.CatProbeY, false);
            var behind = Make(new Vector3(0, 13, -20), DepthMath.RankProp, 0, 0, true);

            DepthSorter.SortAll(new List<DepthSortable> { near, far, cat, behind }, cam, fwd, cfg);

            Assert.That(far.Group.sortingOrder, Is.LessThan(near.Group.sortingOrder), "먼 것 먼저");
            var sr = behind.GetComponentInChildren<SpriteRenderer>();
            Assert.That(sr.enabled, Is.False, "카메라 뒤(d < nearCut)는 숨김");
            Assert.That(near.GetComponentInChildren<SpriteRenderer>().enabled, Is.True);

            var fwdD = new Vector3d(fwd.x, fwd.y, fwd.z);
            double dFar = DepthMath.Depth(new Vector3d(0, 0, 60), Vector3d.From(cam), fwdD);
            float expectA = 0.8f * (float)DepthMath.FogAlpha(dFar, cfg.FogStart, cfg.FogRange);
            var farColor = far.GetComponentInChildren<SpriteRenderer>().color;
            Assert.That(farColor.a, Is.EqualTo(expectA).Within(1e-4f), "안개 기준은 앵커(탐침 +0.8 아님)");
            Assert.That(farColor.r, Is.EqualTo(1f).Within(1e-6f), "rgb는 그대로");
            Assert.That(cat.GetComponentInChildren<SpriteRenderer>().color.a, Is.EqualTo(0.8f).Within(1e-6f), "고양이는 안개 없음");
            Assert.That(near.GetComponentInChildren<SpriteRenderer>().color.a, Is.EqualTo(0.8f).Within(1e-6f), "안개 시작 전");
        }

        /// <summary>
        /// 풀 재사용(MV-OBJ-POOLVISIBLE): 카메라를 지나 근평면 숨김(렌더러 끔) 상태로 풀에 돌아간 뷰를 다시 꺼내면
        /// 관리자가 <see cref="DepthSortable.Refresh"/>를 부른다. 그 뒤 화면 안으로 들어오면 렌더러가 다시 켜져야 한다.
        /// </summary>
        [Test]
        public void Refresh_AfterNearHide_ShowsAgainWhenInView()
        {
            var cfg = ViewTestTables.Config();
            var cam = new Vector3(0, 13, -9);
            var fwd = (new Vector3(0, 0.5f, 5.5f) - cam).normalized;
            var s = Make(new Vector3(0, 13, -20), DepthMath.RankObject, 0, (float)cfg.ObjProbeY, true);
            var sr = s.GetComponentInChildren<SpriteRenderer>();
            var list = new List<DepthSortable> { s };

            DepthSorter.SortAll(list, cam, fwd, cfg);
            Assert.That(sr.enabled, Is.False, "카메라 뒤는 숨김");

            s.Refresh();   // 풀에서 다시 꺼냄(ObjectViewFactory.Show)
            s.transform.position = new Vector3(0, 0, 30);
            DepthSorter.SortAll(list, cam, fwd, cfg);
            Assert.That(sr.enabled, Is.True, "풀에서 다시 꺼내 화면 안에 들어왔는데 렌더러가 꺼져 있다");
        }

        /// <summary>프리팹에서 일부러 끈 렌더러(계단문 Glow 파티클 등)는 근평면 숨김 → 다시 보임·풀 재사용 뒤에도 꺼진 채다.</summary>
        [Test]
        public void NearHide_ThenShow_KeepsPrefabDisabledRenderersOff()
        {
            var cfg = ViewTestTables.Config();
            var cam = new Vector3(0, 13, -9);
            var fwd = (new Vector3(0, 0.5f, 5.5f) - cam).normalized;
            var s = Make(new Vector3(0, 0, 30), DepthMath.RankObject, 0, (float)cfg.ObjProbeY, true);
            var on = s.GetComponentInChildren<SpriteRenderer>();
            var offGo = new GameObject("Off");
            offGo.transform.SetParent(s.transform, false);
            var off = offGo.AddComponent<SpriteRenderer>();
            off.enabled = false;
            s.Refresh();
            var list = new List<DepthSortable> { s };

            s.transform.position = new Vector3(0, 13, -20);
            DepthSorter.SortAll(list, cam, fwd, cfg);
            Assert.That(on.enabled, Is.False, "숨김");
            s.transform.position = new Vector3(0, 0, 30);
            DepthSorter.SortAll(list, cam, fwd, cfg);
            Assert.That(on.enabled, Is.True, "다시 보임");
            Assert.That(off.enabled, Is.False, "일부러 끈 렌더러는 다시 보일 때도 꺼진 채");

            s.transform.position = new Vector3(0, 13, -20);
            DepthSorter.SortAll(list, cam, fwd, cfg);
            s.Refresh();   // 숨긴 채 풀 재사용
            Assert.That(on.enabled, Is.True, "풀 재사용 때 원래 상태로 되돌림");
            Assert.That(off.enabled, Is.False, "풀 재사용 뒤에도 일부러 끈 렌더러는 꺼진 채");
        }
    }
}
