using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MV.Boot;
using MV.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MV.Tests
{
    /// <summary>
    /// 룰렛 카드 비행 크기 보간 점검(MV-UI3-CLIMBFX3): 실제 Game 씬에서 계단·룰렛을 일으키고, Card Pos로 나는 카드의
    /// 크기(localScale)를 프레임마다 적어 `Logs/CardFlyProbe/fly_log.txt`(git 제외)에 남긴다.
    /// 디자이너 보고 「1 → 0.45가 서서히가 아니라 한 번에 바뀌어 보인다」(2026-11-23)를 숫자로 본다. 이름으로 골라 돌린다(Explicit).
    /// 이 시험 어셈블리는 MV.UI·MV.Run을 참조하지 않으므로(asmdef는 통합 Writer) 형식 이름·반사로 읽는다.
    /// </summary>
    [Explicit("진단: 이름으로 골라 돌린다")]
    public sealed class CardFlyScaleProbe
    {
        const float TimeoutSeconds = 240f;
        const int StairsTopUp = 90;

        static string LogDir => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "CardFlyProbe");

        MvMemorySavePrefs _prefs;
        readonly StringBuilder _log = new StringBuilder();

        [SetUp]
        public void Setup()
        {
            _prefs = MvSave.UseMemoryPreferences();
            Directory.CreateDirectory(LogDir);
        }

        [UnityTearDown]
        public IEnumerator EndGame() => PlayModeIsolation.EndGame();

        [TearDown]
        public void TearDown()
        {
            var loop = MvGameLoop.Current;
            if (loop != null) { loop.ScriptedDrag = null; loop.FixedDt = null; loop.TicksPerFrame = 1; loop.SetTimeScale(1); }
            Time.timeScale = 1f;
            File.WriteAllText(Path.Combine(LogDir, $"fly_log_{TestContext.CurrentContext.Test.Name}.txt"), _log.ToString());
            MvBootMeta.Detach();
            MvSave.Shutdown();
            MvSave.PreferencesOverride = null;
        }

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

        static Type FindType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null);

        sealed class Flight
        {
            public Component Card;
            public readonly List<(float t, float scale, Vector3 pos, bool holderShown)> Samples = new List<(float, float, Vector3, bool)>();
        }

        [UnityTest]
        public IEnumerator RouletteCardFly_ScaleChangesOverManyFrames() => Fly(1);

        /// <summary>MV-CLOCK-2: 2배속(Time.timeScale 2)이면 HUD 카드 비행도 게임 시간을 따라 실제 시간 약 절반(flySec/2)에 끝난다.</summary>
        [UnityTest]
        public IEnumerator RouletteCardFly_At2x_FollowsGameTime() => Fly(2);

        IEnumerator Fly(int speed)
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

            var posType = FindType("MV.UI.CardPosSection");
            Assert.IsNotNull(posType, "MV.UI.CardPosSection 형식");
            var flyingField = posType.GetField("_flying", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(flyingField, "CardPosSection._flying");

            loop.ScriptedDrag = f => 0;
            loop.FixedDt = 1 / 60.0;
            loop.TicksPerFrame = 8;
            loop.SetTimeScale(speed);
            Assert.IsTrue(loop.PressStart(), "출발이 런을 시작하지 않았다");
            Assert.AreEqual((float)speed, Time.timeScale, "런 배속 = Time.timeScale");

            var flights = new List<Flight>();
            var seen = new HashSet<Component>();
            bool climbed = false;
            float rouletteEnd = -1;
            elapsed = 0f;
            float t0 = Time.realtimeSinceStartup;
            Component pos = null;
            while (loop.Phase == MvLoopPhase.Run && elapsed < TimeoutSeconds)
            {
                var s = State(loop);
                bool inClimb = s != null && Get(s, "Phase")?.ToString() == "Climb";
                bool roulette = s != null && Get(s, "Roulette") != null;
                if (!climbed && s != null && !inClimb && !roulette && Convert.ToInt32(Get(s, "Stairs")) < StairsTopUp) Set(s, "Stairs", StairsTopUp);
                if (inClimb || roulette)
                {
                    climbed = true;
                    // 정산·룰렛은 실제 프레임 간격으로 돌려 사람이 보는 속도로 본다(배속은 Time.timeScale).
                    loop.TicksPerFrame = 1;
                    loop.FixedDt = null;
                }
                if (!pos) pos = UnityEngine.Object.FindFirstObjectByType(posType, FindObjectsInactive.Include) as Component;
                if (pos && flyingField.GetValue(pos) is IList list)
                {
                    float now = Time.realtimeSinceStartup - t0;
                    var cur = new HashSet<Component>();
                    foreach (Component c in list)
                    {
                        if (!c) continue;
                        cur.Add(c);
                        if (!seen.Contains(c))
                        {
                            flights.Add(new Flight { Card = c });   // 지난 프레임에 없던 카드 = 새 비행(풀 재사용 포함)
                            var crt = (RectTransform)c.transform;
                            var prt = (RectTransform)pos.transform;
                            _log.AppendLine($"[fit] card rect {crt.rect.size} parent {crt.parent.name} parentLossy {crt.parent.lossyScale.x:0.####} | CardPos local {prt.localScale.x:0.####} lossy {prt.lossyScale.x:0.####} active {prt.gameObject.activeInHierarchy}");
                            if (posType.GetProperty("PosCards")?.GetValue(pos) is IList holders)
                                foreach (Component h in holders)
                                {
                                    var hr = (RectTransform)h.transform;
                                    _log.AppendLine($"[fit]   holder card {hr.name} rect {hr.rect.size} local {hr.localScale.x:0.####} lossy {hr.lossyScale.x:0.####} parent {hr.parent.name} parentLocal {hr.parent.localScale.x:0.####} active {h.gameObject.activeSelf}");
                                }
                        }
                        var fl = flights.Last(f => f.Card == c);
                        fl.Samples.Add((now, c.transform.localScale.x, c.transform.localPosition, false));
                    }
                    seen = cur;
                }
                if (climbed && !inClimb && !roulette)
                {
                    if (rouletteEnd < 0) rouletteEnd = elapsed;
                    if (elapsed - rouletteEnd > 1f) break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            float flySec = pos ? Convert.ToSingle(posType.GetProperty("FlySec")?.GetValue(pos) ?? -1f) : -1f;
            _log.AppendLine($"graphics {SystemInfo.graphicsDeviceType} speed {speed} flySec {flySec} flights {flights.Count}");
            foreach (var f in flights)
            {
                _log.AppendLine($"-- flight {flights.IndexOf(f)} samples {f.Samples.Count}");
                foreach (var x in f.Samples) _log.AppendLine($"t {x.t:0.0000} scale {x.scale:0.0000} pos {x.pos.x:0.0},{x.pos.y:0.0}");
            }
            Debug.Log("[CardFlyScaleProbe]\n" + _log);

            Assert.That(climbed, Is.True, "계단 정산이 일어나지 않았다");
            Assert.That(flights.Count, Is.GreaterThan(0), "날아간 카드를 보지 못했다");
            foreach (var f in flights)
            {
                float a = f.Samples.First().scale, b = f.Samples.Min(x => x.scale);
                int mid = f.Samples.Count(x => x.scale < a - 0.02f && x.scale > b + 0.02f);
                Assert.That(a - b, Is.GreaterThan(0.1f), $"비행 {flights.IndexOf(f)}: 크기가 줄지 않았다({a:0.###} → {b:0.###})");
                Assert.That(mid, Is.GreaterThanOrEqualTo(3), $"비행 {flights.IndexOf(f)}: 중간 크기 프레임이 {mid}개뿐(한 번에 바뀜)");
                // 비행 실제 시간 = 첫 표본 → 도착 크기(최솟값)에 처음 닿은 표본. 게임 시간 flySec ÷ 배속.
                float start = f.Samples.First().t, land = f.Samples.First(x => x.scale <= b + 0.003f).t;
                float real = land - start, want = flySec / speed;
                _log.AppendLine($"flight {flights.IndexOf(f)} real {real:0.###} want {want:0.###}");
                Assert.That(real, Is.InRange(want * 0.6f, want * 1.3f + 0.05f), $"비행 {flights.IndexOf(f)}: 실제 {real:0.###}초, 기대 {want:0.###}초(flySec {flySec} ÷ 배속 {speed})");
            }
            Debug.Log("[CardFlyScaleProbe] " + string.Join(" / ", flights.Select(f => $"{f.Samples.Count} samples")));
        }
    }
}
