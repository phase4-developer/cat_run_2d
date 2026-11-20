using System.Collections.Generic;
using MV.Data;
using MV.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.UI.Tests
{
    /// <summary>
    /// MV-UI3-STAIRFX(노션 「IngameScreen : Stair 업그레이드 연출」): 계단 경험치 오브 도착에 맞춰 게이지가 나눠 오르고(값은 시뮬), 보이는 레벨이 오르면
    /// 계단 칸 등급 업 연출(크기·Flash·Sparkle). 로비 MainUpgrade로 계단 레벨이 오를 때도 같은 부품. 화면 모양은 사람 확인(NOT_RUN 별도).
    /// </summary>
    public sealed class StairFxTests
    {
        const string IngamePath = "Assets/Game/Art/UI/Prefab/UIScreen/IngameScreen.prefab";
        const string LobbyPath = "Assets/Game/Addressables/Local/UI/Lobby/UIScreen_Lobby.prefab";
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
            var so = new SerializedObject(s);
            so.FindProperty("gaugeStepSec").floatValue = 0; // 시험: 따라 오르는 시간 없이 바로
            so.ApplyModifiedPropertiesWithoutUndo();
            s.ResetRun();
            return s;
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
        public void LobbyStair_WiresSameGradeUpFx()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPath);
            var v = prefab.GetComponentInChildren<LobbyDesignView>(true);
            Assert.That(v.StairGradeUpFx, Is.Not.Null, "stairGradeUpFx");
            Assert.AreEqual("Slot_Stair", v.StairGradeUpFx.name);
            Assert.That(v.StairGradeUpFx.HasFlash && v.StairGradeUpFx.HasSparkle, "로비 Slot_Stair Flash·Effect_Sparkle 연결");
        }

        [Test]
        public void Gauge_FollowsSimDirectly_WhenNoOrbs()
        {
            var s = NewStair();
            s.SetGauge(T, 2, 0.25f);
            Assert.AreEqual(2, s.ShownLevel);
            Assert.AreEqual(0.25f, s.GaugeValue, 1e-3f);
            s.SetGauge(T, 2, 0.5f);
            Assert.AreEqual(0.5f, s.GaugeValue, 1e-3f);
            Assert.AreEqual(0, s.GradeUpFx.PlayCount, "첫 그리기·같은 레벨은 연출 없음");
        }

        [Test]
        public void Gauge_RisesPerOrbArrival_ThenGradeUpFxOnLevelUp()
        {
            var s = NewStair();
            s.SetGauge(T, 2, 0.2f);
            s.OrbsLaunched(2);
            s.SetGauge(T, 3, 0.4f); // 시뮬은 이미 받았다
            Assert.AreEqual(2, s.ShownLevel, "오브가 도착하기 전에는 그대로");
            Assert.AreEqual(0.2f, s.GaugeValue, 1e-3f);

            s.OrbArrived();
            s.SetGauge(T, 3, 0.4f);
            Assert.AreEqual(2, s.ShownLevel, "첫 오브: 남은 차이의 절반(2.2 → 2.8)");
            Assert.AreEqual(0.8f, s.GaugeValue, 1e-3f);
            Assert.AreEqual(0, s.GradeUpFx.PlayCount);

            s.OrbArrived();
            s.SetGauge(T, 3, 0.4f);
            Assert.AreEqual(3, s.ShownLevel, "마지막 오브: 시뮬 값");
            Assert.AreEqual(0.4f, s.GaugeValue, 1e-3f);
            Assert.AreEqual(1, s.GradeUpFx.PlayCount, "보이는 레벨이 오르면 등급 업 연출 1번");
            Assert.AreEqual("+3", s.StairValueText);
        }

        [Test]
        public void ResetRun_SnapsToSimWithoutFx()
        {
            var s = NewStair();
            s.SetGauge(T, 2, 0.2f);
            s.OrbsLaunched(3);
            s.ResetRun();
            s.SetGauge(T, 5, 0.1f);
            Assert.AreEqual(5, s.ShownLevel, "새 런은 바로 시뮬 값");
            Assert.AreEqual(0, s.GradeUpFx.PlayCount);
        }
    }
}
