using System;

namespace MV.UI
{
    /// <summary>
    /// 플레이어 HUD 배속 토글(MV-UI2-HUD)의 연결 지점. 게임 루프가 런 배속(<c>RunSetup.TimeScale</c>, 시뮬이 매 틱 읽는 기존 배속)을 바꾸는 함수를 넣는다.
    /// 테스트 패널 ×1/×2/×3(<see cref="MvDebugHooks.SetTimeScale"/>)과 같은 값을 바꾼다. 비면 토글은 아무 일도 하지 않는다.
    /// </summary>
    public static class MvRunSpeed
    {
        public static Action<int> Set;

        /// <summary>토글 다음 배속: 상태 수(디자이너 토글의 1x·2x 자식 수)까지 1씩 올리고, 그 위면 1로. 상태 수 밖(테스트 패널 ×3)이면 1로.</summary>
        public static int Next(int current, int states) => current >= 1 && current < states ? current + 1 : 1;

        /// <summary>표시할 상태 칸(0부터): 배속을 1~상태 수로 자른 값 − 1.</summary>
        public static int StateIndex(int timeScale, int states) => states <= 0 ? -1 : Math.Max(1, Math.Min(states, timeScale)) - 1;
    }
}
