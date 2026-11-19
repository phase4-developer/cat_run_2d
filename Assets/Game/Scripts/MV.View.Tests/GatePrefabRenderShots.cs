using System.Collections.Generic;
using System.IO;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 점검용 촬영(직접 지정해야만 돈다): 계단문·무기문·계단 구역 프리팹을 디자이너 시안과 같은 정면 직교 시점으로 찍어
    /// 프로젝트 `Temp/MvRender/*.png`에 남긴다. 1 m = <see cref="PxPerUnit"/> px, 1 m 격자선을 함께 그린다. 그래픽 장치가 있는 실행에서만 의미가 있다.
    /// 글자판 내용은 실제 런과 같은 <see cref="ObjectLabel.Of"/> 결과를 입힌다.
    /// </summary>
    [Explicit("점검 촬영 — 이름으로 지정할 때만")]
    public sealed class GatePrefabRenderShots
    {
        const int PxPerUnit = 100;
        const int Width = 600, Height = 800;
        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
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

        [Test]
        public void Shoot_GateAndZonePrefabs()
        {
            var t = ViewTestTables.Load();
            Shoot(t, "Obj_StairGate", "sgate", o => { o.N = 320; o.Prog = 0.5; });
            Shoot(t, "Obj_CraftGate", "cgate", o => { o.Made = 1; o.Prog = 0.5; });
            Shoot(t, "Obj_StairZone", "stair", o => { });
        }

        void Shoot(MvTables t, string prefab, string key, System.Action<RunObject> setup)
        {
            var src = LoadPrefab(prefab);
            Assert.That(src, Is.Not.Null, prefab);
            var go = Object.Instantiate(src);
            _made.Add(go);
            go.transform.position = new Vector3(0, 0, 100);
            var o = RunObjectFactory.Create(t, t.ObjectKind.ByKey(key), 0, 10);
            setup(o);
            var label = CountLabel.Find(go.transform);
            if (label != null) label.Apply(ObjectLabel.Of(o, 0, 3, 0, 1));
            foreach (var tmp in go.GetComponentsInChildren<TMPro.TMP_Text>(true)) tmp.ForceMeshUpdate(true, true);

            var camGo = new GameObject("ShotCam");
            _made.Add(camGo);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Height * 0.5f / PxPerUnit;
            cam.transform.position = new Vector3(0, cam.orthographicSize - 1f, 80);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.35f, 0.42f, 0.55f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _made.Add(rt);
            cam.targetTexture = rt;
            cam.aspect = (float)Width / Height;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            _made.Add(tex);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            RenderTexture.active = prev;

            // 1 m 격자(세계 x·y 정수 자리). 화면 아래 끝 = y −1 m.
            var grid = new Color(0f, 0f, 0f, 1f);
            for (int u = -3; u <= 3; u++)
            {
                int x = Width / 2 + u * PxPerUnit;
                if (x < 0 || x >= Width) continue;
                for (int y = 0; y < Height; y += 2) tex.SetPixel(x, y, grid);
            }
            for (int v = -1; v <= 7; v++)
            {
                int y = (v + 1) * PxPerUnit;
                if (y < 0 || y >= Height) continue;
                for (int x = 0; x < Width; x += 2) tex.SetPixel(x, y, v == 0 ? Color.red : grid);
            }
            tex.Apply();

            var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "MvRender");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, prefab + ".png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            TestContext.WriteLine($"{prefab}: {file}");
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
                TestContext.WriteLine($"  {r.name}: bounds min {r.bounds.min} max {r.bounds.max} order {r.sortingOrder} {(r is SpriteRenderer s && s.sprite != null ? s.sprite.name : "")}");
        }
    }
}
