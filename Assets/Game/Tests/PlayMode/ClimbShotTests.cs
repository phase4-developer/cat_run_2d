using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using MV.Boot;
using MV.Save;
using MV.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MV.Tests
{
    /// <summary>
    /// 계단(마나 정산) 화면 촬영(MV-FIX-CLIMB5 2단계): 실제 Game 씬에서 런을 돌려 정산을 두 번 일으키고,
    /// 시작·돌기 중간·옆 시점·바퀴/등급 이펙트·다 오름·끝·돌아옴을 월드 카메라로 찍어 `Logs/ClimbShots`(git 제외)에 남긴다.
    /// 그래픽 장치가 있어야 뜻이 있으므로 이름으로 골라 `--graphics`로만 돌린다(Explicit). 판정은 사람이 PNG를 보고 한다.
    /// 지면 높이·카메라·옆 시점 진행은 같은 폴더 `climb_log.txt`에 적는다.
    /// </summary>
    [Explicit("화면 촬영: --graphics로 이름을 골라 돌린다")]
    public sealed class ClimbShotTests
    {
        const float TimeoutSeconds = 240f;
        const int ShotW = 720, ShotH = 1280, Climbs = 2;
        const int StairsTopUp = 90;   // 바퀴 카드(75칸)와 등급이 모두 나오게 한다

        static string ShotDir => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "ClimbShots");

        MvMemorySavePrefs _prefs;
        RenderTexture _rt;
        readonly StringBuilder _log = new StringBuilder();

        [SetUp]
        public void Setup()
        {
            _prefs = MvSave.UseMemoryPreferences();
            if (Directory.Exists(ShotDir)) Directory.Delete(ShotDir, true);
            Directory.CreateDirectory(ShotDir);
            _rt = new RenderTexture(ShotW, ShotH, 24);
        }

        [UnityTearDown]
        public IEnumerator EndGame() => PlayModeIsolation.EndGame();

        [TearDown]
        public void TearDown()
        {
            var loop = MvGameLoop.Current;
            if (loop != null) { loop.ScriptedDrag = null; loop.FixedDt = null; loop.TicksPerFrame = 1; }
            if (_rt != null) { _rt.Release(); UnityEngine.Object.DestroyImmediate(_rt); }
            File.WriteAllText(Path.Combine(ShotDir, "climb_log.txt"), _log.ToString());
            MvBootMeta.Detach();
            MvSave.Shutdown();
            MvSave.PreferencesOverride = null;
        }

        // ---- 런 상태 읽기(MV.Run은 이 어셈블리가 참조하지 않아 반사로 읽는다) ----
        static object Sim(MvGameLoop loop) => typeof(MvGameLoop).GetField("_sim", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(loop);
        static object State(MvGameLoop loop) { var s = Sim(loop); return s?.GetType().GetProperty("State")?.GetValue(s); }
        static object Get(object o, string name)
        {
            if (o == null) return null;
            var t = o.GetType();
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (f != null) return f.GetValue(o);
            return t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(o);
        }
        static void Set(object o, string name, object v)
        {
            var t = o.GetType();
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (f != null) { f.SetValue(o, Convert.ChangeType(v, f.FieldType)); return; }
            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            p.SetValue(o, Convert.ChangeType(v, p.PropertyType));
        }
        static double D(object o, string name) => Convert.ToDouble(Get(o, name) ?? 0.0);
        static bool InClimb(object s) => s != null && Get(s, "Phase")?.ToString() == "Climb" && Get(s, "Climb") != null;

        void Shot(Camera cam, string name, object s, CameraModel m)
        {
            var req = new RenderPipeline.StandardRequest { destination = _rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else { var prevT = cam.targetTexture; cam.targetTexture = _rt; cam.Render(); cam.targetTexture = prevT; }
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            var c = Get(s, "Climb");
            _log.AppendLine($"{name}: phase {Get(s, "Phase")} WY {D(s, "WorldY"):0.###} P.Z {D(Get(s, "P"), "Z"):0.##} " +
                            (c != null ? $"climb t {D(c, "Time"):0.###} pos {D(c, "Pos"):0.##}/{Get(c, "K")} endAt {D(c, "EndAt"):0.###} lap {Get(c, "Lap")} tier {Get(c, "Tier")} " : "") +
                            $"orbitE {m.OrbitE:0.###} cam {cam.transform.position} look {m.Look} camLift {m.CamLift:0.###}");
        }

        [UnityTest]
        public IEnumerator TwoClimbs_ShotsAtKeyMoments()
        {
            // Explicit여도 어셈블리 단위 실행에 끼어든다. 그래픽 장치 없이(-nographics) 렌더 요청을 내면 Unity가 죽으므로 건너뛴다.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("그래픽 장치 없음: --graphics로 돌린다");
            SceneManager.LoadScene(GameBootstrap.BootSceneName, LoadSceneMode.Single);
            yield return null;
            float elapsed = 0f;
            while ((GameBootstrap.State == BootState.NotStarted || GameBootstrap.State == BootState.Running) && elapsed < TimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(BootState.Succeeded, GameBootstrap.State, GameBootstrap.LastError?.ToString());
            var loop = MvGameLoop.Current;
            Assert.IsNotNull(loop);
            var world = UnityEngine.Object.FindFirstObjectByType<WorldView>();
            Assert.IsNotNull(world, "Game 씬에 WorldView가 없다");
            var cam = world.Rig.Cam;
            var model = world.Rig.Model;
            _log.AppendLine($"graphics {SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName}");

            loop.ScriptedDrag = f => 0;
            loop.FixedDt = 1 / 60.0;
            loop.TicksPerFrame = 8;
            Assert.IsTrue(loop.PressStart(), "출발이 런을 시작하지 않았다");

            int done = 0;
            elapsed = 0f;
            while (done < Climbs && loop.Phase == MvLoopPhase.Run && elapsed < TimeoutSeconds)
            {
                var s = State(loop);
                if (s != null && !InClimb(s) && Convert.ToInt32(Get(s, "Stairs")) < StairsTopUp) Set(s, "Stairs", StairsTopUp);
                if (!InClimb(s)) { elapsed += Time.unscaledDeltaTime; yield return null; continue; }

                // 정산 시작: 시뮬도 실시간(dt = 실제 프레임 시간)으로 돌려 카메라 시계와 맞추고 사람이 보는 속도로 찍는다.
                loop.TicksPerFrame = 1;
                loop.FixedDt = null;
                string tag = $"c{done + 1}";
                var climb = Get(s, "Climb");
                var shots = new List<(string name, Func<bool> when)>
                {
                    ("01_start", () => true),
                    ("02_orbitHalf", () => model.OrbitE >= 0.5),
                    ("03_side", () => model.OrbitE >= 0.999),
                    ("04_mid", () => D(climb, "Pos") >= Convert.ToInt32(Get(climb, "K")) * 0.5),
                    ("05_lap", () => Convert.ToInt32(Get(climb, "Lap")) >= 1 || D(climb, "EndAt") >= 0),
                    ("06_top", () => D(climb, "EndAt") >= 0),
                };
                int next = 0, lapFrames = -1;
                while (InClimb(State(loop)) && elapsed < TimeoutSeconds)
                {
                    if (lapFrames > 0) lapFrames--;
                    while (next < shots.Count && shots[next].when())
                    {
                        if (shots[next].name == "05_lap")
                        {
                            if (lapFrames < 0) { lapFrames = 6; break; }   // 이펙트가 퍼진 뒤 찍는다
                            if (lapFrames > 0) break;
                        }
                        Shot(cam, $"{tag}_{shots[next].name}", s, model);
                        next++;
                    }
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                Shot(cam, $"{tag}_07_end", s, model);
                for (int i = 0; i < 6; i++) yield return null;
                Shot(cam, $"{tag}_08_land", s, model);
                float back = 0;
                while (model.OrbitE > 0.5 && back < 3) { back += Time.unscaledDeltaTime; yield return null; }
                Shot(cam, $"{tag}_09_returnHalf", s, model);
                back = 0;
                while (model.OrbitE > 0.0001 && back < 3) { back += Time.unscaledDeltaTime; yield return null; }
                for (int i = 0; i < 20; i++) yield return null;
                Shot(cam, $"{tag}_10_back", s, model);
                done++;
                loop.TicksPerFrame = 8;
                loop.FixedDt = 1 / 60.0;
            }
            Debug.Log("[ClimbShotTests]\n" + _log);
            Assert.AreEqual(Climbs, done, $"정산 {done}번만 일어났다(단계 {loop.Phase})");
        }
    }
}
