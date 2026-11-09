using MV.Data;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 테마 틴트 `Tint` 층(02 §2.5.3): `GameCamera` 앞 1 m 전체 화면 쿼드, `MV/ScreenMultiply`(Blend DstColor Zero).
    /// `304_theme.tint`가 비면 끈다. 쿼드 하나뿐이라 머티리얼 사본 1개를 쓴다(월드 스프라이트 배칭 규칙과 무관).
    /// </summary>
    [DefaultExecutionOrder(110)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TintLayer : MonoBehaviour
    {
        public const string SortingLayer = "Tint";
        static readonly int s_color = Shader.PropertyToID("_Color");

        [SerializeField] Camera _camera;
        [SerializeField] Material _material;
        Material _instance;
        Mesh _mesh;
        float _lastFov = -1f, _lastAspect = -1f;

        public void Init(Camera cam, Material shared)
        {
            _camera = cam;
            if (shared != null) _material = shared;
            transform.SetParent(cam.transform, false);
            transform.localPosition = new Vector3(0f, 0f, 1f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            if (_instance == null && _material != null) _instance = new Material(_material) { name = "MV_ScreenMultiply (Tint)" };
            var mr = GetComponent<MeshRenderer>();
            mr.sharedMaterial = _instance;
            mr.sortingLayerName = SortingLayer;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "TintQuad" };
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }
        }

        public void SetTheme(ThemeData theme)
        {
            bool on = theme != null && MvColor.TryColor32(theme.tint, out _);
            GetComponent<MeshRenderer>().enabled = on && _instance != null;
            if (!on || _instance == null) return;
            MvColor.TryColor32(theme.tint, out var c);
            _instance.SetColor(s_color, (Color)c);
        }

        void LateUpdate()
        {
            if (_camera == null || _mesh == null) return;
            float fov = _camera.fieldOfView, aspect = _camera.aspect;
            if (Mathf.Approximately(fov, _lastFov) && Mathf.Approximately(aspect, _lastAspect)) return;
            _lastFov = fov; _lastAspect = aspect;
            // 1 m 앞 화면을 조금 넘게 덮는다.
            float hh = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 1.02f, hw = hh * aspect;
            _mesh.Clear();
            _mesh.SetVertices(new[] { new Vector3(-hw, -hh, 0), new Vector3(hw, -hh, 0), new Vector3(hw, hh, 0), new Vector3(-hw, hh, 0) });
            _mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(hw * 2, hh * 2, 0.1f));
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_instance != null) Destroy(_instance);
        }
    }
}
