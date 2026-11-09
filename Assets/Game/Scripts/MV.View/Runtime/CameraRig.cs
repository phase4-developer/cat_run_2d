using MV.Data;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// `GameCamera` 리그(02 §2.2.2 `CameraRig.LateUpdate`). 실시간 dt(상한 `time.dtCap`, 히트스톱 무관)로
    /// camLift·ck 갱신 → 위치 보간 → 흔들림 가산 → LookAt. 빌보드 동기화·깊이 정렬은 <see cref="DepthSorter"/>가 이 뒤에 한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        Camera _cam;
        CameraModel _model;
        readonly CameraShake _shake = new CameraShake();
        IWorldViewSource _source;
        double _dtCap = 0.05;
        double _baseFov = 52;

        public CameraModel Model => _model;
        public Camera Cam => _cam != null ? _cam : (_cam = GetComponent<Camera>());
        public bool Ready => _model != null && _source != null;

        /// <summary>표를 읽은 뒤 한 번 부른다. 로비 자세로 바로 선다(02 §2.2.2 로비 → 런 스윕).</summary>
        public void Init(MvTables tables, IWorldViewSource source)
        {
            _model = CameraModel.FromTables(tables);
            _source = source;
            _dtCap = tables.RunConfig.D(RunConfigKeys.Time_DtCap);
            var run = _model.RunRow;
            _baseFov = run.fovV;
            var cam = Cam;
            cam.orthographic = false;
            cam.nearClipPlane = (float)run.near;
            cam.farClipPlane = (float)run.far;
            cam.clearFlags = CameraClearFlags.SolidColor;
            var lobby = _model.LobbyRow;
            _model.Snap(new Vector3d(0, lobby.posY, lobby.posZ), new Vector3d(0, lobby.lookY, lobby.lookZ));
            Apply();
        }

        public void SetSource(IWorldViewSource source) => _source = source;

        /// <summary>테마 `fog` = 배경색(02 §2.1.4, 하늘 없음 D-44).</summary>
        public void SetTheme(ThemeData theme)
        {
            if (theme != null && MvColor.TryColor32(theme.fog, out var c)) Cam.backgroundColor = c;
        }

        /// <summary>정본 `shake(a, t)`: 최댓값 병합(02 §2.2.4).</summary>
        public void Shake(double amp, double time) => _shake.Request(amp, time);

        public void ClearShake() => _shake.Clear();

        void LateUpdate()
        {
            if (!Ready) return;
            double dt = System.Math.Min(_dtCap, Time.unscaledDeltaTime);
            // 표시 전용 난수(UnityEngine.Random). MV.Run 결정적 난수와 섞지 않는다.
            var shake = _shake.Step(dt, UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
            _model.Step(_source.Camera, dt, shake);
            Apply();
        }

        void Apply()
        {
            var cam = Cam;
            var t = transform;
            t.position = _model.Pos.ToVector3();
            t.LookAt(_model.Look.ToVector3(), Vector3.up);
            float aspect = cam.pixelHeight > 0 ? cam.pixelWidth / (float)cam.pixelHeight : 9f / 16f;
            cam.fieldOfView = (float)CameraModel.VerticalFov(_baseFov, aspect);
        }
    }
}
