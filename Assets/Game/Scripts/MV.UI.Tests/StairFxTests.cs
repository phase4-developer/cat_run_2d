using System.Collections.Generic;
using MV.Data;
using MV.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI.Tests
{
    /// <summary>
    /// MV-UI3-STAIRFX(노션 「IngameScreen : Stair 업그레이드 연출」): 계단 경험치 오브 도착 순간 시뮬이 실제 게이지를 올리고(MV-UI3-STAIRFX3) HUD 막대는
    /// Framework AnimatedGauge로 따라 오르며(MV-UI3-STAIRFX4), 레벨 넘김(바퀴)이 끝날 때마다 계단 칸 등급 업 연출(크기·Flash·Sparkle).
    /// 로비 MainUpgrade로 계단 레벨이 오를 때도 같은 부품. 화면 모양은 사람 확인(NOT_RUN 별도).
    /// EditMode에서는 AnimatedGauge.Update가 돌지 않으므로 시험이 <c>Gauge.Tick</c>을 직접 부른다.
    /// </summary>
    public sealed class StairFxTests
    {
        const string IngamePath = "Assets/Game/Art/UI/Prefab/UIScreen/IngameScreen.prefab";
        const string LobbyPath = "Assets/Game/Addressables/Local/UI/Lobby/UIScreen_Lobby.prefab";
        const string RunHudPath = "Assets/Game/Addressables/Local/UI/Hud/UIHud_Run.prefab";
        static MvTables T => MetaUiTestKit.Base();

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            StairOrbFlight.ClearForTests();
        }

        StairSection NewStair()
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(IngamePath));
            _made.Add(go);
            var s = go.GetComponentInChildren<StairSection>(true);
            Assert.That(s, Is.Not.Null, "Mid/Stair StairSection");
            Assert.That(s.Gauge, Is.Not.Null, "AnimatedGauge");
            s.ResetRun();
            return s;
        }

        static void Run(StairSection s, float seconds, float dt = 1f / 60f)
        {
            for (float t = 0; t < seconds; t += dt)
            {
                s.Gauge.Tick(dt);
                s.Tick(dt);
            }
        }

        [Test]
        public void IngameStair_WiresOrbAnchorAndGradeUpFx()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IngamePath);
            var s = prefab.GetComponentInChildren<StairSection>(true);
            Assert.That(s.OrbTarget, Is.Not.Null, "orbTarget");
            Assert.AreEqual("OrbPos Anchor", s.OrbTarget.name, "디자이너 노션 그림의 Mid/Stair/OrbPos Anchor");
            Assert.AreEqual(s.transform, s.OrbTarget.parent, "Mid/Stair 아래");
            Assert.That(s.GradeUpFx, Is.Not.Null, "gradeUpFx");
            Assert.AreEqual("Slot_Stair", s.GradeUpFx.name);
            Assert.That(s.GradeUpFx.HasFlash && s.GradeUpFx.HasSparkle, "Slot_Stair Flash·Effect_Sparkle 연결");
        }

        [Test]
        public void IngameStair_GaugeIsAnimatedGaugeOnDesignerSlider()
        {
            // MV-UI3-STAIRFX4: 디자이너 Slider 막대(Sliced Fill) 모양을 그대로 두고 AnimatedGauge가 Slider를 막대로 쓴다(ui.widgets pre.3).
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IngamePath);
            var s = prefab.GetComponentInChildren<StairSection>(true);
            Assert.That(s.Gauge, Is.Not.Null, "gaugeBar");
            var so = new SerializedObject(s.Gauge);
            Assert.That(so.FindProperty("fill").objectReferenceValue, Is.Null, "Filled Image 대신 Slider");
            var slider = so.FindProperty("fillSlider").objectReferenceValue as Slider;
            Assert.That(slider, Is.Not.Null, "fillSlider");
            Assert.AreEqual(s.Gauge.gameObject, slider.gameObject, "Progress_Level Slider");
            Assert.That(slider.wholeNumbers, Is.False, "정수로 끊기지 않는다");
            Assert.That(so.FindProperty("timing.lapDuration").floatValue, Is.GreaterThan(0f), "바퀴 시간(프리팹 직렬화)");
        }

        [Test]
        public void LobbyStair_WiresSameGradeUpFx()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPath);
            var v = prefab.GetComponentInChildren<LobbyDesignView>(true);
            Assert.That(v.StairGradeUpFx, Is.Not.Null, "stairGradeUpFx");
            Assert.AreEqual("Slot_Stair", v.StairGradeUpFx.name);
            Assert.That(v.StairGradeUpFx.HasFlash && v.StairGradeUpFx.HasSparkle, "로비 Slot_Stair Flash·Effect_Sparkle 연결");
        }

        [Test]
        public void FirstDraw_IsInstant_ThenRisesToSim()
        {
            var s = NewStair();
            s.SetGauge(T, 2, 0.25f);
            Assert.AreEqual(2, s.ShownLevel);
            Assert.AreEqual(0.25f, s.GaugeValue, 1e-3f, "첫 그리기는 바로");
            s.SetGauge(T, 2, 0.6f);
            Assert.AreEqual(0.25f, s.GaugeValue, 1e-3f, "값이 바뀐 프레임에는 아직 그대로");
            Run(s, 0.05f);
            Assert.That(s.GaugeValue, Is.GreaterThan(0.25f).And.LessThan(0.6f), "따라 오르는 중");
            Run(s, 1f);
            Assert.AreEqual(0.6f, s.GaugeValue, 1e-3f, "Timing 뒤 시뮬 값");
            Assert.AreEqual(0, s.GradeUpFx.PlayCount, "첫 그리기·같은 레벨은 연출 없음");
        }

        [Test]
        public void LevelUp_LapCompletes_ThenGradeUpFxOnce()
        {
            var s = NewStair();
            s.SetGauge(T, 2, 0.2f);
            s.SetGauge(T, 2, 0.8f); // 첫 오브 도착 몫
            Run(s, 1f);
            Assert.AreEqual(0, s.GradeUpFx.PlayCount);

            s.SetGauge(T, 3, 0.4f); // 마지막 오브 도착 몫(레벨 업)
            Assert.AreEqual(2, s.ShownLevel, "바퀴가 끝나기 전에는 이전 레벨");
            Assert.AreEqual(0, s.GradeUpFx.PlayCount);
            Run(s, 1f);
            Assert.AreEqual(3, s.ShownLevel);
            Assert.AreEqual(0.4f, s.GaugeValue, 1e-3f);
            Assert.AreEqual(1, s.GradeUpFx.PlayCount, "레벨 넘김(바퀴) 1번에 등급 업 연출 1번");
            Assert.AreEqual("+3", s.StairValueText);
        }

        [Test]
        public void TwoLevels_TwoLaps_TwoFx()
        {
            var s = NewStair();
            s.SetGauge(T, 2, 0.2f);
            s.SetGauge(T, 4, 0.3f);
            Run(s, 2f);
            Assert.AreEqual(4, s.ShownLevel);
            Assert.AreEqual(2, s.GradeUpFx.PlayCount);
            Assert.AreEqual(0.3f, s.GaugeValue, 1e-3f);
        }

        [Test]
        public void NewValueDuringLap_KeepsTheLap()
        {
            // 바퀴 도중 다음 오브 몫이 오면 AnimatedGauge가 바퀴를 다시 짠다. 남은 바퀴(시뮬 − 보이는 레벨)를 다시 줘야 레벨 업이 사라지지 않는다.
            var s = NewStair();
            s.SetGauge(T, 2, 0.2f);
            s.SetGauge(T, 3, 0.1f);
            Run(s, 0.05f);
            Assert.AreEqual(2, s.ShownLevel, "아직 바퀴 중");
            s.SetGauge(T, 3, 0.3f);
            Run(s, 1f);
            Assert.AreEqual(3, s.ShownLevel);
            Assert.AreEqual(1, s.GradeUpFx.PlayCount);
            Assert.AreEqual(0.3f, s.GaugeValue, 1e-3f);
        }

        [Test]
        public void SkippedLaps_SnapLevelWithoutFx()
        {
            // AnimatedGauge는 꺼질 때(Mid 숨김) 끝 상태로 정리하며 남은 바퀴를 건너뛴다. 보이는 레벨은 연출 없이 시뮬에 맞춘다.
            var s = NewStair();
            s.SetGauge(T, 2, 0.2f);
            s.SetGauge(T, 3, 0.4f);
            s.Gauge.Complete();
            s.Tick(0f);
            Assert.AreEqual(3, s.ShownLevel);
            Assert.AreEqual(0, s.GradeUpFx.PlayCount);
            Assert.AreEqual(0.4f, s.GaugeValue, 1e-3f);
        }

        [Test]
        public void HudFrames_LevelUpWhileStepping_PlaysGradeUpFx_AndKeepsIt()
        {
            // MV-UI3-STAIRFX4: 실제 런 HUD 흐름(UIHud_Run → IngameHudView.SetState → TickRun 매 프레임, AnimatedGauge는 자기 Update로 돈다)에서
            // 따라 오르는 게이지가 레벨을 넘으면 Slot_Stair 등급 업 연출이 켜지고, 뒤 프레임들이 끄지 않아야 한다.
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RunHudPath));
            _made.Add(go);
            var view = go.GetComponentInChildren<IngameHudView>(true);
            var stair = view.Stair;
            Assert.That(stair.Gauge, Is.Not.Null, "UIHud_Run 안 계단 AnimatedGauge");
            var run = new MV.Run.RunState(null, T, new MV.Run.Pcg32Rng(1)) { Bal = MV.Run.RunLevelBuilder.Preset(T, null) }; // 실제 런은 항상 프리셋이 있다
            run.GaugeLv = 2; // 런 시작 값 = 첫 프레임 레벨(f1c3c335 이후 게이지 기준은 런 시작 값)
            view.BindRun(() => run);
            var fx = stair.GradeUpFx;
            var scaleRest = fx.transform.localScale;

            void Frame(int lv, float fill)
            {
                const float dt = 1f / 60f;
                var st = new RunHudState { GaugeLv = lv, GaugeFill = fill, TimeScale = 1 };
                view.SetState(st);
                stair.Gauge.Tick(dt); // 실행 중에는 AnimatedGauge.Update
                view.TickRun(dt);
            }

            Frame(2, 0.2f);
            Frame(2, 0.2f);
            Frame(2, 0.8f);
            for (int i = 0; i < 30; i++) Frame(2, 0.8f);
            Assert.AreEqual(0, fx.PlayCount, "같은 레벨은 연출 없음");
            Frame(3, 0.1f); // 마지막 오브 도착 몫(레벨 업)
            for (int i = 0; i < 60 && stair.ShownLevel < 3; i++) Frame(3, 0.1f);
            Assert.AreEqual(3, stair.ShownLevel, "따라 올라 레벨을 넘었다");
            Assert.AreEqual(1, fx.PlayCount, "레벨을 넘은 프레임에 등급 업 연출");
            for (int i = 0; i < 3; i++) Frame(3, 0.1f);
            Assert.That(fx.Playing, "뒤 프레임이 연출을 끄지 않는다");
            Assert.That(fx.transform.localScale.x, Is.GreaterThan(scaleRest.x), "크기 튐이 남아 있다(EditMode는 트윈이 돌지 않는다)");
        }

        [Test]
        public void HudFrames_FirstChangeIsLevelUp_StillLapsAndPlaysFx()
        {
            // MV-UI3-STAIRFX5(사용자 2026-11-21 로그: 「최초에는 OnLap가 호출이 안되었어 … 한대 치면 바로 레벨업인 상태일때 안오른 느낌」):
            // 런 첫 프레임은 SetState(Presenter.Tick)가 TickRun의 새 런 정리(ResetRun)보다 먼저다. 정리 뒤 기준을 런 시작 값(유저 정보로 만든 RunState)으로 잡아,
            // 런의 첫 값 변경이 레벨 업이면 바로 맞추지 않고 바퀴 → OnLap → 등급 업 연출로 가야 한다.
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RunHudPath));
            _made.Add(go);
            var view = go.GetComponentInChildren<IngameHudView>(true);
            var stair = view.Stair;
            var run = new MV.Run.RunState(null, T, new MV.Run.Pcg32Rng(1)) { Bal = MV.Run.RunLevelBuilder.Preset(T, null) }; // 실제 런은 항상 프리셋이 있다
            run.GaugeLv = 2; // 런 시작 값(유저 정보 → RunLevelBuilder kegStart)
            run.GaugeFill = 0;
            view.BindRun(() => run);
            var fx = stair.GradeUpFx;
            int laps = 0;
            stair.Gauge.OnLapCompleted.AddListener(() => laps++);

            const float dt = 1f / 60f;
            view.SetState(new RunHudState { GaugeLv = 2, GaugeFill = 0f, TimeScale = 1 }); // 런 첫 그리기
            view.TickRun(dt); // 새 런 정리
            Assert.AreEqual(2, stair.ShownLevel, "새 런 정리 뒤 기준은 런 시작 값");
            Assert.AreEqual(0f, stair.GaugeValue, 1e-3f);

            view.SetState(new RunHudState { GaugeLv = 3, GaugeFill = 0.1f, TimeScale = 1 }); // 첫 타격 = 레벨 업
            Assert.AreEqual(2, stair.ShownLevel, "바로 맞추지 않고 바퀴로 따라 오른다");
            for (int i = 0; i < 120 && stair.ShownLevel < 3; i++)
            {
                stair.Gauge.Tick(dt);
                view.TickRun(dt);
            }
            Assert.AreEqual(3, stair.ShownLevel);
            Assert.AreEqual(1, laps, "OnLap 1번");
            Assert.AreEqual(1, fx.PlayCount, "첫 타격 레벨 업에도 등급 업 연출");
        }

        [Test]
        public void ResetRun_SnapsToSimWithoutFx()
        {
            var s = NewStair();
            s.SetGauge(T, 2, 0.2f);
            s.ResetRun();
            s.SetGauge(T, 5, 0.1f);
            Assert.AreEqual(5, s.ShownLevel, "새 런은 바로 시뮬 값");
            Assert.AreEqual(0.1f, s.GaugeValue, 1e-3f);
            Assert.AreEqual(0, s.GradeUpFx.PlayCount);
        }
    }
}
