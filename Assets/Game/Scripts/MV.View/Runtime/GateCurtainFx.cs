using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 롤 게이트(계단문·무기문) 연출(새 HTML 2026-11-13): 통과 뒤 가라앉기(안쪽 y −sink·6, 0.5 넘으면 숨김, L1503),
    /// 맞으면 마법 막 번쩍(`106_hitReaction.flash`, 0.1초), 막 위로 올라가는 반짝이. 판정 Hitbox는 그림 묶음 밖이라 가라앉아도 그대로다
    /// (게이트 판정 여부는 시뮬 `105.hittable = BeforePass`가 정한다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GateCurtainFx : RunObjectModule
    {
        [Tooltip("맞으면 번쩍이는 마법 막 그림")]
        [SerializeField] SpriteRenderer _curtain;
        [Tooltip("반짝이를 만들 그림 묶음(빌보드 묶음 안에 두면 함께 카메라를 본다)")]
        [SerializeField] Transform _sparkParent;

        Color _curtainBase;
        bool _captured;
        SpriteRenderer[] _sparks;
        double _flash;

        /// <summary>막 번쩍 남은 초.</summary>
        public double Flash => _flash;
        public SpriteRenderer Curtain => _curtain;

        public override void OnBind(RunObjectView view)
        {
            if (!_captured) { _captured = true; if (_curtain != null) _curtainBase = _curtain.color; }   // 프리팹 기본 색
            _flash = 0;
            if (_sparks == null && _sparkParent != null)
                _sparks = view.Ctx.MakeSparks(_sparkParent, ObjectReaction.GateSparks, Color.white, false);
        }

        public override void OnUnbind(RunObjectView view)
        {
            _flash = 0;
            if (_curtain != null) view.Ds.SetBaseColor(_curtain, _curtainBase);
        }

        public override void OnRunEvent(RunObjectView view, in RunEvent e)
        {
            if (e.Type != RunEventType.Hit) return;
            var r = view.Obj?.Reaction;
            if (r != null && r.flash) _flash = ObjectReaction.FlashSec;
        }

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            var o = view.Obj;
            if (_flash > 0) _flash = System.Math.Max(0, _flash - f.Dt);
            view.Pose.Offset += new Vector3(0, (float)ObjectReaction.GateSinkY(o.Sink), 0);
            if (ObjectReaction.GateHidden(o.Sink)) view.Pose.Alpha = 0f;
            if (_curtain != null) view.Ds.SetBaseColor(_curtain, ObjectViewContext.Alpha(_curtainBase, ObjectReaction.CurtainRatio(_flash > 0)));
            if (_sparks == null) return;
            for (int i = 0; i < _sparks.Length; i++)
            {
                double ph = ObjectReaction.GateSparkPhase(f.T, i);
                var at = new Vector3((float)ObjectReaction.GateSparkX(i, o.Z, o.Hw), (float)(ObjectReaction.GatePanelH * ph), -0.05f);
                ObjectViewContext.SetSpark(_sparks[i], at, ObjectReaction.GateSparkR * (1 - ph));
            }
        }
    }
}
