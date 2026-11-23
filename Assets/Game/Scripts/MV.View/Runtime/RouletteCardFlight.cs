using System;
using System.Collections.Generic;

namespace MV.View
{
    /// <summary>
    /// 룰렛 확정 카드가 Card Pos로 나는 비행의 입구(MV-UI3-CLIMBFX3, 사용자 2026-11-23 「비행 시간도 프리팹 인스펙터로 조절하고 싶다고 하니 표가 아니라 프리팹값으로 교체」).
    /// HUD Card Pos 섹션이 비행을 맡으면 <see cref="Claim"/>하고, 날아 도착하면(또는 도착하지 못하고 끝나면) <see cref="RaiseArrived"/>로 카드 번호를 알린다.
    /// 런 프레임(Boot)이 그 번호를 시뮬(<c>RunSim.CardArrived</c>)에 넘겨 카드가 그 순간 적용된다. 아무도 맡지 않은 비행은 런 프레임이 다음 프레임에 바로 도착으로 친다.
    /// 카드 번호 = RouletteFly 이벤트 A(지금 룰렛 안의 카드 번호). MV.UI·MV.Boot가 MV.View를 참조하므로 여기서 만난다(<see cref="StairOrbFlight"/>와 같은 방식).
    /// </summary>
    public static class RouletteCardFlight
    {
        /// <summary>카드 하나 도착(카드 번호). 실제 적용은 시뮬이 하므로 화면은 듣기만 한다(시험·QA).</summary>
        public static event Action<int> Arrived;

        /// <summary>지금 런 번호. 런 프레임이 새 런마다 <see cref="BeginRun"/>으로 올린다. 지난 런 카드의 도착은 버린다.</summary>
        public static int Run { get; private set; }

        static readonly List<int> s_arrivals = new List<int>();
        static readonly HashSet<int> s_claimed = new HashSet<int>();

        /// <summary>이 카드 비행을 화면이 맡았다. 맡은 카드는 도착을 <see cref="RaiseArrived"/>로 정확히 한 번 알린다.</summary>
        public static void Claim(int cardIndex)
        {
            if (cardIndex >= 0) s_claimed.Add(cardIndex);
        }

        /// <summary>런 프레임: 이 카드를 누가 맡았는가(맡음 표시는 지운다).</summary>
        public static bool TakeClaim(int cardIndex) => s_claimed.Remove(cardIndex);

        /// <summary>카드 하나 도착(또는 도착하지 못하고 끝남). 지난 런·번호 없음은 버린다.</summary>
        public static void RaiseArrived(int run, int cardIndex)
        {
            if (cardIndex < 0 || run != Run) return;
            s_arrivals.Add(cardIndex);
            Arrived?.Invoke(cardIndex);
        }

        /// <summary>런 프레임: 쌓인 도착을 넘기고 비운다(시뮬 Tick 앞).</summary>
        public static void DrainArrivals(Action<int> to)
        {
            if (s_arrivals.Count == 0) return;
            foreach (int i in s_arrivals) to?.Invoke(i);
            s_arrivals.Clear();
        }

        /// <summary>새 런: 쌓인 도착·맡음을 비우고 런 번호를 올린다.</summary>
        public static void BeginRun()
        {
            Run++;
            s_arrivals.Clear();
            s_claimed.Clear();
        }

        /// <summary>시험 정리용.</summary>
        public static void ClearForTests()
        {
            Arrived = null;
            s_arrivals.Clear();
            s_claimed.Clear();
        }
    }
}
