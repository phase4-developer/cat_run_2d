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
    /// 5) 두 층 지면(새 HTML `groundY`·`drawGround`): 절벽(정산 시작 shiftWorld의 `R.cliff`)이 있으면 z &lt; cliff.z는 아래층(low),
    ///    그 너머는 위층(high)에 깔고, 절벽 자리에 옹벽 면을 세운다. 아래층은 Ground를 한 번 복제해 쓴다(프리팹은 그대로).
    /// 6) Backdrop은 카메라가 보는 수평 방향(새 HTML `drawStreet` yaw) 앞에 두고 먼 땅 높이에 맞춘다. 원경 무늬는 yaw만큼 민다.
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
        Func<CliffInfo?> _cliff;
        GroundCopy _high, _low;
        Transform _wall;
        Renderer _wallRenderer;
        MaterialPropertyBlock _wallMpb;
        Vector4 _wallST;
        Vector2 _sideCutSize;
        Quaternion _backdropRot;
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        /// <summary>새 HTML drawGround 왼쪽 끝 x(−70)·옹벽 가로 시작.</summary>
        const float WallLeftX = -70f;
        /// <summary>새 HTML drawStreet: yawPx = −yaw·W·1.3, 원경 겹 이동 = yawPx·(0.7 + par·0.3), par = 먼 겹 0.5·가까운 겹 1.0.</summary>
        const float YawScreens = 1.3f, YawBase = 0.7f, YawPar = 0.3f, FarPar = 0.5f, NearPar = 1.0f;
        /// <summary>새 HTML 옆 시점 뒤쪽 확장: 땅 zN = PZ + 0.8 − 46·e(drawGround), 소품 z gt; PZ − 2 − 40·e(draw2D·streetProps).</summary>
        const float GroundBackE = 46f, DecoBackE = 40f;

        /// <summary>바닥 판 한 장: z 방향 길이 축과 원래 자리·무늬를 기억해 두고 매번 그 값에서 잘라 다시 계산한다.</summary>
        sealed class GroundPart
        {
            public Transform T;
            public Renderer R;
            public MaterialPropertyBlock Mpb;
            public bool HasST;
            public Vector4 ST;
            /// <summary>z 길이가 localScale.x(세운 단면)인지 .y(눕힌 판)인지. 무늬도 같은 축(u·v)이 z를 따라 는다.</summary>
            public bool ZOnX;
            public float Zc, Len;
            /// <summary>카메라 쪽 바닥(NearGround)이면 오른쪽 끝을 잘린 자리까지 줄인다(왼쪽 끝 고정).</summary>
            public bool Near;
            public float Left, Right, ScaleX;
        }

        sealed class GroundCopy
        {
            public Transform Root;
            public readonly List<GroundPart> Parts = new List<GroundPart>();
            public Transform SideCut;
            /// <summary>카메라 쪽 바닥 오른쪽 끝(단면은 잘린 끝과 이 값 중 작은 자리에 선다).</summary>
            public float NearRight = float.PositiveInfinity;
        }
        readonly List<SpriteRenderer> _decos = new List<SpriteRenderer>();
        Material _farMat, _nearMat;

        /// <summary>
        /// 게임 루프가 부른다. groundY = 지금 지면 높이(계단을 오르면 올라간다). orbitE = 옆 시점 돌기 진행(<see cref="CameraModel.OrbitE"/>, 없으면 가림 처리 안 함).
        /// cliff = 정산 절벽(없으면 null, 한 층).
        /// </summary>
        public void Bind(Camera cam, Func<float> groundY, Func<double> orbitE = null, Func<CliffInfo?> cliff = null)
        {
            _cam = cam;
            _groundY = groundY;
            _orbitE = orbitE;
            _cliff = cliff;
            if (Backdrop != null) _backdropRot = Backdrop.localRotation;
            if (SideCut != null) { _sideCutSize = new Vector2(SideCut.localScale.x, SideCut.localScale.y); SideCut.gameObject.SetActive(false); }
            if (Ground != null && _high == null)
            {
                _high = Capture(Ground, NearGround, SideCut);
                var lowGo = Instantiate(Ground.gameObject, Ground.parent);
                lowGo.name = Ground.name + "_Low";
                _low = Capture(lowGo.transform, Twin(Ground, NearGround, lowGo.transform), Twin(Ground, SideCut, lowGo.transform));
                lowGo.SetActive(false);
                if (SideCut != null)
                {
                    // 옹벽: 단면과 같은 종이 그림(새 HTML은 절벽 면을 단색으로 그린다). 크기·무늬는 매 프레임 맞춘다.
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    wall.name = "CliffWall";
                    var col = wall.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    wall.transform.SetParent(transform, false);
                    _wall = wall.transform;
                    _wallRenderer = wall.GetComponent<Renderer>();
                    var src = SideCut.GetComponent<Renderer>();
                    if (src != null) { _wallRenderer.sharedMaterial = src.sharedMaterial; _wallRenderer.shadowCastingMode = src.shadowCastingMode; }
                    var m = _wallRenderer.sharedMaterial;
                    _wallST = m != null && m.HasProperty(BaseMapST) ? m.GetVector(BaseMapST) : Vector4.zero;
                    wall.SetActive(false);
                }
            }
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
            var cl = _cliff != null ? _cliff() : null;
            double e = _orbitE != null ? _orbitE() : 0;
            // 새 HTML groundY: z < cliff.z면 아래층, 아니면 지형 높이(정산 시작부터 위층).
            float far = cl.HasValue ? (float)cl.Value.High : gy;
            float snapZ = Mathf.Floor((c.z - GroundBackE * (float)e) / GroundSnap) * GroundSnap;   // 옆 시점이면 카메라 뒤 땅까지
            float cutX = (float)ClimbOcclusion.CutX(e);
            bool section = e > 0 && ClimbOcclusion.ShowSection(c.x, e);

            if (Backdrop != null)
            {
                // 새 HTML drawStreet: 카메라 수평 앞 방향, 먼 땅(groundY(PZ + 300)) 높이.
                var f = _cam.transform.forward;
                f.y = 0;
                if (f.sqrMagnitude < 1e-6f) f = Vector3.forward;
                f.Normalize();
                float yaw = Mathf.Atan2(f.x, f.z);
                float dist = Mathf.Min(BackdropDistance, _cam.farClipPlane * 0.9f);   // 카메라 먼 평면 안쪽
                Backdrop.position = new Vector3(c.x + f.x * dist, far, c.z + f.z * dist);
                Backdrop.rotation = Quaternion.LookRotation(f, Vector3.up) * _backdropRot;
                float span = 2f * dist * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * _cam.aspect;   // 그 거리에서 보이는 가로(m)
                if (_farMat != null) _farMat.mainTextureOffset = new Vector2(c.x * FarParallax + YawShift(SkylineFar, _farMat, yaw, span, FarPar), 0f);
                if (_nearMat != null) _nearMat.mainTextureOffset = new Vector2(c.x * NearParallax + YawShift(SkylineNear, _nearMat, yaw, span, NearPar), 0f);
            }
            if (_high != null)
            {
                if (cl.HasValue)
                {
                    float z = (float)cl.Value.Z, lo = (float)cl.Value.Low, hi = (float)cl.Value.High;
                    PlaceCopy(_high, hi, snapZ, z, float.PositiveInfinity, cutX, section);
                    PlaceCopy(_low, lo, snapZ, float.NegativeInfinity, z, cutX, section);
                    PlaceWall(z, lo, hi, cutX);
                }
                else
                {
                    PlaceCopy(_high, gy, snapZ, float.NegativeInfinity, float.PositiveInfinity, cutX, section);
                    if (_low.Root.gameObject.activeSelf) _low.Root.gameObject.SetActive(false);
                    if (_wall != null && _wall.gameObject.activeSelf) _wall.gameObject.SetActive(false);
                }
            }
            PlaceDecos(c.z, z => cl.HasValue && z < cl.Value.Z ? (float)cl.Value.Low : far, e);
        }

        /// <summary>새 HTML 원경 yaw 이동을 텍스처 u로: 화면 이동 yaw·1.3·(0.7 + par·0.3)화면 → 보이는 가로 span(m) → 판 가로·반복 수.</summary>
        static float YawShift(Renderer r, Material m, float yaw, float span, float par)
        {
            float width = r != null ? Mathf.Abs(r.transform.lossyScale.x) : 0f;
            if (width <= 0f) return 0f;
            return yaw * YawScreens * (YawBase + par * YawPar) * span / width * m.mainTextureScale.x;
        }

        /// <summary>복제본에서 원본 자식과 같은 자리(형제 순서 경로)의 자식.</summary>
        static Transform Twin(Transform root, Transform part, Transform copyRoot)
        {
            if (part == null) return null;
            var path = new List<int>();
            for (var t = part; t != null && t != root; t = t.parent) path.Insert(0, t.GetSiblingIndex());
            var x = copyRoot;
            foreach (int i in path) x = x != null && i < x.childCount ? x.GetChild(i) : null;
            return x;
        }

        static GroundCopy Capture(Transform root, Transform near, Transform sideCut)
        {
            var g = new GroundCopy { Root = root, SideCut = sideCut };
            foreach (Transform t in root)
            {
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                bool zOnX = t == sideCut;   // 세운 단면은 가로(x 스케일)가 z를 따라 놓인다
                var part = new GroundPart
                {
                    T = t, R = r, ZOnX = zOnX, Zc = t.localPosition.z, Len = zOnX ? t.localScale.x : t.localScale.y,
                    HasST = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseMapST),
                    Near = t == near, ScaleX = t.localScale.x,
                    Left = t.localPosition.x - t.localScale.x * 0.5f, Right = t.localPosition.x + t.localScale.x * 0.5f,
                };
                if (part.HasST) part.ST = r.sharedMaterial.GetVector(BaseMapST);
                if (part.Near) g.NearRight = part.Right;
                g.Parts.Add(part);
            }
            return g;
        }

        /// <summary>
        /// 바닥 한 벌을 높이 y·z 범위 [zMin, zMax]로 깐다. 판은 범위 밖을 잘라 내고 무늬는 월드에 고정한다(자른 만큼 반복·시작점 보정).
        /// 카메라 쪽 바닥은 오른쪽 끝을 cutX까지 줄이고(<see cref="ClimbOcclusion.CutX"/>), 단면은 section일 때 그 자리에 켠다.
        /// </summary>
        static void PlaceCopy(GroundCopy g, float y, float snapZ, float zMin, float zMax, float cutX, bool section)
        {
            if (!g.Root.gameObject.activeSelf) g.Root.gameObject.SetActive(true);
            g.Root.position = new Vector3(0f, y, snapZ);
            foreach (var p in g.Parts)
            {
                float a0 = p.Zc - p.Len * 0.5f, a = Mathf.Max(a0, zMin - snapZ), b = Mathf.Min(p.Zc + p.Len * 0.5f, zMax - snapZ);
                float right = p.Near ? Mathf.Min(p.Right, cutX) : p.Right;
                float w = Mathf.Max(0f, right - p.Left);
                bool on = b > a && w > 0f && (p.T != g.SideCut || section);
                if (p.T.gameObject.activeSelf != on) p.T.gameObject.SetActive(on);
                if (!on) continue;
                float len = b - a;
                var sc = p.T.localScale;
                var pos = p.T.localPosition;
                if (p.ZOnX) sc.x = len; else sc.y = len;
                pos.z = (a + b) * 0.5f;
                if (p.Near) { sc.x = w; pos.x = p.Left + w * 0.5f; }
                if (p.T == g.SideCut) pos.x = Mathf.Min(cutX, g.NearRight);
                p.T.localScale = sc;
                p.T.localPosition = pos;
                if (!p.HasST) continue;
                // 무늬: z 축 반복 T' = T·len/L, 시작 O' = O + T·(a − a0)/L. 카메라 쪽 바닥 가로 반복은 줄인 폭 비율.
                var st = p.ST;
                float k = len / p.Len, off = (a - a0) / p.Len;
                if (p.ZOnX) { st.z += st.x * off; st.x *= k; } else { st.w += st.y * off; st.y *= k; }
                if (p.Near) st.x *= w / p.ScaleX;
                p.Mpb ??= new MaterialPropertyBlock();
                p.R.GetPropertyBlock(p.Mpb);
                p.Mpb.SetVector(BaseMapST, st);
                p.R.SetPropertyBlock(p.Mpb);
            }
        }

        /// <summary>새 HTML drawGround 절벽 면: x −70 ~ 잘린 끝, 높이 low ~ high, z = cliff.z(아래층 쪽을 본다).</summary>
        void PlaceWall(float z, float lo, float hi, float cutX)
        {
            if (_wall == null) return;
            float w = cutX - WallLeftX, h = hi - lo;
            bool on = w > 0f && h > 0f;
            if (_wall.gameObject.activeSelf != on) _wall.gameObject.SetActive(on);
            if (!on) return;
            _wall.position = new Vector3(WallLeftX + w * 0.5f, lo + h * 0.5f, z);
            _wall.rotation = Quaternion.identity;
            _wall.localScale = new Vector3(w, h, 1f);
            if (_wallST == Vector4.zero || _sideCutSize.x <= 0f || _sideCutSize.y <= 0f) return;
            _wallMpb ??= new MaterialPropertyBlock();
            _wallRenderer.GetPropertyBlock(_wallMpb);
            _wallMpb.SetVector(BaseMapST, new Vector4(_wallST.x * w / _sideCutSize.x, _wallST.y * h / _sideCutSize.y, _wallST.z, _wallST.w));
            _wallRenderer.SetPropertyBlock(_wallMpb);
        }

        void PlaceDecos(float camZ, Func<float, float> groundAt, double e)
        {
            if (DecoTemplate == null || DecoSprites.Count == 0) return;
            int k0 = Mathf.FloorToInt((camZ + DecoRange.x - DecoBackE * (float)e) / DecoSpacing), k1 = Mathf.FloorToInt((camZ + DecoRange.y) / DecoSpacing);
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
                    float dz = k * DecoSpacing + h * 1.2f;
                    sr.transform.parent.position = new Vector3(x, groundAt(dz), dz);
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
