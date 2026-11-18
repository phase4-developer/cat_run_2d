using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 결계 블록 연출(새 HTML 2026-11-13): 블록마다 자리 흔들기(L3677, 그림만), 맞으면 가로 흔들림(`106_hitReaction.objShake`, L1484),
    /// 줄(bar) 순서로 상자 그림 하나만 켜기(정본 chest(bar % n), n = <see cref="_chests"/> 길이), 생선 빛 맥동.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallBlockFx : RunObjectModule
    {
        [Tooltip("줄 순서로 하나만 켜는 상자 그림(비우면 그대로 둔다)")]
        [SerializeField] GameObject[] _chests = new GameObject[0];
        [Tooltip("맥동하는 빛 그림(없으면 빛 연출 없음)")]
        [SerializeField] SpriteRenderer _glow;

        bool _captured;
        Color _glowBase;
        bool[] _chestBase;
        double _shake;

        /// <summary>흔들림 남은 초.</summary>
        public double Shake => _shake;

        public override void OnBind(RunObjectView view)
        {
            if (!_captured)
            {
                _captured = true;
                if (_glow != null) _glowBase = _glow.color;
                _chestBase = new bool[_chests.Length];
                for (int i = 0; i < _chests.Length; i++) _chestBase[i] = _chests[i] != null && _chests[i].activeSelf;
            }
            _shake = 0;
        }

        public override void OnUnbind(RunObjectView view)
        {
            _shake = 0;
            if (_glow != null) view.Ds.SetBaseColor(_glow, _glowBase);
            for (int i = 0; i < _chests.Length; i++) if (_chests[i] != null) _chests[i].SetActive(_chestBase[i]);
        }

        public override void OnRunEvent(RunObjectView view, in RunEvent e)
        {
            if (e.Type != RunEventType.Hit) return;
            var r = view.Obj?.Reaction;
            if (r != null && r.objShake > 0) _shake = ObjectReaction.ShakeSec;
        }

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            var o = view.Obj;
            if (_shake > 0) _shake = System.Math.Max(0, _shake - f.Dt);
            ObjectReaction.WallJitter(o.Idx, o.Bar, out double jx, out double jz);
            view.Pose.Offset += new Vector3((float)(ObjectReaction.WallShakeX(_shake) + jx), 0, (float)jz);
            if (_chests.Length > 0)
            {
                int pick = System.Math.Max(0, o.Bar) % _chests.Length;
                for (int i = 0; i < _chests.Length; i++)
                    if (_chests[i] != null && _chests[i].activeSelf != (i == pick)) _chests[i].SetActive(i == pick);
            }
            if (_glow != null) view.Ds.SetBaseColor(_glow, ObjectViewContext.Alpha(_glowBase, ObjectReaction.FishGlowRatio(f.T)));
        }
    }
}
