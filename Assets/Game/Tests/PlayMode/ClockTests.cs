using System;
using System.Collections;
using System.Reflection;
using MV.Boot;
using MV.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MV.Tests
{
    /// <summary>
    /// 시계 하나(MV-CLOCK-1, 사용자 2026-11-23 「타임라인을 맞춰야 배속도 같이 적용되고 따로 놀지 않으니까」): 런 중 배속 = <c>Time.timeScale</c>,
    /// 시뮬은 게임 시간(<c>Time.deltaTime</c>)으로 진행해 <c>Time.timeScale</c> 0이면 멈추고, 로비로 돌아가면 1로 되돌린다.
    /// 이 시험 어셈블리는 MV.Run을 참조하지 않으므로(asmdef는 통합 Writer) 런 상태는 반사로 읽는다.
    /// </summary>
    public sealed class ClockTests
    {
        const float TimeoutSeconds = 120f;

        MvMemorySavePrefs _prefs;

        [SetUp]
        public void IsolateSave() => _prefs = MvSave.UseMemoryPreferences();

        [UnityTearDown]
        public IEnumerator EndGame()
        {
            Time.timeScale = 1f;
            return PlayModeIsolation.EndGame();
        }

        [TearDown]
        public void ReleaseSave()
        {
            Time.timeScale = 1f;
            var loop = MvGameLoop.Current;
            if (loop != null) { loop.ScriptedDrag = null; loop.FixedDt = null; loop.TicksPerFrame = 1; loop.SetTimeScale(1); }
            MvBootMeta.Detach();
            MvSave.Shutdown();
            MvSave.PreferencesOverride = null;
        }

        static object State(MvGameLoop loop)
        {
            var sim = typeof(MvGameLoop).GetField("_sim", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(loop);
            return sim?.GetType().GetProperty("State")?.GetValue(sim);
        }

        static double RunTime(MvGameLoop loop)
        {
            var s = State(loop);
            return s == null ? -1 : Convert.ToDouble(s.GetType().GetField("Time", BindingFlags.Instance | BindingFlags.Public).GetValue(s));
        }

        [UnityTest]
        public IEnumerator Run_SpeedIsTimeScale_PauseStopsSim_LobbyResets()
        {
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
            Assert.AreEqual(1f, Time.timeScale, "로비 = 1배");

            loop.ScriptedDrag = f => 0;
            loop.SetTimeScale(2);
            Assert.AreEqual(1f, Time.timeScale, "로비에서는 배속을 걸지 않는다");
            Assert.IsTrue(loop.PressStart(), "출발이 런을 시작하지 않았다");
            Assert.AreEqual(2f, Time.timeScale, "런 시작 = 배속");
            loop.SetTimeScale(1);
            Assert.AreEqual(1f, Time.timeScale, "런 중 배속 바꾸기 = Time.timeScale");
            loop.SetTimeScale(2);

            // 게임 시간과 시뮬 런 시간이 같이 간다(2배속: 실제 시간의 약 2배).
            for (int i = 0; i < 10; i++) yield return null;
            double t0 = RunTime(loop);
            float g0 = Time.time, r0 = Time.unscaledTime;
            for (int i = 0; i < 30; i++) yield return null;
            double ran = RunTime(loop) - t0;
            float game = Time.time - g0, real = Time.unscaledTime - r0;
            Debug.Log($"[ClockTests] 2x ran {ran:0.####} game {game:0.####} real {real:0.####}");
            Assert.Greater(ran, 0, "런이 진행한다");
            // 히트스톱·프레임 상한(dtCap)으로 시뮬이 덜 갈 수는 있어도 게임 시간보다 더 가지는 않는다. 2배속이면 실제 시간보다 확실히 더 간다.
            Assert.LessOrEqual(ran, game * 1.05 + 0.02, "시뮬 런 시간 ≤ 게임 시간");
            Assert.Greater(ran, real * 1.2, "2배속: 실제 시간보다 빠르다");

            // 일시정지(Time.timeScale 0): 시뮬이 멈춘다.
            Time.timeScale = 0f;
            yield return null;
            double p0 = RunTime(loop);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.AreEqual(p0, RunTime(loop), 1e-12, "timeScale 0이면 시뮬 정지");
            loop.SetTimeScale(2);
            Assert.AreEqual(2f, Time.timeScale);
        }
    }
}
