using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 파티클에 안개·가림 알파를 넣는다(<see cref="DepthSortable"/>의 안개 알파 대상). 스프라이트처럼 정렬 루트가 매기는
    /// 먼 끝 안개·옆 시점 페이드·가림 페이드 알파를 아래 모든 파티클 렌더러(자취 포함)에 곱한다.
    /// 재질에 따라 Opacity(Hovl HS_Blend) / _BaseColor(URP Particles) / _TintColor / _Color 알파를 렌더러별 속성 블록으로 바꾼다(공유 재질은 그대로).
    /// 알파 속성이 없는 재질(Mobile/Particles/Additive 등)은 알파가 거의 0이면 렌더러를 숨긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParticleFogAlpha : MonoBehaviour, IFogAlphaTarget
    {
        static readonly int s_opacity = Shader.PropertyToID("_Opacity"), s_base = Shader.PropertyToID("_BaseColor"),
            s_tint = Shader.PropertyToID("_TintColor"), s_color = Shader.PropertyToID("_Color");

        enum Mode : byte { Opacity, BaseColor, Tint, Color, None }

        [Tooltip("알파를 못 바꾸는 재질의 파티클을 숨기는 기준: 안개·가림 알파가 이 값보다 작으면 렌더러를 끈다")]
        [SerializeField, Range(0f, 1f)] float _hideBelow = 0.05f;

        /// <summary>알파를 못 바꾸는 재질의 숨김 기준(시험·QA).</summary>
        public float HideBelow => _hideBelow;

        ParticleSystemRenderer[] _rs;
        Mode[] _mode;
        Color[] _baseColor;
        float[] _baseOpacity;
        MaterialPropertyBlock _mpb;
        float _last = -1f;

        void Collect()
        {
            _rs = GetComponentsInChildren<ParticleSystemRenderer>(true);
            _mode = new Mode[_rs.Length];
            _baseColor = new Color[_rs.Length];
            _baseOpacity = new float[_rs.Length];
            for (int i = 0; i < _rs.Length; i++)
            {
                var m = _rs[i].sharedMaterial != null ? _rs[i].sharedMaterial : _rs[i].trailMaterial;
                if (m == null) { _mode[i] = Mode.None; continue; }
                if (m.HasProperty(s_opacity)) { _mode[i] = Mode.Opacity; _baseOpacity[i] = m.GetFloat(s_opacity); }
                else if (m.HasProperty(s_base)) { _mode[i] = Mode.BaseColor; _baseColor[i] = m.GetColor(s_base); }
                else if (m.HasProperty(s_tint)) { _mode[i] = Mode.Tint; _baseColor[i] = m.GetColor(s_tint); }
                else if (m.HasProperty(s_color)) { _mode[i] = Mode.Color; _baseColor[i] = m.GetColor(s_color); }
                else _mode[i] = Mode.None;
            }
            _mpb ??= new MaterialPropertyBlock();
        }

        public void SetFogAlpha(float alpha)
        {
            if (_rs == null) Collect();
            if (Mathf.Abs(alpha - _last) < 1f / 255f) return;
            _last = alpha;
            for (int i = 0; i < _rs.Length; i++)
            {
                var r = _rs[i];
                if (r == null) continue;
                if (_mode[i] == Mode.None)
                {
                    // 알파를 못 바꾸는 재질: 거의 사라질 때만 숨긴다(기준 = 프리팹 직렬화 _hideBelow).
                    r.forceRenderingOff = alpha < _hideBelow;
                    continue;
                }
                r.GetPropertyBlock(_mpb);
                switch (_mode[i])
                {
                    case Mode.Opacity: _mpb.SetFloat(s_opacity, _baseOpacity[i] * alpha); break;
                    case Mode.BaseColor: { var c = _baseColor[i]; c.a *= alpha; _mpb.SetColor(s_base, c); break; }
                    case Mode.Tint: { var c = _baseColor[i]; c.a *= alpha; _mpb.SetColor(s_tint, c); break; }
                    case Mode.Color: { var c = _baseColor[i]; c.a *= alpha; _mpb.SetColor(s_color, c); break; }
                }
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
