using System;
using System.Collections.Generic;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 스테이지 맵 프리팹(`Stage{n}`, 주소 = 파일 이름) 루트. 게임 루프가 지역(area)에 맞는 프리팹을 불러와 <see cref="Bind"/>한다.
    /// 하는 일은 셋뿐이다.
    /// 1) Backdrop(하늘·원경)을 카메라 앞 먼 곳에 붙이고, 원경은 카메라 x를 따라 텍스처를 밀어 시차를 낸다.
    /// 2) Ground(바닥 판)를 카메라 z를 따라 <see cref="GroundSnap"/> 단위로 옮겨 끝없이 이어 보이게 한다(타일 반복 길이의 배수라 이음매가 안 보인다).
    /// 3) Deco(길가 장식)를 정해진 간격으로 재배치하며 <see cref="DecoSprites"/> 중 하나를 골라 그린다.
    /// 4) 계단 정산 옆 시점(<see cref="ClimbOcclusion"/>, 노션 계단연출 3절 가림 처리): 카메라 쪽(x &gt; 0) 장식을 흐리고,
    ///    오른쪽 바닥(<see cref="NearGround"/>)을 잘린 끝까지 줄이고, 카메라가 잘린 면 바깥이면 그 자리에 땅 단면(<see cref="SideCut"/>)을 켠다.
    /// </summary>
    [DefaultExecutionOrder(60)]   // CameraRig(−100) 뒤
    public sealed class StageMap : MonoBehaviour
    {
        [Header("하늘·원경 (카메라를 따라감)")]
        public Transform Backdrop;
        [Tooltip("카메라 앞 거리(m). 바닥 끝보다 멀어야 한다.")] public float BackdropDistance = 110f;
        public Renderer SkylineFar, SkylineNear;
        [Tooltip("원경 시차(웹 프로토: 먼 겹 0.5, 가까운 겹 1.0) — 카메라 x 1m당 텍스처 이동량")] public float FarParallax = 0.004f, NearParallax = 0.008f;

        [Header("바닥 (카메라 z를 따라감)")]
        public Transform Ground;
        [Tooltip("바닥 판을 옮기는 단위(m). 판석 4.4 · 잔디 4 · 연석 2의 공배수")] public float GroundSnap = 44f;
        [Tooltip("켜면 기존 지면 메쉬(GroundStripMesh)를 숨긴다")] public bool HideBuiltInGround = true;
        [Tooltip("켜면 기존 길가 소품(PropStreamer)을 숨긴다")] public bool HideBuiltInProps = false;
        [Tooltip("계단 정산 옆 시점에서 오른쪽 끝을 잘라 줄이는 카메라 쪽 바닥 판(왼쪽 끝은 그대로)")] public Transform NearGround;
        [Tooltip("계단 정산 옆 시점에서 잘린 끝에 세우는 땅 단면(꺼 둔 채 둔다)")] public Transform SideCut;

        [Header("길가 장식")]
        public SpriteRenderer DecoTemplate;
        public List<Sprite> DecoSprites = new List<Sprite>();
        [Tooltip("한쪽 장식 간격(m)")] public float DecoSpacing = 12.6f;
        [Tooltip("길 중심에서 장식까지 거리(m), 아래·위 범위에서 무작위")] public Vector2 DecoX = new Vector2(6.9f, 8.0f);
        [Tooltip("카메라 뒤·앞으로 장식을 둘 범위(m)")] public Vector2 DecoRange = new Vector2(-10f, 110f);

        Camera _cam;
        Func<float> _groundY;
        Func<double> _orbitE;
        float _nearLeft, _nearRight, _nearScaleX;
        Vector4 _nearST;
        Renderer _nearRenderer;
        MaterialPropertyBlock _nearMpb;
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        readonly List<SpriteRenderer> _decos = new List<SpriteRenderer>();
        Material _farMat, _nearMat;

        /// <summary>게임 루프가 부른다. groundY = 지금 지면 높이(계단을 오르면 올라간다). orbitE = 옆 시점 돌기 진행(<see cref="CameraModel.OrbitE"/>, 없으면 가림 처리 안 함).</summary>
        public void Bind(Camera cam, Func<float> groundY, Func<double> orbitE = null)
        {
            _cam = cam;
            _groundY = groundY;
            _orbitE = orbitE;
            if (NearGround != null)
            {
                // 판(Quad) 가로 = x 스케일. 처음 자리·폭을 기억해 두고 매번 그 값에서 다시 계산한다.
                _nearScaleX = NearGround.localScale.x;
                _nearLeft = NearGround.localPosition.x - _nearScaleX * 0.5f;
                _nearRight = NearGround.localPosition.x + _nearScaleX * 0.5f;
                _nearRenderer = NearGround.GetComponent<Renderer>();
                if (_nearRenderer != null && _nearRenderer.sharedMaterial != null && _nearRenderer.sharedMaterial.HasProperty(BaseMapST))
                    _nearST = _nearRenderer.sharedMaterial.GetVector(BaseMapST);
                else _nearRenderer = null;
            }
            if (SideCut != null) SideCut.gameObject.SetActive(false);
            if (SkylineFar != null) _farMat = SkylineFar.material;
            if (SkylineNear != null) _nearMat = SkylineNear.material;
            if (HideBuiltInGround) foreach (var g in FindObjectsByType<GroundStripMesh>(FindObjectsSortMode.None)) g.GetComponent<Renderer>().enabled = false;
            if (HideBuiltInProps) foreach (var p in FindObjectsByType<PropStreamer>(FindObjectsSortMode.None)) p.gameObject.SetActive(false);
            if (DecoTemplate != null) DecoTemplate.transform.parent.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            // 숨겼던 기존 지면을 되살린다(다른 스테이지 맵으로 바뀔 때)
            if (HideBuiltInGround) foreach (var g in FindObjectsByType<GroundStripMesh>(FindObjectsSortMode.None)) if (g) g.GetComponent<Renderer>().enabled = true;
            if (_farMat != null) Destroy(_farMat);
            if (_nearMat != null) Destroy(_nearMat);
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            var c = _cam.transform.position;
            float gy = _groundY != null ? _groundY() : 0f;

            if (Backdrop != null)
            {
                Backdrop.position = new Vector3(c.x, gy, c.z + Mathf.Min(BackdropDistance, _cam.farClipPlane * 0.9f));   // 카메라 먼 평면 안쪽
                if (_farMat != null) _farMat.mainTextureOffset = new Vector2(c.x * FarParallax, 0f);
                if (_nearMat != null) _nearMat.mainTextureOffset = new Vector2(c.x * NearParallax, 0f);
            }
            if (Ground != null) Ground.position = new Vector3(0f, gy, Mathf.Floor(c.z / GroundSnap) * GroundSnap);
            double e = _orbitE != null ? _orbitE() : 0;
            PlaceDecos(c.z, gy, e);
            CutNearGround(c.x, e);
        }

        /// <summary>오른쪽 바닥을 잘린 끝(<see cref="ClimbOcclusion.CutX"/>)까지 줄이고(무늬 밀도는 그대로), 카메라가 그 바깥이면 단면을 그 자리에 켠다.</summary>
        void CutNearGround(float camX, double e)
        {
            float right = Mathf.Min(_nearRight, (float)ClimbOcclusion.CutX(e));
            if (NearGround != null)
            {
                float w = Mathf.Max(0f, right - _nearLeft);
                var sc = NearGround.localScale;
                if (!Mathf.Approximately(sc.x, w))
                {
                    sc.x = w;
                    NearGround.localScale = sc;
                    var p = NearGround.localPosition; p.x = _nearLeft + w * 0.5f; NearGround.localPosition = p;
                    if (_nearRenderer != null)
                    {
                        _nearMpb ??= new MaterialPropertyBlock();
                        _nearRenderer.GetPropertyBlock(_nearMpb);
                        _nearMpb.SetVector(BaseMapST, new Vector4(_nearST.x * w / _nearScaleX, _nearST.y, _nearST.z, _nearST.w));
                        _nearRenderer.SetPropertyBlock(_nearMpb);
                    }
                }
                bool show = w > 0f;
                if (NearGround.gameObject.activeSelf != show) NearGround.gameObject.SetActive(show);
            }
            if (SideCut != null)
            {
                bool on = e > 0 && ClimbOcclusion.ShowSection(camX, e);
                if (on) { var p = SideCut.localPosition; p.x = right; SideCut.localPosition = p; }
                if (SideCut.gameObject.activeSelf != on) SideCut.gameObject.SetActive(on);
            }
        }

        void PlaceDecos(float camZ, float gy, double e)
        {
            if (DecoTemplate == null || DecoSprites.Count == 0) return;
            int k0 = Mathf.FloorToInt((camZ + DecoRange.x) / DecoSpacing), k1 = Mathf.FloorToInt((camZ + DecoRange.y) / DecoSpacing);
            int need = (k1 - k0 + 1) * 2;
            while (_decos.Count < need)
            {
                var root = Instantiate(DecoTemplate.transform.parent.gameObject, DecoTemplate.transform.parent.parent);
                root.SetActive(true);
                _decos.Add(root.GetComponentInChildren<SpriteRenderer>(true));
            }
            int n = 0;
            for (int k = k0; k <= k1; k++)
                for (int side = -1; side <= 1; side += 2)
                {
                    var sr = _decos[n++];
                    float h = Hash(k * 2.17f + side * 0.73f);
                    var sp = DecoSprites[Mathf.Min(DecoSprites.Count - 1, (int)(Hash(k * 1.3f + side) * DecoSprites.Count))];
                    if (sr.sprite != sp) { sr.sprite = sp; var ds = sr.GetComponentInParent<DepthSortable>(); if (ds != null) ds.Refresh(); }
                    float x = side * Mathf.Lerp(DecoX.x, DecoX.y, h);
                    sr.transform.parent.position = new Vector3(x, gy, k * DecoSpacing + h * 1.2f);
                    float a = ClimbOcclusion.NearAlpha(e, x);   // 카메라 쪽 장식은 옆 시점으로 돌수록 흐려진다
                    var fade = sr.GetComponentInParent<DepthSortable>();
                    if (fade != null) fade.SetAlphaScale(a);
                    bool on = a > 0.01f;
                    if (sr.transform.parent.gameObject.activeSelf != on) sr.transform.parent.gameObject.SetActive(on);
                }
            for (; n < _decos.Count; n++) if (_decos[n].transform.parent.gameObject.activeSelf) _decos[n].transform.parent.gameObject.SetActive(false);
        }

        static float Hash(float x) { float s = Mathf.Sin(x * 127.1f + 311.7f) * 43758.5453f; return s - Mathf.Floor(s); }
    }
}
