using System;
using System.Collections.Generic;
using UnityEngine;

namespace MV.UI
{
    /// <summary>HUD 비행 대상. 순서·이름은 <c>MV.Fx.FlyTarget</c>와 같다(Boot 다리가 이름으로 확인한다).</summary>
    public enum HudFlyTarget { Gold, Soul, Dia, Stair, Hand, Gauge }

    /// <summary>
    /// 한 비행 대상의 HUD 쪽(그림·도착 자리). 섹션이 자기 대상을 맡아 <see cref="MvHudFly.Register"/>로 건다.
    /// 화면 좌표는 px, 왼아래 원점(<c>MvFlyLayer</c>와 같다).
    /// </summary>
    public interface IHudFlyHandler
    {
        /// <summary>도착 자리(화면 px 가운데·크기). 없으면 false.</summary>
        bool TryGetRect(out Vector2 center, out Vector2 size);
        void Show(int id, int amount);
        void Place(int id, Vector2 screen, float scale, float alpha);
        void Hide(int id);
    }

    /// <summary>
    /// HUD 비행 입구(MV-UI3-HUD4). 비행 시간·곡선·지연은 <c>MV.Fx.MvFlyLayer</c>가 갖고, 그림과 도착 자리는 HUD 섹션이 갖는다.
    /// MV.UI는 MV.Fx를 참조하지 않으므로 Boot의 다리(<c>MvHudFlyBridge</c>)가 둘을 잇는다.
    /// </summary>
    public static class MvHudFly
    {
        static readonly IHudFlyHandler[] s_handlers = new IHudFlyHandler[Enum.GetValues(typeof(HudFlyTarget)).Length];

        /// <summary>대상별 날고 있는(지연 포함) 비행 수. 다리가 꽂는다. 없으면 비행 층이 없는 것(시험·연출 미설치).</summary>
        public static Func<HudFlyTarget, int> InFlight;

        /// <summary>비행 도착(대상, 값). 다리가 비행 층 도착을 넘긴다.</summary>
        public static event Action<HudFlyTarget, int> Arrived;

        /// <summary>
        /// 월드 카드가 HUD 카드로 바뀌는 순간(대상, 카드별 화면 자리 px 왼아래 원점). 무기문 위 카드 묶음을 통과하면 Boot 다리가 넘긴다.
        /// 받는 섹션은 같은 자리·같은 크기로 UI 카드를 세워 날린다(알파 변화 없음). 목록은 그 자리에서 읽고 보관하지 않는다.
        /// </summary>
        public static event Action<HudFlyTarget, IReadOnlyList<Rect>> Launched;

        public static void RaiseLaunched(HudFlyTarget target, IReadOnlyList<Rect> cards) => Launched?.Invoke(target, cards);

        public static void Register(HudFlyTarget target, IHudFlyHandler handler) => s_handlers[(int)target] = handler;

        /// <summary>자기가 건 것만 푼다(다른 섹션이 이미 바꿔 걸었으면 그대로 둔다).</summary>
        public static void Unregister(HudFlyTarget target, IHudFlyHandler handler)
        {
            if (s_handlers[(int)target] == handler) s_handlers[(int)target] = null;
        }

        public static IHudFlyHandler Handler(HudFlyTarget target) => s_handlers[(int)target];

        /// <summary>날고 있는 수. 비행 층이 없으면 0.</summary>
        public static int CountInFlight(HudFlyTarget target) => InFlight != null ? InFlight(target) : 0;

        public static void RaiseArrived(HudFlyTarget target, int amount) => Arrived?.Invoke(target, amount);

        /// <summary>시험 정리용.</summary>
        public static void ClearForTests()
        {
            Array.Clear(s_handlers, 0, s_handlers.Length);
            InFlight = null;
            Arrived = null;
            Launched = null;
        }
    }
}
