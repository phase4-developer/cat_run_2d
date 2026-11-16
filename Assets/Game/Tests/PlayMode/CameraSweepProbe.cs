using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using MV.Data;
using MV.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace MV.Tests
{
    /// <summary>
    /// 시각 검수(MV-SCENE-VERIFY) Play Mode 쪽: 실제 Game 씬 `GameCamera`로
    /// ① 로비 자세 ② 로비 → 런 보간(02 §2.2.2: 런 첫 프레임부터 dt·posLerp 보간, 미리 옮기지 않음) ③ 설정 에셋 변경 반영을 보고,
    /// 9:16 스크린샷과 자세 기록을 `Logs/CameraShots`(git 제외)에 남긴다. 에셋 값은 메모리에서만 바꾸고 되돌린다.
    /// </summary>
    internal sealed class CameraSweepProbe
    {
        const int ShotW = 540, ShotH = 960;
        const double SweepSeconds = 1.5, SettleSeconds = 1.0, ChangeY = 2.0;

        readonly CameraRig _rig;
        readonly MvCameraSettings _settings;
        readonly string _original;
        readonly string _dir;
        readonly StringBuilder _log = new StringBuilder();

        public CameraSweepProbe()
        {
            _rig = UnityEngine.Object.FindFirstObjectByType<CameraRig>();
            Assert.IsNotNull(_rig, "Game 씬에 CameraRig가 없다");
            _settings = _rig.Settings;
            Assert.IsNotNull(_settings, "GameCamera가 설정 에셋을 참조하지 않는다");
            _original = JsonUtility.ToJson(_settings);
            Assert.AreNotSame(_settings.run, _rig.Model.RunRow, "런타임 모델이 에셋 행을 그대로 쓴다(사본이 아님)");
            _dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "CameraShots");
            Directory.CreateDirectory(_dir);
            _log.Append("phase\tt\tcamDt\tpos\teuler\tprogressY\n");
        }

        double LobbyY => _settings.lobby.posY;
        double RunY => _settings.run.posY;
        /// <summary>리그가 표(`time.dtCap`)에서 읽어 둔 카메라 dt 상한.</summary>
        double DtCap => (double)typeof(CameraRig).GetField("_dtCap", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_rig);
        double Progress(float y) => (y - LobbyY) / (RunY - LobbyY);

        public void CaptureLobby()
        {
            var l = _settings.lobby;
            var p = _rig.transform.position;
            Assert.Less(Vector3.Distance(new Vector3(0, (float)l.posY, (float)l.posZ), p), 1e-3f, $"로비 카메라가 lobby 자세가 아니다: {p:F3}");
            Row("lobby", 0, 0);
            Shot("play_0_Lobby.png");
        }

        /// <summary>출발 직후부터 실시간으로 카메라 높이를 기록한다. 카메라 dt는 리그와 같게 `min(dtCap, unscaledDeltaTime)`로 센다.</summary>
        public IEnumerator RecordSweep(Func<bool> running)
        {
            double dtCap = DtCap;
            double camT = 0, wall = 0;
            float prevY = _rig.transform.position.y;
            bool shotMid = false, firstChecked = false;
            double progressAt03 = double.NaN;
            while (wall < SweepSeconds * 4 && camT < SweepSeconds && running())
            {
                yield return null;
                double dt = Math.Min(dtCap, Time.unscaledDeltaTime);
                camT += dt;
                wall += Time.unscaledDeltaTime;
                float y = _rig.transform.position.y;
                if (!firstChecked)
                {
                    firstChecked = true;
                    Assert.Less(Progress(y), 0.75, $"런 첫 프레임에 카메라가 런 자세로 순간 이동했다(보간 없음): y {y:F3}");
                    Shot("play_1_RunFirstFrame.png");
                }
                Assert.GreaterOrEqual(y, prevY - 0.05f, $"로비 → 런 보간 중 카메라 높이가 되돌아갔다({prevY:F3} → {y:F3})");
                prevY = y;
                Row("sweep", camT, dt);
                if (!shotMid && camT >= 0.1) { shotMid = true; Shot("play_2_Sweep_0.1s.png"); }
                if (double.IsNaN(progressAt03) && camT >= 0.3) progressAt03 = Progress(y);
            }
            Assert.IsTrue(running(), "보간을 보는 동안 런이 끝났다");
            Assert.IsFalse(double.IsNaN(progressAt03), $"카메라 시간 0.3초를 채우지 못했다({camT:F2}초)");
            Assert.GreaterOrEqual(progressAt03, 0.9, $"카메라 0.3초에 로비 → 런 높이의 {progressAt03:P0}만 올라감(정본 약 95%)");
            Assert.GreaterOrEqual(Progress(_rig.transform.position.y), 0.99, "1.5초 뒤에도 런 자세에 닿지 않았다");
            _log.Append("progressAt0.3s\t").Append(progressAt03.ToString("F3")).Append('\n');
            Shot("play_3_Run.png");
            Assert.AreEqual(_original, JsonUtility.ToJson(_settings), "Play 중 게임이 설정 에셋 값을 바꿨다");
        }

        /// <summary>Play 중 Inspector 수정과 같은 길(`OnValidate`)로 run.posY를 올렸다가 되돌린다.</summary>
        public IEnumerator CheckSettingsChange(Func<bool> running)
        {
            float before = _rig.transform.position.y;
            _settings.run.posY += ChangeY;
            RaiseValidate();
            yield return Wait(SettleSeconds);
            float raised = _rig.transform.position.y;
            Row("run.posY+" + ChangeY, 0, 0);
            Shot("play_4_Run_posY+2.png");
            JsonUtility.FromJsonOverwrite(_original, _settings);
            RaiseValidate();
            yield return Wait(SettleSeconds);
            float back = _rig.transform.position.y;
            Row("restored", 0, 0);
            _log.Append("settingsChange\t").Append(before.ToString("F3")).Append(" -> ").Append(raised.ToString("F3"))
                .Append(" -> ").Append(back.ToString("F3")).Append('\n');
            Assert.IsTrue(running(), "설정 변경을 보는 동안 런이 끝났다");
            Assert.AreEqual(before + ChangeY, raised, 0.3, "Play 중 설정 에셋 변경이 카메라에 반영되지 않았다");
            Assert.AreEqual(before, back, 0.3, "되돌린 설정으로 카메라가 돌아오지 않았다");
        }

        public void Finish()
        {
            JsonUtility.FromJsonOverwrite(_original, _settings);
            RaiseValidate();
            File.WriteAllText(Path.Combine(_dir, "play_poses.tsv"), _log.ToString(), new UTF8Encoding(false));
            Debug.Log("[CameraSweepProbe]\n" + _log);
        }

        static IEnumerator Wait(double seconds)
        {
            double t = 0;
            while (t < seconds) { yield return null; t += Time.unscaledDeltaTime; }
        }

        void RaiseValidate() =>
            typeof(MvCameraSettings).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(_settings, null);

        void Row(string phase, double t, double dt)
        {
            var tr = _rig.transform;
            _log.Append(phase).Append('\t').Append(t.ToString("F3")).Append('\t').Append(dt.ToString("F4")).Append('\t')
                .Append(tr.position.ToString("F3")).Append('\t').Append(tr.eulerAngles.ToString("F2")).Append('\t')
                .Append(Progress(tr.position.y).ToString("F3")).Append('\n');
        }

        /// <summary>월드 카메라만 9:16 RenderTexture에 그린다(UI 오버레이 제외). FOV는 그 화면비로 잠시 맞췄다 되돌린다.</summary>
        void Shot(string file)
        {
            var cam = _rig.Cam;
            var rt = RenderTexture.GetTemporary(ShotW, ShotH, 24);
            var prevTarget = cam.targetTexture;
            float prevFov = cam.fieldOfView;
            try
            {
                cam.targetTexture = rt;
                cam.fieldOfView = (float)_rig.Model.VerticalFov(_rig.Model.RunRow.fovV, ShotW / (double)ShotH);
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(_dir, file), tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                cam.fieldOfView = prevFov;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
