using System;
using System.Collections.Generic;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 스테이지 맵 프리팹(`Stage_{n}`) 루트. 게임 루프가 지역(area)에 맞는 프리팹을 불러와 <see cref="Bind"/>한다.
    /// 하는 일은 셋뿐이다.
    /// 1) Backdrop(하늘·원경)을 카메라 앞 먼 곳에 붙이고, 원경은 카메라 x를 따라 텍스처를 밀어 시차를 낸다.
    /// 2) Ground(바닥 판)를 카메라 z를 따라 <see cref="GroundSnap"/> 단위로 옮겨 끝없이 이어 보이게 한다(타일 반복 길이의 배수라 이음매가 안 보인다).
    /// 3) Deco(길가 장식)를 정해진 간격으로 재배치하며 <see cref="DecoSprites"/> 중 하나를 골라 그린다.
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

        [Header("길가 장식")]
        public SpriteRenderer DecoTemplate;
        public List<Sprite> DecoSprites = new List<Sprite>();
        [Tooltip("한쪽 장식 간격(m)")] public float DecoSpacing = 12.6f;
        [Tooltip("길 중심에서 장식까지 거리(m), 아래·위 범위에서 무작위")] public Vector2 DecoX = new Vector2(6.9f, 8.0f);
        [Tooltip("카메라 뒤·앞으로 장식을 둘 범위(m)")] public Vector2 DecoRange = new Vector2(-10f, 110f);

        Camera _cam;
        Func<float> _groundY;
        readonly List<SpriteRenderer> _decos = new List<SpriteRenderer>();
        Material _farMat, _nearMat;

        /// <summary>게임 루프가 부른다. groundY = 지금 지면 높이(계단을 오르면 올라간다).</summary>
        public void Bind(Camera cam, Func<float> groundY)
        {
            _cam = cam;
            _groundY = groundY;
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
            PlaceDecos(c.z, gy);
        }

        void PlaceDecos(float camZ, float gy)
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
                    sr.transform.parent.position = new Vector3(side * Mathf.Lerp(DecoX.x, DecoX.y, h), gy, k * DecoSpacing + h * 1.2f);
                    if (!sr.transform.parent.gameObject.activeSelf) sr.transform.parent.gameObject.SetActive(true);
                }
            for (; n < _decos.Count; n++) if (_decos[n].transform.parent.gameObject.activeSelf) _decos[n].transform.parent.gameObject.SetActive(false);
        }

        static float Hash(float x) { float s = Mathf.Sin(x * 127.1f + 311.7f) * 43758.5453f; return s - Mathf.Floor(s); }
    }
}
