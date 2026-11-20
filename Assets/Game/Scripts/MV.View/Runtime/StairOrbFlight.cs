using System;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 계단 경험치 오브(<c>Obj_Stair_Orb</c>, MV-UI3-STAIRFX 노션 「World Object Attraction」) 비행의 입구.
    /// 월드 쪽(<see cref="StairOrbEmitter"/>)이 출발을 요청하고(<see cref="Launcher"/>, MV.Fx가 EffectManager로 UI Effect 루트에 재생),
    /// HUD 계단 섹션이 도착 자리(화면 px)와 오브를 놓을 UI 판(<see cref="UiRoot"/>)을 꽂고 게이지를 도착에 맞춰 올린다(MV-UI3-STAIRFX2).
    /// MV.UI·MV.Fx가 MV.View를 참조하므로 다리 없이 여기서 만난다(<see cref="GateRewardDisplay.Released"/>와 같은 방식).
    /// 게이지 값은 시뮬(`RunState.GaugeFill`)이 갖고, 여기에는 연출 개수만 있다.
    /// </summary>
    public static class StairOrbFlight
    {
        /// <summary>도착 자리(화면 px, 왼아래 원점). HUD 계단 섹션이 <c>Mid/Stair/OrbPos Anchor</c>로 꽂는다. 없거나 false면 오브는 퍼진 자리에서 사라진다.</summary>
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
        }

        public delegate bool LaunchFn(in OrbLaunch launch);

        /// <summary>오브 재생기(MV.Fx <c>StairOrbFx</c>가 꽂는다). 없거나 false면 오브 없이 바로 도착으로 친다.</summary>
        public static LaunchFn Launcher;

        /// <summary>오브를 놓을 UI 판(UIManager Effect 루트). HUD 계단 섹션이 꽂는다.</summary>
        public static Func<Transform> UiRoot;

        /// <summary>출발 요청. 재생기가 받으면 true(도착은 재생기가 <see cref="RaiseArrived"/>로 알린다).</summary>
        public static bool TryLaunch(in OrbLaunch launch) => Launcher != null && Launcher(launch);

        /// <summary>오브 묶음 출발(개수). 한 번 비울 때 한 번.</summary>
        public static event Action<int> Launched;

        /// <summary>오브 하나 도착(중간에 치워진 오브도 도착으로 친다. 게이지가 멈춰 있지 않게).</summary>
        public static event Action Arrived;

        /// <summary>아직 도착하지 않은 오브 수.</summary>
        public static int InFlight { get; private set; }

        public static void RaiseLaunched(int count)
        {
            if (count <= 0) return;
            InFlight += count;
            Launched?.Invoke(count);
        }

        public static void RaiseArrived()
        {
            if (InFlight > 0) InFlight--;
            Arrived?.Invoke();
        }

        /// <summary>시험 정리용.</summary>
        public static void ClearForTests()
        {
            Target = null;
            UiRoot = null;
            Launcher = null;
            Launched = null;
            Arrived = null;
            InFlight = 0;
        }
    }
}
