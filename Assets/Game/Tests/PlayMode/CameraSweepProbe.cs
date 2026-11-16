using System;
using System.Collections;
using System.Reflection;
using MV.Data;
using MV.View;
using NUnit.Framework;
using UnityEngine;

namespace MV.Tests
{
    /// <summary>
    /// 설정 에셋 Play Mode 확인(MV-SCENE-VERIFY·MV-SETTINGS-LIVE): 실제 Game 씬에서
    /// ① 로비 자세 ② 로비 → 런 보간(02 §2.2.2: 런 첫 프레임부터 dt·posLerp 보간) ③ Play 중 카메라 설정 변경 반영
    /// ④ Play 중 캐릭터 이동 속도 변경 반영 ⑤ 게임이 에셋 값을 바꾸지 않음을 본다.
    /// 에셋 값은 Inspector 수정과 같은 알림(`OnValidate`)으로 바꾸고 끝에 되돌린다(저장하지 않음).
    /// </summary>
    internal sealed class CameraSweepProbe
    {
        const double SweepSeconds = 1.5, SettleSeconds = 1.0, ChangeY = 2.0, SpeedMul = 2.0, SpeedWindow = 0.5;

        readonly CameraRig _rig;
        readonly MvCameraSettings _settings;
        readonly MvCharacterSettings _character;
        readonly string _original, _characterOriginal;

        public CameraSweepProbe()
        {
            _rig = UnityEngine.Object.FindFirstObjectByType<CameraRig>();
            Assert.IsNotNull(_rig, "Game 씬에 CameraRig가 없다");
            _settings = _rig.Settings;
            Assert.IsNotNull(_settings, "GameCamera가 설정 에셋을 참조하지 않는다");
            _character = MvCharacterSettings.Load();
            _original = JsonUtility.ToJson(_settings);
            _characterOriginal = JsonUtility.ToJson(_character);
            Assert.AreNotSame(_settings.run, _rig.Model.RunRow, "런타임 모델이 에셋 행을 그대로 쓴다(사본이 아님)");
        }

        double LobbyY => _settings.lobby.posY;
        double RunY => _settings.run.posY;
        double Progress(float y) => (y - LobbyY) / (RunY - LobbyY);
        /// <summary>리그가 표(`time.dtCap`)에서 읽어 둔 카메라 dt 상한.</summary>
        double DtCap => (double)typeof(CameraRig).GetField("_dtCap", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_rig);

        public void CheckLobby()
        {
            var l = _settings.lobby;
            var p = _rig.transform.position;
            Assert.Less(Vector3.Distance(new Vector3(0, (float)l.posY, (float)l.posZ), p), 1e-3f, $"로비 카메라가 lobby 자세가 아니다: {p:F3}");
        }

        /// <summary>출발 직후부터 카메라 높이를 본다. 카메라 dt는 리그와 같게 `min(dtCap, unscaledDeltaTime)`로 센다.</summary>
        public IEnumerator RecordSweep(Func<bool> running)
        {
            double dtCap = DtCap;
            double camT = 0, wall = 0;
            float prevY = _rig.transform.position.y;
            bool firstChecked = false;
            double progressAt03 = double.NaN;
            while (wall < SweepSeconds * 4 && camT < SweepSeconds && running())
            {
                yield return null;
                camT += Math.Min(dtCap, Time.unscaledDeltaTime);
                wall += Time.unscaledDeltaTime;
                float y = _rig.transform.position.y;
                if (!firstChecked)
                {
                    firstChecked = true;
                    Assert.Less(Progress(y), 0.75, $"런 첫 프레임에 카메라가 런 자세로 순간 이동했다(보간 없음): y {y:F3}");
                }
                Assert.GreaterOrEqual(y, prevY - 0.05f, $"로비 → 런 보간 중 카메라 높이가 되돌아갔다({prevY:F3} → {y:F3})");
                prevY = y;
                if (double.IsNaN(progressAt03) && camT >= 0.3) progressAt03 = Progress(y);
            }
            Assert.IsTrue(running(), "보간을 보는 동안 런이 끝났다");
            Assert.IsFalse(double.IsNaN(progressAt03), $"카메라 시간 0.3초를 채우지 못했다({camT:F2}초)");
            Assert.GreaterOrEqual(progressAt03, 0.9, $"카메라 0.3초에 로비 → 런 높이의 {progressAt03:P0}만 올라감(정본 약 95%)");
            Assert.GreaterOrEqual(Progress(_rig.transform.position.y), 0.99, "1.5초 뒤에도 런 자세에 닿지 않았다");
            Debug.Log($"[CameraSweepProbe] 로비→런 보간 0.3초 {progressAt03:P1}");
            Assert.AreEqual(_original, JsonUtility.ToJson(_settings), "Play 중 게임이 카메라 설정 에셋 값을 바꿨다");
            Assert.AreEqual(_characterOriginal, JsonUtility.ToJson(_character), "Play 중 게임이 캐릭터 설정 에셋 값을 바꿨다");
        }

        /// <summary>Play 중 Inspector 수정과 같은 알림으로 run.posY를 올렸다가 되돌린다.</summary>
        public IEnumerator CheckCameraChange(Func<bool> running)
        {
            float before = _rig.transform.position.y;
            _settings.run.posY += ChangeY;
            RaiseValidate(_settings);
            yield return Wait(SettleSeconds);
            float raised = _rig.transform.position.y;
            JsonUtility.FromJsonOverwrite(_original, _settings);
            RaiseValidate(_settings);
            yield return Wait(SettleSeconds);
            float back = _rig.transform.position.y;
            Debug.Log($"[CameraSweepProbe] 카메라 run.posY 변경: y {before:F3} → {raised:F3} → {back:F3}");
            Assert.IsTrue(running(), "카메라 설정 변경을 보는 동안 런이 끝났다");
            Assert.AreEqual(before + ChangeY, raised, 0.3, "Play 중 카메라 설정 변경이 카메라에 반영되지 않았다");
            Assert.AreEqual(before, back, 0.3, "되돌린 카메라 설정으로 돌아오지 않았다");
        }

        /// <summary>
        /// Play 중 Inspector 수정과 같은 알림으로 전진 속도(runSpeed·runSpeedMid)를 2배로 → 실행 중 런의 전진 속도(카메라 z 이동량)가 따라온다.
        /// </summary>
        public IEnumerator CheckCharacterChange(Func<bool> running)
        {
            var tables = MvTables.Current;
            Assert.IsNotNull(tables, "MvTables.Current가 없다");
            double baseSpeed = tables.Character.RunSpeed;
            double before = 0;
            yield return Advance(d => before = d);
            _character.runSpeed *= SpeedMul;
            _character.runSpeedMid *= SpeedMul;
            RaiseValidate(_character);
            Assert.AreEqual(baseSpeed * SpeedMul, tables.Character.RunSpeed, 1e-9, "에셋 변경이 MvTables 사본에 반영되지 않았다");
            yield return null;
            double after = 0;
            yield return Advance(d => after = d);
            JsonUtility.FromJsonOverwrite(_characterOriginal, _character);
            RaiseValidate(_character);
            Assert.AreEqual(baseSpeed, tables.Character.RunSpeed, 1e-9, "되돌린 값이 MvTables 사본에 반영되지 않았다");
            Debug.Log($"[CameraSweepProbe] 전진 속도 ×{SpeedMul}: {SpeedWindow}초 이동 {before:F3} m → {after:F3} m");
            Assert.IsTrue(running(), "이동 속도 변경을 보는 동안 런이 끝났다");
            Assert.Greater(before, 0.25 * baseSpeed * SpeedWindow, $"변경 전 전진이 너무 적어 비교할 수 없다({before:F3} m)");
            Assert.Greater(after / before, 1.6, $"Play 중 이동 속도 변경이 실행 중 런에 반영되지 않았다({before:F3} → {after:F3} m)");
        }

        /// <summary>실시간 <see cref="SpeedWindow"/>초 동안 카메라 z 이동량(런 dt = 실시간 dt).</summary>
        IEnumerator Advance(Action<double> done)
        {
            float z0 = _rig.transform.position.z;
            double t = 0;
            while (t < SpeedWindow) { yield return null; t += Time.unscaledDeltaTime; }
            done((_rig.transform.position.z - z0) * (SpeedWindow / t));
        }

        public void Finish()
        {
            JsonUtility.FromJsonOverwrite(_original, _settings);
            RaiseValidate(_settings);
            JsonUtility.FromJsonOverwrite(_characterOriginal, _character);
            RaiseValidate(_character);
        }

        static IEnumerator Wait(double seconds)
        {
            double t = 0;
            while (t < seconds) { yield return null; t += Time.unscaledDeltaTime; }
        }

        /// <summary>Inspector가 값을 바꿀 때 Unity가 부르는 것과 같은 `OnValidate`(Play 시험 어셈블리는 UnityEditor를 쓰지 않는다).</summary>
        static void RaiseValidate(ScriptableObject so) =>
            so.GetType().GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(so, null);
    }
}
