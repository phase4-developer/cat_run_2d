using System.Collections.Generic;
using UnityEngine;

namespace MV.Fx
{
    /// <summary>HUD 비행 대상(정본 `#hGold`·`#hSoul`·`#hDia`·`#hStair`·`#wHand`·`#gBar`). 위치는 MV-UI-HUD가 <see cref="IMvHudAnchors"/>로 준다.</summary>
    public enum FlyTarget { Gold, Soul, Dia, Stair, Hand, Gauge }

    /// <summary>숫자·문구 팝업 1개(06 §6.7). World = 게임 좌표.</summary>
    public struct PopRequest
    {
        public Vector3 World;
        public string Text;
        public Color32 Color;
        public float Size;
    }

    /// <summary>HUD 보상 비행 1개(06 §6.8 `flyTo`). DelayMs 뒤 출발(동료 관문 70ms 간격 등). Amount는 도착 콜백 값(계단 +n 등).</summary>
    public struct FlyRequest
    {
        public FlyTarget Target;
        public Vector3 World;
        public int DelayMs;
        public int Amount;
    }

    /// <summary>화면 플래시 요청(06 §6.9). Color null이면 흰색. 표시는 MV-UI-HUD(층 순서 05).</summary>
    public struct FlashRequest
    {
        public float Peak;
        public int HoldMs;
        public string Color;
    }

    /// <summary>RunEvent 묶음 1회를 라우팅한 결과. 매 프레임 비우고 다시 쓴다(할당 없음).</summary>
    public sealed class MvFxOutput
    {
        /// <summary>재생한 04_effect 그룹 key(정본 호출 지점 순서). 확률에 떨어진 것은 빠진다.</summary>
        public readonly List<string> Groups = new List<string>(32);
        public readonly List<FxSpawn> Spawns = new List<FxSpawn>(64);
        public readonly List<PopRequest> Pops = new List<PopRequest>(16);
        public readonly List<FlyRequest> Flies = new List<FlyRequest>(8);
        public readonly List<FlashRequest> Flashes = new List<FlashRequest>(2);
        /// <summary>흔들림(amp, time). MV.Run이 낸 Shake 이벤트 값 그대로(최댓값 병합은 CameraRig).</summary>
        public readonly List<Vector2> Shakes = new List<Vector2>(4);
        /// <summary>우다다 단계(ClimbWarpStage). −1 = 변화 없음.</summary>
        public int WarpStage = -1;
        public bool WarpBegin, WarpEnd;

        public void Clear()
        {
            Groups.Clear(); Spawns.Clear(); Pops.Clear(); Flies.Clear(); Flashes.Clear(); Shakes.Clear();
            WarpStage = -1; WarpBegin = false; WarpEnd = false;
        }
    }
}
