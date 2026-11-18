using System;
using System.Collections.Generic;
using MV.Run;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 런 HUD 이벤트 입구(MV-UI3-HUD1, HUD_구조_설계안 3.1 「RunHudFeed」).
    /// 런 프레임이 <c>RunSim.DrainEvents()</c> 결과를 소리·이펙트·월드와 같은 목록, 같은 프레임에 넘긴다.
    /// HUD 섹션은 자기 <c>SetSubscribe</c>에서 <see cref="OnEvent"/>를 구독하고, 화면은 신호를 중계하지 않는다.
    /// 계속 보이는 값(레벨·재화·계단 수·게이지)은 이벤트가 아니라 <see cref="IRunHudSource"/> 스냅샷으로 읽는다.
    /// </summary>
    public static class MvHudEvents
    {
        /// <summary>이벤트 1개마다 한 번, 목록 순서대로.</summary>
        public static event Action<RunEvent> OnEvent;

        /// <summary>
        /// 이번 프레임 이벤트를 순서대로 전달한다. 구독자 하나가 예외를 내도 나머지 구독자와 다음 이벤트는 계속 받는다
        /// (HUD 오류가 런 프레임을 멈추지 않게).
        /// </summary>
        public static void OnRunEvents(IReadOnlyList<RunEvent> events)
        {
            var handlers = OnEvent;
            if (handlers == null || events == null) return;
            var list = handlers.GetInvocationList();
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                for (int h = 0; h < list.Length; h++)
                {
                    try { ((Action<RunEvent>)list[h])(e); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
            }
        }

        /// <summary>시험 정리용: 모든 구독을 끊는다.</summary>
        public static void ClearForTests() => OnEvent = null;
    }
}
