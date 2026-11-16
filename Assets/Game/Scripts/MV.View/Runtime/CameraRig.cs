using MV.Data;
using UnityEngine;

namespace MV.View
{
    /// <summary>편집 모드에서 보여 줄 카메라 시점.</summary>
    public enum CameraPreview { Lobby, Run, Climb, ClimbSide }

    /// <summary>
    /// `GameCamera` 리그(02 §2.2.2 `CameraRig.LateUpdate`). 실시간 dt(상한 `time.dtCap`, 히트스톱 무관)로
    /// camLift·ck 갱신 → 위치 보간 → 흔들림 가산 → LookAt. 빌보드 동기화·깊이 정렬은 <see cref="DepthSorter"/>가 이 뒤에 한다.
    /// 값은 씬에서 참조하는 <see cref="MvCameraSettings"/> 에셋 하나에서 읽는다(편집 모드·Play Mode·빌드 같음).
    /// 편집 모드에서는 <see cref="_preview"/> 시점의 다 따라간 자세로 서서 Scene·Game 뷰에서 구도를 바로 본다.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] MvCameraSettings _settings;

        [Header("편집 모드 미리보기(Play에는 쓰지 않음)")]
        [Tooltip("편집 모드에서 카메라가 설 시점")]
        [SerializeField] CameraPreview _preview = CameraPreview.Lobby;
        [Tooltip("기준 대상(플레이어 자리). 비우면 월드 원점. 높이(y)는 지면 높이로 본다")]
        [SerializeField] Transform _previewTarget;

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

        void OnEnable()
        {
            Listen(_settings);
            if (!Application.isPlaying) ApplyPreview();
        }

        void OnDisable() => Listen(null);

        void OnValidate()
        {
            Listen(isActiveAndEnabled ? _settings : null);
            if (!Application.isPlaying) QueuePreview();
        }

        void Listen(MvCameraSettings s)
        {
            if (_listening == s) return;
            if (_listening != null) _listening.Changed -= OnSettingsChanged;
            _listening = s;
            if (_listening != null) _listening.Changed += OnSettingsChanged;
        }

        /// <summary>
        /// Inspector에서 설정을 바꿨을 때. 편집 모드는 미리보기 구도를 다시 세우고, Play Mode는 새 값의 사본으로 바꾼다(위치·보간 상태 유지).
        /// </summary>
        void OnSettingsChanged()
        {
            if (this == null) return;
            if (!Application.isPlaying) { QueuePreview(); return; }
            if (_model == null || _settings == null) return;
            var next = CameraModel.FromSettings(_settings);
            next.Snap(_model.Pos, _model.Look, _model.CamLift, _model.Ck);
            _model = next;
            ApplyLens(_model);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                if (_previewTarget != null && _previewTarget.hasChanged) { _previewTarget.hasChanged = false; ApplyPreview(); }
                return;
            }
            if (!Ready) return;
            double dt = System.Math.Min(_dtCap, Time.unscaledDeltaTime);
            // 표시 전용 난수(UnityEngine.Random). MV.Run 결정적 난수와 섞지 않는다.
            var shake = _shake.Step(dt, UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
            _model.Step(_source.Camera, dt, shake);
            Apply(_model);
        }

        /// <summary>
        /// 편집 모드 구도: 고른 시점의 다 따라간 자세(보간·돌기 끝)로 선다. 같은 <see cref="CameraModel"/> 식을 쓰므로 Play와 같다.
        /// </summary>
        void ApplyPreview()
        {
            if (_settings == null || Application.isPlaying) return;
            CameraModel m;
            try { m = CameraModel.FromSettings(_settings, _preview == CameraPreview.ClimbSide); }
            catch (System.Exception e) { Debug.LogWarning("[CameraRig] 미리보기 실패: " + e.Message, this); return; }
            var p = _previewTarget != null ? _previewTarget.position : Vector3.zero;
            var mode = _preview == CameraPreview.Lobby ? WorldViewMode.Lobby
                : _preview == CameraPreview.Run ? WorldViewMode.Run : WorldViewMode.Climb;
            var input = new CameraInput { Mode = mode, Px = p.x, Pz = p.z, Wy = p.y };
            m.Snap(new Vector3d(0, m.LobbyRow.posY, m.LobbyRow.posZ), new Vector3d(0, m.LobbyRow.lookY, m.LobbyRow.lookZ));
            // 큰 dt 한 번이면 보간·높이·섞기·옆 돌기 계수가 모두 1로 잘려 목표 자세에 선다.
            m.Step(input, PreviewSettleDt, default);
            ApplyLens(m);
            Apply(m);
        }

        const double PreviewSettleDt = 1e3;

        /// <summary>OnValidate 안에서는 Transform을 바꾸지 않고 다음 에디터 틱에 세운다.</summary>
        void QueuePreview()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) ApplyPreview(); };
#endif
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
