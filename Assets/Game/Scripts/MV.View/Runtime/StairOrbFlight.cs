using System;
using System.Collections.Generic;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 계단 경험치 오브(<c>Obj_Stair_Orb</c>, MV-UI3-STAIRFX 노션 「World Object Attraction」) 비행의 입구.
    /// 월드 쪽(<see cref="StairOrbEmitter"/>)이 출발을 요청하고(<see cref="Launcher"/>, MV.Fx가 Framework 비행으로 UI Effect 루트에 재생),
    /// HUD 계단 섹션이 도착 자리(화면 px)와 오브를 놓을 UI 판(<see cref="UiRoot"/>)을 꽂는다.
    /// 오브가 도착하거나 도착하지 못하고 끝나면 <see cref="RaiseArrived"/>로 수령 번호를 알리고, 런 프레임(Boot)이 그 번호를 시뮬
    /// (<c>RunSim.ReceiveArrived</c>)에 넘겨 실제 게이지가 그 순간 오른다(MV-UI3-STAIRFX3, 사용자 2026-11-20 「이펙트가 도착해서 종료되는 시점에 처리해」).
    /// MV.UI·MV.Fx·MV.Boot가 MV.View를 참조하므로 다리 없이 여기서 만난다.
    /// </summary>
    public static class StairOrbFlight
    {
        /// <summary>도착 자리(화면 px, 왼아래 원점). HUD 계단 섹션이 <c>Mid/Stair/OrbPos Anchor</c>로 꽂는다. 없거나 false면 오브는 마지막 자리(퍼진 자리)에서 도착한다.</summary>
        public delegate bool TargetFn(out Vector2 screen);

        public static TargetFn Target;

        /// <summary>오브 하나 출발 요청(월드 쪽 → 재생기).</summary>
        public struct OrbLaunch
        {
            /// <summary>탑 오브 월드 자리·퍼질 월드 자리.</summary>
            public Vector3 From, Spread;
            /// <summary>월드 카메라(월드 자리를 화면에 투영).</summary>
            public Camera WorldCam;
            /// <summary>오브 프리팹 Addressables 주소(EffectManager 키).</summary>
            public string Key;
            /// <summary>UI 판 캔버스 정렬 순서에 더할 값(오브가 그 캔버스 위에 그려지게).</summary>
            public int SortingOffset;
            /// <summary>시뮬 수령 번호(WellFlush 이벤트 B)와 그때의 런 번호(<see cref="Run"/>). 도착 때 그대로 돌려준다.</summary>
            public int ReceiveId, Run;
        }

        public delegate bool LaunchFn(in OrbLaunch launch);

        /// <summary>
        /// 오브 재생기(MV.Fx <c>StairOrbFx</c>가 꽂는다). 받으면 true이고, 그 뒤 도착(또는 도착 못 하고 끝남)을 정확히 한 번
        /// <see cref="RaiseArrived"/>로 알린다. 없거나 false면 요청한 쪽이 바로 도착으로 친다.
        /// </summary>
        public static LaunchFn Launcher;

        /// <summary>오브를 놓을 UI 판(UIManager Effect 루트). HUD 계단 섹션이 꽂는다.</summary>
        public static Func<Transform> UiRoot;

        /// <summary>오브 하나 도착(수령 번호). 실제 게이지는 시뮬이 올리므로 화면은 듣기만 한다(시험·QA).</summary>
        public static event Action<int> Arrived;

        /// <summary>지금 런 번호. 런 프레임이 새 런마다 <see cref="BeginRun"/>으로 올린다. 지난 런 오브의 도착은 버린다.</summary>
        public static int Run { get; private set; }

        static readonly List<int> s_arrivals = new List<int>();
        static readonly HashSet<int> s_claimed = new HashSet<int>();

        /// <summary>출발 요청. 재생기가 받으면 true.</summary>
        public static bool TryLaunch(in OrbLaunch launch) => Launcher != null && Launcher(launch);

        /// <summary>
        /// 이 수령을 오브로 맡았다(<see cref="StairOrbEmitter"/>). 맡은 수령의 도착은 오브가 알리고,
        /// 아무도 맡지 않은 수령은 런 프레임이 바로 도착으로 친다(탑 뷰 없음·재생기 없음, 게이지가 멈추지 않게).
        /// </summary>
        public static void Claim(int receiveId)
        {
            if (receiveId > 0) s_claimed.Add(receiveId);
        }

        /// <summary>런 프레임: 이 수령을 누가 맡았는가(맡음 표시는 지운다).</summary>
        public static bool TakeClaim(int receiveId) => s_claimed.Remove(receiveId);

        /// <summary>오브 하나 도착(또는 도착하지 못하고 끝남). 지난 런·번호 없음은 버린다.</summary>
        public static void RaiseArrived(int run, int receiveId)
        {
            if (receiveId <= 0 || run != Run) return;
            s_arrivals.Add(receiveId);
            Arrived?.Invoke(receiveId);
        }

        /// <summary>런 프레임: 쌓인 도착을 넘기고 비운다(시뮬 Tick 앞).</summary>
        public static void DrainArrivals(Action<int> to)
        {
            if (s_arrivals.Count == 0) return;
            foreach (int id in s_arrivals) to?.Invoke(id);
            s_arrivals.Clear();
        }

        /// <summary>새 런: 쌓인 도착·맡음을 비우고 런 번호를 올린다(날고 있던 지난 런 오브의 도착은 버린다).</summary>
        public static void BeginRun()
        {
            Run++;
            s_arrivals.Clear();
            s_claimed.Clear();
        }

        /// <summary>시험 정리용.</summary>
        public static void ClearForTests()
        {
            Target = null;
            UiRoot = null;
            Launcher = null;
            Arrived = null;
            s_arrivals.Clear();
            s_claimed.Clear();
        }
    }
}
