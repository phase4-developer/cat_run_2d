using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 게이트 등급 색: 지금 런 무기 단계(Bt)의 `103_item.gradeCol`로 보석(SpriteRenderer)과 파티클 시작 색을 칠한다.
    /// 색은 통합 코드가 <see cref="ObjectViewContext.GateWeaponColor"/>에 넣는다. 빈칸이면 프리팹 색 그대로.
    /// 파티클은 등급색 ↔ 흰색 쪽으로 <see cref="Lighten"/>만큼 섞은 색 사이 무작위.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GradeColorTint : RunObjectModule
    {
        [Tooltip("등급색으로 칠할 보석 그림")] public SpriteRenderer[] Gems = new SpriteRenderer[0];
        [Tooltip("시작 색을 등급색으로 바꿀 파티클")] public ParticleSystem[] Particles = new ParticleSystem[0];
        [Tooltip("파티클 두 번째 시작 색을 흰색 쪽으로 섞는 정도(색 사이 무작위)")] [Range(0f, 1f)] public float Lighten = 0.4f;

        bool _cached;
        Color[] _gemBase;
        ParticleSystem.MinMaxGradient[] _psBase;
        string _shown;

        void Cache()
        {
            if (_cached) return;
            _cached = true;
            _gemBase = new Color[Gems != null ? Gems.Length : 0];
            for (int i = 0; i < _gemBase.Length; i++) _gemBase[i] = Gems[i] != null ? Gems[i].color : Color.white;
            _psBase = new ParticleSystem.MinMaxGradient[Particles != null ? Particles.Length : 0];
            for (int i = 0; i < _psBase.Length; i++) if (Particles[i] != null) _psBase[i] = Particles[i].main.startColor;
        }

        public override void OnBind(RunObjectView view)
        {
            _shown = null;
            Apply(view.Ctx != null ? view.Ctx.GateWeaponColor : null);
        }

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            if (view.Ctx != null) Apply(view.Ctx.GateWeaponColor);
        }

        public override void OnUnbind(RunObjectView view)
        {
            Restore();
            _shown = null;
        }

        /// <summary>등급색 hex로 칠한다(빈칸 = 프리팹 색). 에디터 미리보기에서도 부를 수 있다.</summary>
        public void Apply(string hex)
        {
            if (hex == null) hex = "";
            if (hex == _shown) return;
            _shown = hex;
            Color32 c32;
            if (hex.Length == 0 || !MvColor.TryColor32(hex, out c32)) { Restore(); return; }
            Apply((Color)c32);
        }

        /// <summary>색 <paramref name="c"/>로 칠한다(알파는 프리팹 값 유지).</summary>
        public void Apply(Color c)
        {
            Cache();
            var sort = GetComponentInParent<DepthSortable>(true);
            for (int i = 0; i < _gemBase.Length; i++)
            {
                var sr = Gems[i];
                if (sr == null) continue;
                var g = new Color(c.r, c.g, c.b, _gemBase[i].a);
                sr.color = g;
                if (sort != null) sort.SetBaseColor(sr, g);
            }
            for (int i = 0; i < _psBase.Length; i++)
            {
                var ps = Particles[i];
                if (ps == null) continue;
                float a = BaseAlpha(_psBase[i]);
                var a0 = new Color(c.r, c.g, c.b, a);
                var a1 = Color.Lerp(a0, new Color(1f, 1f, 1f, a), Lighten);
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(a0, a1);
            }
        }

        /// <summary>프리팹 색으로 되돌린다.</summary>
        public void Restore()
        {
            if (!_cached) return;
            var sort = GetComponentInParent<DepthSortable>(true);
            for (int i = 0; i < _gemBase.Length; i++)
            {
                if (Gems[i] == null) continue;
                Gems[i].color = _gemBase[i];
                if (sort != null) sort.SetBaseColor(Gems[i], _gemBase[i]);
            }
            for (int i = 0; i < _psBase.Length; i++)
            {
                if (Particles[i] == null) continue;
                var main = Particles[i].main;
                main.startColor = _psBase[i];
            }
        }

        static float BaseAlpha(ParticleSystem.MinMaxGradient g)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color: return g.color.a;
                case ParticleSystemGradientMode.TwoColors: return Mathf.Max(g.colorMin.a, g.colorMax.a);
                default: return 1f;
            }
        }
    }
}
