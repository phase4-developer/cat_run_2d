using System.Collections.Generic;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 계단 정산 칸(MV-VIEW-CLIMB·MV-VIEW-CLIMB3): 디자이너 프리팹 `Obj_ClimbStep`(등급별 `_C`·`_UC`·`_R`·`_E`·`_L`) = 330 stepW·stepH·stepD 한 블록,
    /// 칸 = 늘린 기둥 하나(새 HTML startClimb·drawStairs3D), 칸 프리팹 = 남은 칸 수의 카드 등급(115 climbStep, 노션 계단연출 2절),
    /// 칸 등장 = popSec 동안 0 → stepPop(1.1)배 → 1배로 튕김, 옆 시점 칸 번호, 시야 밖 칸은 안 만듦.
    /// </summary>
    public sealed class ClimbStairViewTests
    {
        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        static GameObject Prefab(string address)
        {
            foreach (var guid in AssetDatabase.FindAssets(address + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == address) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            Assert.Fail("프리팹 없음: " + address);
            return null;
        }

        static Vector3 Size(GameObject root, string child)
        {
            var r = root.transform.Find(child).GetComponent<SpriteRenderer>();
            var s = r.sprite.bounds.size;
            return Vector3.Scale(new Vector3(s.x, s.y, 0), r.transform.lossyScale);
        }

        static IEnumerable<string> AllAddresses(MvTables t)
        {
            yield return t.Climb.Config().stepPrefab;
            foreach (var r in t.CardTier.Datas) yield return r.climbStep;
        }

        [Test]
        public void Prefabs_MatchClimbTableBlock()
        {
            var t = ViewTestTables.Load();
            var c = t.Climb.Config();
            foreach (string address in AllAddresses(t))
            {
                var p = Prefab(address);
                var front = Size(p, "Front");
                var side = Size(p, "Side");
                Assert.That(front.x, Is.EqualTo(c["stepW"]).Within(0.01), address + " 앞판 폭 = stepW");
                Assert.That(front.y, Is.EqualTo(c["stepH"]).Within(0.01), address + " 앞판 높이 = stepH");
                Assert.That(side.x, Is.EqualTo(c["stepD"]).Within(0.01), address + " 옆판 폭 = stepD");
                Assert.That(side.y, Is.EqualTo(c["stepH"]).Within(0.01), address + " 옆판 높이 = stepH");
                Assert.That(p.transform.Find("Top"), Is.Not.Null, address + " 윗판");
                // 정렬 부품·칸 번호 글자는 프리팹이 원본(실행 중에 붙이거나 만들지 않는다)
                Assert.That(p.GetComponent<UnityEngine.Rendering.SortingGroup>(), Is.Not.Null, address + " SortingGroup");
                Assert.That(p.GetComponent<DepthSortable>(), Is.Not.Null, address + " DepthSortable");
                var number = p.GetComponentInChildren<WorldLabel>(true);
                Assert.That(number, Is.Not.Null, address + " 칸 번호 글자(WorldLabel)");
                Assert.That(number.GetComponent<Billboard>(), Is.Not.Null, address + " 번호 빌보드");
                Assert.That(number.Text.font, Is.Not.Null, address + " 번호 글꼴");
            }
        }

        Camera SideCamera(double catY, double catZ)
        {
            var go = new GameObject("Cam");
            _made.Add(go);
            var cam = go.AddComponent<Camera>();
            var side = ViewTestTables.Camera().side;
            cam.fieldOfView = (float)side.fovV;
            cam.nearClipPlane = (float)side.near;
            cam.farClipPlane = (float)side.far;
            cam.aspect = 9f / 16f;
            go.transform.position = new Vector3((float)side.posX, (float)(catY + side.posY), (float)(catZ + side.posZ));
            go.transform.LookAt(new Vector3((float)side.lookX, (float)(catY + side.lookY), (float)(catZ + side.lookZ)));
            return cam;
        }

        ClimbStairView NewView(MvTables t, Camera cam)
        {
            var go = new GameObject("Stairs");
            _made.Add(go);
            var v = go.AddComponent<ClimbStairView>();
            v.Init(t, ViewTestTables.Config(), cam);
            v.UseTemplates(Prefab);
            return v;
        }

        static RunState Climbing(MvTables t, int k, double z0, double time, int shown, double total = 100)
        {
            var s = new RunState(null, t, new Pcg32Rng(1));
            s.Phase = RunPhase.Climb;
            s.Climb = new ClimbState { N = k, K = k, Pw = 1, Z0 = z0, ZEnd = z0 + k, T = total, Time = time, StepsShown = shown };
            return s;
        }

        static List<Transform> Active(ClimbStairView v)
        {
            var list = new List<Transform>();
            foreach (Transform c in v.transform) if (c.gameObject.activeSelf) list.Add(c);
            return list;
        }

        [Test]
        public void SideView_BuildsColumnsOnlyInsideFrustum_WithNumbersAndTierPrefab()
        {
            var t = ViewTestTables.Load();
            var c = t.Climb.Config();
            const int k = 60;
            const double z0 = 10;
            int at = 30;
            double catY = at * c["stepH"], catZ = z0 + at * c["stepD"];
            var v = NewView(t, SideCamera(catY, catZ));
            v.Sync(Climbing(t, k, z0, 50, k, 50));   // 끝난 순간: 모든 칸 내려앉음
            Assert.That(v.ActiveBlocks, Is.GreaterThan(0));
            Assert.That(v.ActiveBlocks, Is.LessThan(k), "시야 밖 칸은 만들지 않는다");
            Assert.That(v.ActiveNumbers, Is.GreaterThan(0), "옆 시점이면 칸 번호");
            var seen = new HashSet<int>();
            foreach (var b in Active(v))
            {
                int i = Mathf.RoundToInt((float)((b.position.z - z0) / c["stepD"]));
                Assert.That(seen.Add(i), Is.True, "칸 하나 = 기둥 하나");
                Assert.That(b.position.y, Is.EqualTo(0).Within(1e-4), "기둥 바닥 = 오르기 시작 높이");
                Assert.That(b.position.x, Is.EqualTo(0).Within(1e-5));
                Assert.That(b.name, Does.StartWith(v.AddressOf(i) + "("), "칸 " + i + " 프리팹 = 그 칸 등급");
                var top = b.Find("Top");
                var top0 = Prefab(v.AddressOf(i)).transform.Find("Top");
                Assert.That(top.localPosition.y - top0.localPosition.y, Is.EqualTo(i * c["stepH"]).Within(1e-4), "윗판 = 칸 높이만큼 올라감");
            }
        }

        [Test]
        public void StepPrefab_FollowsCardTierOfRemainingCount_BaseBelowFirstTier()
        {
            var t = ViewTestTables.Load();
            var c = t.Climb.Config();
            int lap = (int)c["lap"], minNeed = int.MaxValue;
            foreach (var r in t.CardTier.Datas) minNeed = System.Math.Min(minNeed, r.need);
            int k = lap + minNeed;
            var v = NewView(t, SideCamera(0, 10));
            v.Sync(Climbing(t, k, 10, 0, 1));   // Pw = 1: 칸 i의 값 v = i + 1
            Assert.That(v.AddressOf(minNeed - 2), Is.EqualTo(c.stepPrefab), "첫 등급 기준 아래 = 330 stepPrefab");
            foreach (var r in t.CardTier.Datas)
                Assert.That(v.AddressOf(r.need - 1), Is.EqualTo(r.climbStep), r.need + "칸 = " + r.climbStep);
            Assert.That(v.AddressOf(lap), Is.EqualTo(c.stepPrefab), "한 바퀴(lap) 넘으면 남은 칸 수로 다시 센다(새 HTML rem)");
        }

        [Test]
        public void NewStep_PopsToStepPopThenSettles()
        {
            var t = ViewTestTables.Load();
            var c = t.Climb.Config();
            const double z0 = 10;
            var v = NewView(t, SideCamera(0, z0));
            var s = Climbing(t, 20, z0, 1, 1);
            v.Sync(s);
            double back = ClimbStairView.PopBack(c["stepPop"]);
            // 이징 최댓값 = stepPop(기획서 1.1배), 시작 0, 끝 1
            double peak = 0;
            for (int i = 0; i <= 1000; i++) peak = System.Math.Max(peak, ClimbStairView.PopScale(i / 1000.0, back));
            Assert.That(peak, Is.EqualTo(c["stepPop"]).Within(1e-4), "가장 클 때 stepPop배");
            Assert.That(ClimbStairView.PopScale(0, back), Is.EqualTo(0));
            Assert.That(ClimbStairView.PopScale(1, back), Is.EqualTo(1));
            s.Climb.Time = 1 + c["popSec"] * 0.5;
            v.Sync(s);
            var blocks = Active(v);
            Assert.That(blocks.Count, Is.EqualTo(1), "보인 칸 1개 = 블록 1개");
            Assert.That(blocks[0].localScale.y, Is.EqualTo((float)ClimbStairView.PopScale(0.5, back)).Within(1e-4), "popSec 절반의 크기");
            Assert.That(blocks[0].position.y, Is.EqualTo(0).Within(1e-4), "떨어지지 않고 바닥에서 생긴다");
            Assert.That(blocks[0].position.z, Is.EqualTo(z0).Within(1e-4));
            s.Climb.Time = 1 + c["popSec"];
            v.Sync(s);
            Assert.That(Active(v)[0].localScale.y, Is.EqualTo(1).Within(1e-4), "다 생기면 1배");
        }

        [Test]
        public void AfterClimb_KeepsLastStairsLanded_UntilNewRun()
        {
            var t = ViewTestTables.Load();
            const double z0 = 10;
            var v = NewView(t, SideCamera(0, z0));
            var s = Climbing(t, 5, z0, 0, 1);
            v.Sync(s);
            s.Phase = RunPhase.Run;
            v.Sync(s);
            Assert.That(v.ActiveBlocks, Is.GreaterThan(1), "끝나면 모든 칸이 내려앉은 채 남는다(HTML LASTC)");
            v.Sync(new RunState(null, t, new Pcg32Rng(2)));
            Assert.That(v.ActiveBlocks, Is.EqualTo(0), "새 런이면 지운다");
        }
    }

    /// <summary>계단 정산 옆 시점 가림 처리(노션 계단연출 3절, 새 HTML streetProps·XCUT·drawGround).</summary>
    public sealed class ClimbOcclusionTests
    {
        [Test]
        public void NearSideFades_FarSideStays()
        {
            Assert.That(ClimbOcclusion.NearAlpha(0, 5), Is.EqualTo(1f));
            Assert.That(ClimbOcclusion.NearAlpha(0.25, 5), Is.EqualTo(1 - 0.25 * 2.2).Within(1e-5));
            Assert.That(ClimbOcclusion.NearAlpha(0.5, 5), Is.EqualTo(0f), "e ≥ 1/2.2이면 안 보임");
            Assert.That(ClimbOcclusion.NearAlpha(1, -5), Is.EqualTo(1f), "먼 쪽은 그대로");
        }

        [Test]
        public void GroundCut_ShrinksToCutNearByE06()
        {
            Assert.That(ClimbOcclusion.CutX(0), Is.EqualTo(70).Within(1e-9));
            Assert.That(ClimbOcclusion.CutX(0.3), Is.EqualTo(70 - 64.9 * 0.5).Within(1e-9));
            Assert.That(ClimbOcclusion.CutX(0.6), Is.EqualTo(5.1).Within(1e-9));
            Assert.That(ClimbOcclusion.CutX(1), Is.EqualTo(5.1).Within(1e-9));
            Assert.That(ClimbOcclusion.ShowSection(9.2, 1), Is.True, "옆 시점 카메라(고양이 + 9.2 m)는 잘린 면 바깥");
            Assert.That(ClimbOcclusion.ShowSection(0, 0), Is.False, "뒤 시점은 단면 없음");
        }

        [Test]
        public void WheelTurnsPerStep_MatchesNotionFormula()
        {
            // 노션 계단연출 4절: 회전각 = pos·hypot(H, D)/r·0.6875, r = 0.373 m → 한 칸 약 1.54 rad(0.24바퀴). 사용자 결정 2026-11-16 「식대로 0.24바퀴」.
            var t = ViewTestTables.Load();
            var c = t.Climb.Config();
            double rad = System.Math.Sqrt(c["stepH"] * c["stepH"] + c["stepD"] * c["stepD"]) / 0.373 * 0.6875;
            Assert.That(t.RunConfig.D(RunConfigKeys.Char_WheelTurnsPerStep), Is.EqualTo(rad / (2 * System.Math.PI)).Within(1e-4));
        }
    }
}
