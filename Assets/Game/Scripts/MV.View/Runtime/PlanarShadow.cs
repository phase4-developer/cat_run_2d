using UnityEngine;
using UnityEngine.Rendering;

namespace MV.View
{
    /// <summary>
    /// 평면 투영 그림자: <see cref="Sources"/> 스프라이트를 고정된 빛 방향(<see cref="LightDir"/>)을 따라 바닥 면(y = 바닥 높이)에 눌러 그린다.
    /// 그림자는 원본 정렬 그룹 밖, 바닥 레이어(<see cref="SortingLayer"/>)에 따로 두어 길 위 오브젝트를 가리지 않는다.
    /// 원본의 그림·뒤집기·알파(안개·옆 시점 페이드)를 매 프레임 따라간다. 빌보드 회전·안개가 정해진 뒤(DepthSorter 100) 돈다.
    /// 투영(아핀 변환)은 2D SVD로 회전·비균등 크기·회전 두 단계 Transform에 나눠 담는다(메쉬·셰이더 추가 없음, 스프라이트 배칭 유지).
    /// </summary>
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class PlanarShadow : MonoBehaviour
    {
        /// <summary>빛이 나아가는 방향(위에서 아래로, y &lt; 0). 스테이지 맵(<see cref="StageMap"/>)이 넣는다.</summary>
        public static Vector3 LightDir = new Vector3(0.55f, -1f, 0.45f);
        /// <summary>그림자 색(알파 = 진하기). 원본 알파를 곱한다.</summary>
        public static Color ShadowColor = new Color(0f, 0f, 0f, 0.5f);
        public static string SortingLayer = "Ground";
        public static int SortingOrder = 10;

        [Tooltip("그림자를 만들 원본 스프라이트(본체·기둥 등). 빛·글자판처럼 그림자가 필요 없는 것은 넣지 않는다")]
        public SpriteRenderer[] Sources = new SpriteRenderer[0];
        [Tooltip("그림자 재질(비우면 원본 재질). 보통 MV_WorldSprite_Shadow")]
        public Material Material;
        [Tooltip("켜면 부모의 높이를 바닥으로 쓴다(점프·흔들림이 자식에만 걸리는 오브젝트). 끄면 이 오브젝트의 높이")]
        public bool GroundFromParent;
        [Tooltip("이 오브젝트만의 진하기 배율")]
        [Range(0f, 2f)] public float Strength = 1f;

        /// <summary>바닥과 겹쳐 깜빡이지 않게 띄우는 높이(m).</summary>
        const float Lift = 0.02f;

        sealed class Item
        {
            public SpriteRenderer Src, Sr;
            public Transform Outer, Inner;
        }

        Item[] _items;
        GameObject _holder;
        SortingGroup _group;

        void OnEnable()
        {
            Build();
            if (_holder != null && !_holder.activeSelf) _holder.SetActive(true);
        }

        void OnDisable()
        {
            if (_holder != null) _holder.SetActive(false);
        }

        void OnDestroy()
        {
            if (_holder != null) Destroy(_holder);
        }

        void Build()
        {
            if (_holder != null || Sources == null) return;
            // 원본 정렬 그룹 밖(장면 최상위)에 둔다: 그룹 안에 있으면 그룹 순서로 같이 정렬돼 앞의 오브젝트를 가린다.
            _holder = new GameObject(name + "_PlanarShadow");
            _group = _holder.AddComponent<SortingGroup>();
            _group.sortingLayerName = SortingLayer;
            _group.sortingOrder = SortingOrder;
            _items = new Item[Sources.Length];
            for (int i = 0; i < Sources.Length; i++)
            {
                var src = Sources[i];
                var outer = new GameObject(src != null ? src.name : "Shadow").transform;
                outer.SetParent(_holder.transform, false);
                var inner = new GameObject("Sprite", typeof(SpriteRenderer)).transform;
                inner.SetParent(outer, false);
                var sr = inner.GetComponent<SpriteRenderer>();
                sr.sharedMaterial = Material != null ? Material : src != null ? src.sharedMaterial : null;
                sr.drawMode = SpriteDrawMode.Simple;
                _items[i] = new Item { Src = src, Sr = sr, Outer = outer, Inner = inner };
                outer.gameObject.SetActive(false);
            }
        }

        void LateUpdate()
        {
            if (_items == null) return;
            if (_group.sortingOrder != SortingOrder) _group.sortingOrder = SortingOrder;
            if (_group.sortingLayerName != SortingLayer) _group.sortingLayerName = SortingLayer;
            Vector3 L = LightDir;
            if (L.y > -0.05f) L.y = -0.05f;   // 빛이 수평에 가까우면 그림자가 끝없이 길어진다
            var gt = GroundFromParent && transform.parent != null ? transform.parent : transform;
            float g = gt.position.y + Lift;
            for (int i = 0; i < _items.Length; i++)
            {
                var it = _items[i];
                var s = it.Src;
                bool on = s != null && s.enabled && s.gameObject.activeInHierarchy && s.sprite != null && s.color.a > 0.004f;
                if (it.Outer.gameObject.activeSelf != on) it.Outer.gameObject.SetActive(on);
                if (!on) continue;

                if (it.Sr.sprite != s.sprite) it.Sr.sprite = s.sprite;
                it.Sr.flipX = s.flipX;
                it.Sr.flipY = s.flipY;
                var c = ShadowColor;
                c.a *= s.color.a * Strength;
                it.Sr.color = c;

                // 원본 평면(피벗 o, 가로 a, 세로 b)을 빛 방향으로 바닥 y = g에 투영한다.
                var st = s.transform;
                Vector3 o = st.position, a = st.TransformVector(Vector3.right), b = st.TransformVector(Vector3.up);
                Vector3 po = o - L * ((o.y - g) / L.y);
                Vector3 pa = a - L * (a.y / L.y);
                Vector3 pb = b - L * (b.y / L.y);

                // 바닥 좌표 (x, z)에서 M = [pa pb]를 R(phi)·diag(sx, sy)·R(theta)로 나눈다(2×2 SVD).
                float m00 = pa.x, m01 = pb.x, m10 = pa.z, m11 = pb.z;
                float e = (m00 + m11) * 0.5f, f = (m00 - m11) * 0.5f, gg = (m10 + m01) * 0.5f, h = (m10 - m01) * 0.5f;
                float q = Mathf.Sqrt(e * e + h * h), r = Mathf.Sqrt(f * f + gg * gg);
                float sx = q + r, sy = q - r;
                float a1 = Mathf.Atan2(gg, f), a2 = Mathf.Atan2(h, e);
                float theta = (a2 - a1) * 0.5f * Mathf.Rad2Deg, phi = (a2 + a1) * 0.5f * Mathf.Rad2Deg;

                // 바깥: 바닥에 눕히고(로컬 x→월드 x, 로컬 y→월드 z) phi 회전·크기, 안쪽: theta 회전.
                it.Outer.SetPositionAndRotation(new Vector3(po.x, g, po.z), Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, phi));
                it.Outer.localScale = new Vector3(sx, sy, 1f);
                it.Inner.localRotation = Quaternion.Euler(0f, 0f, theta);
            }
        }
    }
}
