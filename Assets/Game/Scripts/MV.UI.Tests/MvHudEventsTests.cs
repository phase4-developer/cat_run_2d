using System;
using System.Collections.Generic;
using MV.Run;
using MV.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MV.UI.Tests
{
    /// <summary>
    /// HUD 이벤트 입구(MV-UI3-HUD1). 연결 계약: 런 프레임이 넘긴 목록을 이벤트마다 한 번, 순서대로 전달하고,
    /// 한 구독자의 예외가 다른 구독자·다음 이벤트를 막지 않는다.
    /// 보장 범위: 정적 입구의 전달 규칙만. <c>MvRunFrame</c>이 실제로 부르는지는 런 PlayMode·화면 확인으로 본다.
    /// </summary>
    public class MvHudEventsTests
    {
        [SetUp, TearDown]
        public void Clear() => MvHudEvents.ClearForTests();

        static List<RunEvent> Three() => new List<RunEvent>
        {
            new RunEvent(RunEventType.WeaponDeliver, a: 3, b: 1),
            new RunEvent(RunEventType.ClimbTierUp, a: 2, b: 7),
            new RunEvent(RunEventType.CardApplied, a: 1, b: 42),
        };

        [Test]
        public void EachEvent_DeliveredOnce_InOrder()
        {
            var got = new List<RunEventType>();
            MvHudEvents.OnEvent += e => got.Add(e.Type);
            MvHudEvents.OnRunEvents(Three());
            CollectionAssert.AreEqual(new[] { RunEventType.WeaponDeliver, RunEventType.ClimbTierUp, RunEventType.CardApplied }, got);
        }

        [Test]
        public void Unsubscribed_ReceivesNothing()
        {
            int n = 0;
            Action<RunEvent> h = _ => n++;
            MvHudEvents.OnEvent += h;
            MvHudEvents.OnEvent -= h;
            MvHudEvents.OnRunEvents(Three());
            Assert.AreEqual(0, n);
        }

        [Test]
        public void ThrowingSubscriber_DoesNotBlockOthersOrLaterEvents()
        {
            int other = 0;
            MvHudEvents.OnEvent += _ => throw new InvalidOperationException("hud test");
            MvHudEvents.OnEvent += _ => other++;
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("hud test"));
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("hud test"));
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("hud test"));
            MvHudEvents.OnRunEvents(Three());
            Assert.AreEqual(3, other);
        }

        [Test]
        public void NoSubscribersOrNullList_IsQuiet()
        {
            Assert.DoesNotThrow(() => MvHudEvents.OnRunEvents(Three()));
            MvHudEvents.OnEvent += _ => { };
            Assert.DoesNotThrow(() => MvHudEvents.OnRunEvents(null));
        }
    }
}
