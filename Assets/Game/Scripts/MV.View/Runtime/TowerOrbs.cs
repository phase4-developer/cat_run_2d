using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 마법서 탑(우물) 연출(새 HTML 2026-11-13): 빛 맥동, 맞으면 구슬 묶음 흔들림(`106_hitReaction.objShake`, L1477),
    /// 모은 피해가 비워지면(WellFlush) 구슬 A개를 빼서 숨긴 뒤 다시 자람(정본 flushBricks 규칙, 튀는 조각·게이지 비행은 MvFxRouter), 구슬 맥동.
    /// 구슬 수 = <see cref="_orbs"/> 배열 길이(프리팹이 정한다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerOrbs : RunObjectModule
    {
        [Tooltip("맥동하는 빛 그림(없으면 빛 연출 없음)")]
        [SerializeField] SpriteRenderer _glow;
        [Tooltip("맞으면 흔들리는 구슬 묶음")]
        [SerializeField] Transform _pile;
        [Tooltip("빠지고 다시 자라는 구슬(순서 = 고르는 순서)")]
        [SerializeField] Transform[] _orbs = new Transform[0];

        bool _captured;
        Color _glowBase;
        Vector3 _pileBase;
        Vector3[] _orbScale;
        double[] _gone, _grow;
        double _shake;

        /// <summary>흔들림 남은 초.</summary>
        public double Shake => _shake;

        /// <summary>프리팹이 정한 구슬 수.</summary>
        public int OrbCount => _orbs != null ? _orbs.Length : 0;

        /// <summary>지금 빠져 있는(숨김) 구슬 수(시험·QA).</summary>
        public int GoneCount
        {
            get
            {
                int n = 0;
                if (_gone != null) for (int i = 0; i < _gone.Length; i++) if (_gone[i] > 0) n++;
                return n;
            }
        }

        void Capture()
        {
            if (_captured) return;
            _captured = true;
            if (_glow != null) _glowBase = _glow.color;
            if (_pile != null) _pileBase = _pile.localPosition;
            int n = _orbs != null ? _orbs.Length : 0;
            _orbScale = new Vector3[n];
            for (int i = 0; i < n; i++) _orbScale[i] = _orbs[i] != null ? _orbs[i].localScale : Vector3.one;
            _gone = new double[n];
            _grow = new double[n];
        }

        public override void OnBind(RunObjectView view)
        {
            Capture();
            _shake = 0;
            for (int i = 0; i < _gone.Length; i++)
            {
                _gone[i] = 0;
                _grow[i] = 1;
                if (_orbs[i] != null) _orbs[i].localScale = _orbScale[i];
            }
            if (_pile != null) _pile.localPosition = _pileBase;
        }

        public override void OnUnbind(RunObjectView view)
        {
            OnBind(view);
            if (_glow != null) view.Ds.SetBaseColor(_glow, _glowBase);
        }

        public override void OnRunEvent(RunObjectView view, in RunEvent e)
        {
            if (e.Type == RunEventType.Hit)
            {
                var r = view.Obj?.Reaction;
                if (r != null && r.objShake > 0) _shake = ObjectReaction.ShakeSec;
            }
            else if (e.Type == RunEventType.WellFlush) view.Ctx.Flush(_gone, _grow, System.Math.Max(1, e.A));
        }

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            if (_shake > 0) _shake = System.Math.Max(0, _shake - f.Dt);
            if (_glow != null) view.Ds.SetBaseColor(_glow, ObjectViewContext.Alpha(_glowBase, ObjectReaction.TowerGlowRatio(f.T)));
            if (_pile != null) _pile.localPosition = _pileBase + new Vector3((float)ObjectReaction.WellShakeX(_shake), 0, 0);
            for (int i = 0; i < _gone.Length; i++)
            {
                if (_orbs[i] == null) continue;
                double sc = ObjectReaction.BrickStep(ref _gone[i], ref _grow[i], f.Dt);
                _orbs[i].localScale = _orbScale[i] * (float)(sc * ObjectReaction.OrbPulse(f.T, i));
            }
        }
    }
}
