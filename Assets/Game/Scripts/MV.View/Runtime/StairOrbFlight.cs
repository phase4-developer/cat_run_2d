using System;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 계단 경험치 오브(<c>Obj_Stair_Orb</c>, MV-UI3-STAIRFX 노션 「World Object Attraction」) 비행의 입구.
    /// 월드 쪽(<see cref="StairOrbEmitter"/>·<see cref="StairOrb"/>)이 출발·도착을 알리고, HUD 계단 섹션이 도착 자리(화면 px)를 꽂고 게이지를 도착에 맞춰 올린다.
    /// MV.UI가 MV.View를 참조하므로 다리 없이 여기서 만난다(<see cref="GateRewardDisplay.Released"/>와 같은 방식).
    /// 게이지 값은 시뮬(`RunState.GaugeFill`)이 갖고, 여기에는 연출 개수만 있다.
    /// </summary>
    public static class StairOrbFlight
    {
        /// <summary>도착 자리(화면 px, 왼아래 원점). HUD 계단 섹션이 <c>Mid/Stair/OrbPos Anchor</c>로 꽂는다. 없거나 false면 오브는 퍼진 자리에서 사라진다.</summary>
        public delegate bool TargetFn(out Vector2 screen);

        public static TargetFn Target;

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

        /// <summary>도착 자리를 월드 점으로. <paramref name="depth"/> = 카메라 앞 거리(오브 크기를 유지하려고 지금 거리를 넘긴다).</summary>
        public static bool TryTargetWorld(Camera cam, float depth, out Vector3 world)
        {
            world = default;
            if (cam == null || Target == null || !Target(out var screen)) return false;
            world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Max(cam.nearClipPlane, depth)));
            return true;
        }

        /// <summary>시험 정리용.</summary>
        public static void ClearForTests()
        {
            Target = null;
            Launched = null;
            Arrived = null;
            InFlight = 0;
        }
    }
}
