using System.Collections.Generic;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 계단 정산 칸(MV-VIEW-CLIMB): 디자이너 프리팹 `Obj_ClimbStep` = 330 stepW·stepH·stepD 한 블록, 칸 = 블록 기둥(새 HTML startClimb·drawStairs3D),
    /// 떨어짐 dropH·(1 − f)², 꼭대기만 줄무늬, 옆 시점 칸 번호, 시야 밖 블록은 안 만듦.
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

        static GameObject Prefab(MvTables t)
        {
            string address = t.Climb.Config().stepPrefab;
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

        [Test]
        public void Prefab_MatchesClimbTableBlock()
        {
            var t = ViewTestTables.Load();
            var c = t.Climb.Config();
            var p = Prefab(t);
            var front = Size(p, "Front");
            var side = Size(p, "Side");
            Assert.That(front.x, Is.EqualTo(c["stepW"]).Within(0.01), "앞판 폭 = stepW");
            Assert.That(front.y, Is.EqualTo(c["stepH"]).Within(0.01), "앞판 높이 = stepH");
            Assert.That(side.x, Is.EqualTo(c["stepD"]).Within(0.01), "옆판 폭 = stepD");
            Assert.That(side.y, Is.EqualTo(c["stepH"]).Within(0.01), "옆판 높이 = stepH");
        }

        Camera SideCamera(double catY, double catZ)
        {
            var go = new GameObject("Cam");
            _made.Add(go);
            var cam = go.AddComponent<Camera>();
            var t = ViewTestTables.Load();
            var side = t.Camera.ByKey(CameraModel.KeySide);
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
            v.Init(t, ViewTestTables.Config(), cam, null);
            v.UseTemplate(Prefab(t));
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
        public void SideView_BuildsColumnsOnlyInsideFrustum_WithNumbersAndTopStripe()
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
            Assert.That(v.ActiveBlocks, Is.LessThan(k * (k + 1) / 2), "시야 밖 블록은 만들지 않는다");
            Assert.That(v.ActiveNumbers, Is.GreaterThan(0), "옆 시점이면 칸 번호");
            foreach (var b in Active(v))
            {
                int i = Mathf.RoundToInt((float)((b.position.z - z0) / c["stepD"]));
                int j = Mathf.RoundToInt((float)(b.position.y / c["stepH"]));
                Assert.That(j, Is.LessThanOrEqualTo(i), "칸 i 기둥은 블록 i + 1개");
                Assert.That(b.position.x, Is.EqualTo(0).Within(1e-5));
                var stripe = b.Find("Stripe").GetComponent<SpriteRenderer>();
                Assert.That(stripe.enabled, Is.EqualTo(j == i), "줄무늬는 꼭대기 블록만");
            }
        }

        [Test]
        public void NewStep_DropsFromDropHWithQuadraticEase()
        {
            var t = ViewTestTables.Load();
            var c = t.Climb.Config();
            const double z0 = 10;
            var v = NewView(t, SideCamera(0, z0));
            var s = Climbing(t, 20, z0, 1, 1);
            v.Sync(s);
            s.Climb.Time = 1 + c["dropSec"] * 0.5;
            v.Sync(s);
            var blocks = Active(v);
            Assert.That(blocks.Count, Is.EqualTo(1), "보인 칸 1개 = 블록 1개");
            Assert.That(blocks[0].position.y, Is.EqualTo(c["dropH"] * 0.25).Within(1e-4), "dropH·(1 − 0.5)²");
            Assert.That(blocks[0].position.z, Is.EqualTo(z0).Within(1e-4));
            s.Climb.Time = 1 + c["dropSec"];
            v.Sync(s);
            Assert.That(Active(v)[0].position.y, Is.EqualTo(0).Within(1e-4), "다 떨어지면 바닥");
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
}
