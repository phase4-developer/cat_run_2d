using System;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 계단 포탈(우다다 계단) 연출(새 HTML 2026-11-13, drawPortal L3735): 점선 고리 회전, 빛(사용 뒤 흐려짐), 바닥 원 둘레에서 올라가는 반짝이(`#fff4c0`).
    /// 반짝이는 바닥 원(월드 축) 둘레라 그림 묶음이 아니라 이 컴포넌트 아래에 각자 빌보드로 둔다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalFx : RunObjectModule
    {
        static readonly Color s_spark = new Color(1f, 0xf4 / 255f, 0xc0 / 255f, 1f);

        [Tooltip("빛 그림(사용한 포탈은 흐려진다). 없으면 빛 연출 없음")]
        [SerializeField] SpriteRenderer _glow;
        [Tooltip("도는 점선 고리. 없으면 회전 없음")]
        [SerializeField] Transform _spin;
        [Tooltip("바닥 원 둘레 반짝이를 만들지")]
        [SerializeField] bool _sparks = true;

        bool _captured;
        Color _glowBase;
        Quaternion _spinBase;
        SpriteRenderer[] _sparkList;

        public override void OnBind(RunObjectView view)
        {
            if (!_captured)
            {
                _captured = true;
                if (_glow != null) _glowBase = _glow.color;
                if (_spin != null) _spinBase = _spin.localRotation;
            }
            if (_sparks && _sparkList == null) _sparkList = view.Ctx.MakeSparks(transform, ObjectReaction.PortalSparks, s_spark, true);
        }

        public override void OnUnbind(RunObjectView view)
        {
            if (_glow != null) view.Ds.SetBaseColor(_glow, _glowBase);
            if (_spin != null) _spin.localRotation = _spinBase;
        }

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            var o = view.Obj;
            if (_spin != null) _spin.localRotation = _spinBase * Quaternion.Euler(0, 0, (float)(f.T * ObjectReaction.PortalSpin * 180 / Math.PI));
            if (_glow != null) view.Ds.SetBaseColor(_glow, ObjectViewContext.Alpha(_glowBase, ObjectReaction.PortalGlowRatio(o.Used)));
            if (_sparkList == null) return;
            for (int i = 0; i < _sparkList.Length; i++)
            {
                double k = ObjectReaction.PortalSparkPhase(f.T, i), a = ObjectReaction.PortalSparkAngle(f.T, i);
                var at = new Vector3((float)(Math.Cos(a) * ObjectReaction.PortalSparkRing), (float)(k * ObjectReaction.PortalSparkH), (float)(Math.Sin(a) * ObjectReaction.PortalSparkRing));
                ObjectViewContext.SetSpark(_sparkList[i], at, ObjectReaction.PortalSparkR * (1 - k));
            }
        }
    }
}
