namespace MV.Run
{
    /// <summary>
    /// 옹벽(정본 `R.cliff = {z, low, high}`, shiftWorld L2227). 계단을 오를 때 착지 지점 zEnd에 생기는 세로 면.
    /// 그 z보다 앞(작은 z) 지면 높이는 <see cref="Low"/>, 면은 <see cref="Low"/>~<see cref="High"/>(02 §2.4.3, groundY L2826).
    /// </summary>
    public readonly struct RunCliff
    {
        /// <summary>옹벽 z(= 계단 zEnd).</summary>
        public readonly double Z;
        /// <summary>오르기 전 지면 높이(그때의 WY).</summary>
        public readonly double Low;
        /// <summary>오른 뒤 지면 높이(WY + dH).</summary>
        public readonly double High;

        public RunCliff(double z, double low, double high) { Z = z; Low = low; High = high; }

        public override string ToString() => $"cliff(z={Z}, low={Low}, high={High})";
    }

    /// <summary>
    /// MV.View가 읽는 런 표시 상태(10 §10.4: View는 Run을 읽기만). 값 복사·구조체만 돌려주어 할당이 없고
    /// 읽어도 상태·규칙 난수·이벤트 열이 바뀌지 않는다. 구현은 <see cref="RunState"/>.
    /// </summary>
    public interface IRunViewState
    {
        /// <summary>
        /// 지면 높이 `WY`(04 §4.8.3, L937). 런 시작 0, 계단 오르기가 끝날 때(endClimb) +dH.
        /// 오르기 중(<see cref="RunPhase.Climb"/>)에는 오르기 전 값 그대로다. 오르기 중 플레이어 높이(py)는 View가 <see cref="RunState.Climb"/>로 계산한다(02 §2.2.1).
        /// </summary>
        double WorldY { get; }

        /// <summary>이 런에서 옹벽이 생겼는가(정본 `R.cliff` 존재). 첫 계단 오르기 시작 전에는 false.</summary>
        bool HasCliff { get; }

        /// <summary>
        /// 가장 최근 옹벽(정본 `R.cliff`). 정본은 1개만 보관하고 계단 오르기를 시작할 때마다 덮어쓴다(이전 옹벽 그림도 climbG와 함께 지운다).
        /// <see cref="HasCliff"/>가 false면 기본값.
        /// </summary>
        RunCliff Cliff { get; }
    }
}
