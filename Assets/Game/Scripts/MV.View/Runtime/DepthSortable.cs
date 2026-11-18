using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MV.View
{
    /// <summary>안개 알파를 받는 그림(정점색 메쉬·TMP 라벨 등). 스프라이트는 <see cref="DepthSortable"/>가 직접 처리한다.</summary>
    public interface IFogAlphaTarget
    {
        void SetFogAlpha(float alpha);
    }

    /// <summary>안개를 색으로 받는 그림(정점색 메쉬 등). <paramref name="k"/> = 안개 비율(0 맑음 ~ 1 안개색), <paramref name="pop"/> = 먼 끝 등장 알파.</summary>
    public interface IFogColorTarget
    {
        void SetFog(float k, float pop, Color fogColor);
    }

    /// <summary>
    /// 월드 그림 단위(오브젝트·소품·고양이·글자판·이펙트 1회분)의 루트(02 §2.3.2). `SortingGroup`(레이어 `World`)을 달고
    /// <see cref="DepthSorter"/>에 등록된다. 탐침 = 앵커 + <see cref="SortOffsetY"/>(오브젝트 +0.8, 고양이 +0.7),
    /// 안개 기준 = 앵커(오브젝트도 +0.8 아님, 02 §2.4.1). 생성 위치와 그리는 위치가 다른 소품은 <see cref="SetFixedAnchor"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SortingGroup))]
    public sealed class DepthSortable : MonoBehaviour
    {
        static readonly List<DepthSortable> s_all = new List<DepthSortable>(512);

        [Tooltip("정렬 범주 순위: 소품·빨랫줄 0, 오브젝트·글자판 1, 고양이·장난감·투사체 2, 이펙트 3")]
        public int Rank;
        [Tooltip("정렬 편향 m(빨랫줄 +0.5, 글자판 −0.05, 회전 장난감 −0.03, 투사체 −0.02)")]
        public float Bias;
        [Tooltip("탐침 높이 m(오브젝트 sort.objProbeY, 고양이 sort.catProbeY)")]
        public float SortOffsetY;
        [Tooltip("안개 알파 적용(고양이·이펙트·가산광은 끈다)")]
        public bool Fog = true;

        SortingGroup _group;
        SpriteRenderer[] _sprites;
        Color[] _baseColors;
        IFogAlphaTarget[] _fogTargets;
        Renderer[] _renderers;
        bool _fixed;
        Vector3 _fixedAnchor;
        bool _shown = true;
        float _lastAlpha = -1f;
        int _index = -1;

        float _alphaScale = 1f;
        /// <summary>가림 페이드 알파(<see cref="DepthSorter"/>가 매 프레임 넣는다). 그림 알파·안개 알파에 곱한다.</summary>
        float _occludeAlpha = 1f;

        internal void SetOccludeAlpha(float a) => _occludeAlpha = a;
        float _lastK = -1f, _lastPop = -1f;

        /// <summary>안개색(카메라 배경 = 테마 fog). <see cref="DepthSorter"/>가 매 프레임 넣는다.</summary>
        public static Color FogColor = Color.white;

        static readonly Dictionary<Shader, bool> s_shaderFog = new Dictionary<Shader, bool>();

        /// <summary>셰이더가 색 안개를 직접 하는지(MV/WorldSprite·MV/WorldSpriteTint). 그런 그림은 알파를 먼 끝 등장 페이드에만 쓴다.</summary>
        static bool ShaderFog(Renderer r)
        {
            var m = r.sharedMaterial;
            var sh = m != null ? m.shader : null;
            if (sh == null) return false;
            if (!s_shaderFog.TryGetValue(sh, out bool on))
                s_shaderFog[sh] = on = sh.name == "MV/WorldSprite" || sh.name == "MV/WorldSpriteTint";
            return on;
        }

        public static List<DepthSortable> All => s_all;

        /// <summary>그림 자체 알파(관문 가까이 흐려짐 등). 안개 알파에 곱한다.</summary>
        public float AlphaScale => _alphaScale;

        public void SetAlphaScale(float a)
        {
            if (Mathf.Abs(a - _alphaScale) < 1f / 255f) return;
            _alphaScale = a;
            _lastAlpha = -1f;
        }
        public SortingGroup Group => _group;

        /// <summary>정렬·안개 기준 점(월드). 정본 소품은 생성 위치로 정렬하고 고정 x로 그린다(draw2D L3785).</summary>
        public Vector3 Anchor => _fixed ? _fixedAnchor : transform.position;

        public void SetFixedAnchor(Vector3 world)
        {
            _fixed = true;
            _fixedAnchor = world;
        }

        public void ClearFixedAnchor() => _fixed = false;

        /// <summary>자식 구성이 바뀐 뒤(스프라이트 교체·자식 추가) 부른다. 기본 색은 지금 색으로 다시 잡는다.</summary>
        public void Refresh()
        {
            _group = GetComponent<SortingGroup>();
            // 이미 기준 색을 잡아 둔 스프라이트는 그 값을 유지한다. 지금 색은 안개 알파가 곱해진 값일 수 있어서
            // 다시 읽으면 알파가 계속 줄어든다(풀 재사용·스프라이트 교체 뒤 Refresh). 새로 생긴 스프라이트만 지금 색을 읽는다.
            var oldSprites = _sprites; var oldBase = _baseColors;
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
            _baseColors = new Color[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++)
            {
                int k = oldSprites != null ? System.Array.IndexOf(oldSprites, _sprites[i]) : -1;
                _baseColors[i] = k >= 0 && oldBase != null && k < oldBase.Length ? oldBase[k] : _sprites[i].color;
            }
            _fogTargets = GetComponentsInChildren<IFogAlphaTarget>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _lastAlpha = -1f;
            _shown = true;
        }

        /// <summary>바꾼 기본 색(테마·불 켬)을 알린다. 안개 알파는 이 색의 알파에 곱한다.</summary>
        public void SetBaseColor(SpriteRenderer sr, Color c)
        {
            if (_sprites == null) Refresh();
            for (int i = 0; i < _sprites.Length; i++)
                if (_sprites[i] == sr) { _baseColors[i] = c; _lastAlpha = -1f; return; }
        }

        void Awake()
        {
            if (_sprites == null) Refresh();
        }

        void OnEnable()
        {
            _index = s_all.Count;
            s_all.Add(this);
        }

        void OnDisable()
        {
            int last = s_all.Count - 1;
            if (_index >= 0 && _index <= last)
            {
                var moved = s_all[last];
                s_all[_index] = moved;
                moved._index = _index;
                s_all.RemoveAt(last);
            }
            _index = -1;
        }

        internal void ApplyVisible(bool shown)
        {
            if (shown == _shown) return;
            _shown = shown;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = shown;
        }

        internal void ApplyOrder(int order)
        {
            if (_group.sortingOrder != order) _group.sortingOrder = order;
        }

        /// <summary>
        /// 안개 알파를 `SpriteRenderer.color`에 넣는다(03 §3.8.9: MPB·머티리얼 복제 금지, 공유 머티리얼 배칭 유지).
        /// 1/255 미만 변화는 건너뛴다.
        /// </summary>
        /// <summary>
        /// 안개(색 안개판): 셰이더가 색을 섞는 그림(<see cref="ShaderFog"/>)은 알파 = 기본 알파 × 등장 페이드 <paramref name="pop"/>,
        /// 그 밖의 스프라이트(기본 셰이더)는 지금처럼 알파 = 기본 알파 × (1 − k). 정점색 메쉬(<see cref="IFogColorTarget"/>)는 색을 섞고,
        /// 그 밖의 대상(글자판)은 알파 1 − k. 1/255 미만 변화는 건너뛴다.
        /// </summary>
        internal void ApplyFog(float k, float pop)
        {
            if (!Fog) { k = 0f; pop = 1f; }
            float a = (1f - k) * _alphaScale * _occludeAlpha, p = pop * _alphaScale * _occludeAlpha;
            if (Mathf.Abs(a - _lastAlpha) < 1f / 255f && Mathf.Abs(p - _lastPop) < 1f / 255f && Mathf.Abs(k - _lastK) < 1f / 255f) return;
            _lastAlpha = a; _lastPop = p; _lastK = k;
            for (int i = 0; i < _sprites.Length; i++)
            {
                var c = _baseColors[i];
                c.a *= ShaderFog(_sprites[i]) ? p : a;
                _sprites[i].color = c;
            }
            for (int i = 0; i < _fogTargets.Length; i++)
            {
                if (_fogTargets[i] is IFogColorTarget ct) ct.SetFog(k, p, FogColor);
                else _fogTargets[i].SetFogAlpha(a);
            }
        }

        internal void ApplyFogAlpha(float a)
        {
            if (!Fog) a = 1f;
            a *= _alphaScale * _occludeAlpha;
            if (Mathf.Abs(a - _lastAlpha) < 1f / 255f) return;
            _lastAlpha = a;
            for (int i = 0; i < _sprites.Length; i++)
            {
                var c = _baseColors[i];
                c.a *= a;
                _sprites[i].color = c;
            }
            for (int i = 0; i < _fogTargets.Length; i++) _fogTargets[i].SetFogAlpha(a);
        }
    }
}
