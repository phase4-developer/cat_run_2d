using MV.Data;
using MV.Run;

namespace MV.View
{
    /// <summary>
    /// 월드 렌더가 매 프레임 읽는 런 상태(읽기 전용). 런 구현 <see cref="RunStateViewSource"/>는 WY·옹벽을 <see cref="IRunViewState"/>에서 읽는다.
    /// </summary>
    public interface IWorldViewSource
    {
        CameraInput Camera { get; }
        /// <summary>정본 `R.cliff`(계단 꼭대기 옹벽). 없으면 false.</summary>
        bool TryGetCliff(out CliffInfo cliff);
    }

    /// <summary>값을 직접 넣는 공급원(로비·통합 전 시험·QA 하네스 고정 상태).</summary>
    public sealed class FixedWorldViewSource : IWorldViewSource
    {
        public CameraInput Input;
        public bool HasCliff;
        public CliffInfo Cliff;

        public CameraInput Camera => Input;

        public bool TryGetCliff(out CliffInfo cliff)
        {
            cliff = Cliff;
            return HasCliff;
        }
    }

    /// <summary>
    /// `RunState` → 카메라 입력. 계단 오르기 중 플레이어 높이는 정본 L2758 식(<see cref="CameraModel.ClimbPlayer"/>).
    /// </summary>
    public sealed class RunStateViewSource : IWorldViewSource
    {
        readonly RunState _s;
        readonly double _stepH, _stepD, _bump;

        /// <summary>지면 높이 WY(<see cref="IRunViewState.WorldY"/>, MV-RUN-VIEWAPI).</summary>
        public double Wy => _s.WorldY;

        public RunStateViewSource(RunState state, MvTables tables)
        {
            _s = state;
            var c = tables.Climb.Config();
            _stepH = c != null ? c["stepH"] : 0;
            _stepD = c != null ? c["stepD"] : 0;
            _bump = ViewConfig.ClimbBump;
        }

        public CameraInput Camera
        {
            get
            {
                var input = new CameraInput { Mode = WorldViewMode.Run, Px = _s.P.X, Pz = _s.P.Z, Wy = Wy };
                var c = _s.Climb;
                if (_s.Phase == RunPhase.Climb && c != null)
                {
                    input.Mode = WorldViewMode.Climb;
                    CameraModel.ClimbPlayer(c.Pos, c.K, c.Z0, _stepH, _stepD, _bump, out double py, out double pz);
                    input.ClimbPy = py;
                    input.Pz = pz;
                }
                return input;
            }
        }

        /// <summary>옹벽(<see cref="IRunViewState.HasCliff"/>·<see cref="IRunViewState.Cliff"/>).</summary>
        public bool TryGetCliff(out CliffInfo cliff)
        {
            IRunViewState v = _s;
            var c = v.Cliff;
            cliff = new CliffInfo(c.Z, c.Low, c.High);
            return v.HasCliff;
        }
    }
}
