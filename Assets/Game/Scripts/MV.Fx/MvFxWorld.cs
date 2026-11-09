using MV.Run;

namespace MV.Fx
{
    /// <summary>이펙트 위치를 정할 때 읽는 오브젝트 값(정본 `o.x`, `o.z`, `o.hd`, `o.k`, `o.rew`).</summary>
    public struct FxObj
    {
        public string KindKey;
        public double X, Z, Hd;
        public FieldReward Rew;
        public bool IsField;
    }

    /// <summary>런 상태 읽기 창. MV.Fx는 상태를 바꾸지 않는다. 시험은 가짜를 넣는다.</summary>
    public interface IMvFxWorld
    {
        bool TryObj(int id, out FxObj o);
        double PlayerX { get; }
        double PlayerZ { get; }
        /// <summary>계단 오르기 세계 높이 WY(04 §4.8, y 열은 여기에 더한다).</summary>
        double WorldY { get; }
        /// <summary>히트스톱 중이고 런 단계면 true(06 §6.5, 시간 배율은 MV.Run이 정한다).</summary>
        bool HitStopActive { get; }
        /// <summary>배속(`RunSetup.TimeScale`, 1 이상).</summary>
        int TimeScale { get; }
    }

    /// <summary><see cref="RunState"/>를 읽는 기본 창.</summary>
    public sealed class RunStateFxWorld : IMvFxWorld, IMvFxProjectiles
    {
        readonly System.Func<RunState> _state;

        public RunStateFxWorld(System.Func<RunState> state) => _state = state;

        public bool TryObj(int id, out FxObj o)
        {
            var s = _state?.Invoke();
            var r = s?.FindObj(id);
            if (r == null) { o = default; return false; }
            o = new FxObj { KindKey = r.Kind?.key, X = r.X, Z = r.Z, Hd = r.Hd, Rew = r.Rew, IsField = r.IsField };
            return true;
        }

        /// <summary>투사체 위치(높이 1 m + WY, 정본 탄 메쉬 높이).</summary>
        public bool TryProjectile(int id, out UnityEngine.Vector3 pos)
        {
            var s = _state?.Invoke();
            if (s != null)
                for (int i = 0; i < s.Projs.Count; i++)
                {
                    var b = s.Projs[i];
                    if (b.Id != id || b.Dead) continue;
                    pos = new UnityEngine.Vector3((float)b.X, (float)(1 + s.WorldY), (float)b.Z);
                    return true;
                }
            pos = default;
            return false;
        }

        public double PlayerX => _state?.Invoke()?.P.X ?? 0;
        public double PlayerZ => _state?.Invoke()?.P.Z ?? 0;
        public double WorldY => _state?.Invoke()?.WorldY ?? 0;

        public bool HitStopActive
        {
            get
            {
                var s = _state?.Invoke();
                return s != null && s.HitStop > 0 && s.Phase == RunPhase.Run;
            }
        }

        public int TimeScale
        {
            get
            {
                var s = _state?.Invoke();
                return s?.Setup == null ? 1 : System.Math.Max(1, s.Setup.TimeScale);
            }
        }
    }
}
