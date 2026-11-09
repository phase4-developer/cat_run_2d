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
    }
}
