using MV.Data;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// `GameCamera` 리그(02 §2.2.2 `CameraRig.LateUpdate`). 실시간 dt(상한 `time.dtCap`, 히트스톱 무관)로
    /// camLift·ck 갱신 → 위치 보간 → 흔들림 가산 → LookAt. 빌보드 동기화·깊이 정렬은 <see cref="DepthSorter"/>가 이 뒤에 한다.
    /// 값은 씬에서 참조하는 <see cref="MvCameraSettings"/> 에셋 하나에서 읽는다(Play Mode·빌드 같음). 런타임은 에셋 행의 사본을 쓰고,
    /// Play 중 Inspector로 에셋을 고치면 새 사본으로 바로 바꾼다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] MvCameraSettings _settings;

        Camera _cam;
        CameraModel _model;
        readonly CameraShake _shake = new CameraShake();
        IWorldViewSource _source;
        double _dtCap = 0.05;
        MvCameraSettings _listening;

        public MvCameraSettings Settings => _settings;
        public CameraModel Model => _model;
        public Camera Cam => _cam != null ? _cam : (_cam = GetComponent<Camera>());
        public bool Ready => _model != null && _source != null;

        /// <summary>표를 읽은 뒤 한 번 부른다. 로비 자세로 바로 선다(02 §2.2.2 로비 → 런 스윕).</summary>
        public void Init(MvTables tables, IWorldViewSource source)
        {
            _model = CameraModel.FromSettings(_settings);
            _source = source;
            _dtCap = tables.RunConfig.D(RunConfigKeys.Time_DtCap);
            ApplyLens(_model);
            var lobby = _model.LobbyRow;
            _model.Snap(new Vector3d(0, lobby.posY, lobby.posZ), new Vector3d(0, lobby.lookY, lobby.lookZ));
            Apply(_model);
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

        void OnEnable() => Listen(_settings);

        void OnDisable() => Listen(null);

        void Listen(MvCameraSettings s)
        {
            if (_listening == s) return;
            if (_listening != null) _listening.Changed -= OnSettingsChanged;
            _listening = s;
            if (_listening != null) _listening.Changed += OnSettingsChanged;
        }

        /// <summary>Play 중 Inspector에서 설정 에셋을 바꿨을 때: 새 값의 사본으로 바꾼다(위치·보간 상태 유지).</summary>
        void OnSettingsChanged()
        {
            if (this == null || _model == null || _settings == null) return;
            var next = CameraModel.FromSettings(_settings);
            next.Snap(_model.Pos, _model.Look, _model.CamLift, _model.Ck);
            _model = next;
            ApplyLens(_model);
        }

        void LateUpdate()
        {
            if (!Ready) return;
            double dt = System.Math.Min(_dtCap, Time.unscaledDeltaTime);
            // 표시 전용 난수(UnityEngine.Random). MV.Run 결정적 난수와 섞지 않는다.
            var shake = _shake.Step(dt, UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
            _model.Step(_source.Camera, dt, shake);
            Apply(_model);
        }

        void ApplyLens(CameraModel m)
        {
            var cam = Cam;
            var run = m.RunRow;
            cam.orthographic = false;
            cam.nearClipPlane = (float)run.near;
            cam.farClipPlane = (float)run.far;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        void Apply(CameraModel m)
        {
            var cam = Cam;
            var t = transform;
            t.position = m.Pos.ToVector3();
            t.LookAt(m.Look.ToVector3(), Vector3.up);
            float aspect = cam.pixelHeight > 0 ? cam.pixelWidth / (float)cam.pixelHeight : (float)m.RefAspect;
            cam.fieldOfView = (float)m.VerticalFov(m.RunRow.fovV, aspect);
        }
    }
}
