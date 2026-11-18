using MV.View;

namespace MV.Boot
{
    /// <summary>월드 뷰 공급원: 런이 있으면 그 상태, 없으면 로비 자세(<see cref="MvGameLoop"/>가 런 시작·로비 진입 때 바꾼다).</summary>
    sealed class MvLoopViewSource : IWorldViewSource
    {
        public RunStateViewSource Run;

        public CameraInput Camera => Run != null ? Run.Camera : new CameraInput { Mode = WorldViewMode.Lobby };

        public bool TryGetCliff(out CliffInfo cliff)
        {
            if (Run != null) return Run.TryGetCliff(out cliff);
            cliff = default;
            return false;
        }
    }
}
