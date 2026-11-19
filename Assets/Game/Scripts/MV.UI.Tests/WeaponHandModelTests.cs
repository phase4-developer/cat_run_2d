using System.Collections.Generic;
using System.Linq;
using MV.UI;
using NUnit.Framework;

namespace MV.UI.Tests
{
    /// <summary>
    /// 손패 표시 모델(MV-UI3-HUD3, 04 §4.10·05 §5.3.2). 시간은 기본 연출 값(프리팹 직렬화 기본값과 같은 <see cref="WeaponHandTiming"/>)으로 본다.
    /// 보장 범위: 순서·시간·결과 손패. 슬롯 연출과 화면은 무기 섹션 계약과 사람 확인(NOT_RUN)으로 본다.
    /// </summary>
    public sealed class WeaponHandModelTests
    {
        const int MergeN = 3;

        static (double end, List<HandAction> actions) RunToIdle(WeaponHandModel m, double start, double dt = 0.001, double limit = 30)
        {
            var actions = new List<HandAction>();
            double now = start;
            // 첫 Step은 대기열이 찬 상태에서 일을 시작한다.
            while (now - start < limit)
            {
                m.Step(now, dt, actions);
                if (m.Idle) return (now, actions);
                now += dt;
            }
            Assert.Fail("손패가 끝나지 않음");
            return (now, actions);
        }

        [Test]
        public void SpecExample_NineTierOne_DealsThenFourMerges_AboutTwoPointSixSeconds()
        {
            // 05 §5.3.3 [계산]: 빈 손패에 1등급 9장 → 전달 9×0.05 + 머지 4×(0.497+0.04) ≈ 2.6초.
            var m = new WeaponHandModel(new WeaponHandTiming(), MergeN, 12);
            m.Reset(new int[13], 0);
            m.Deliver(9, 1);
            var (end, actions) = RunToIdle(m, 0);

            Assert.That(actions.Count(a => a.Kind == HandActionKind.Dealt), Is.EqualTo(9));
            Assert.That(actions.Count(a => a.Kind == HandActionKind.GatherStart), Is.EqualTo(4));
            Assert.That(actions.Count(a => a.Kind == HandActionKind.Merged), Is.EqualTo(4));
            Assert.That(end, Is.EqualTo(0.45 + 4 * (0.54 * 0.92 + 0.04)).Within(0.01), "전달 0.45 + 머지 4 × (md 0.4968 + 쉼 0.04)");
            Assert.That(m.Cards.Select(c => c.Tier), Is.EqualTo(new[] { 3 }), "9 → 3 → 1");
            Assert.That(m.TopTier, Is.EqualTo(3));
            var gather = actions.First(a => a.Kind == HandActionKind.GatherStart);
            Assert.That(gather.MoveSec, Is.EqualTo(gather.Duration * 0.9f).Within(1e-5), "모으는 이동 = md × 0.9");
        }

        [Test]
        public void Reset_SortsHighTierFirst_SameTierOldestFirst()
        {
            var m = new WeaponHandModel(new WeaponHandTiming(), MergeN, 12);
            m.Reset(new[] { 0, 2, 1 }, 0);
            Assert.That(m.Cards.Select(c => c.Tier), Is.EqualTo(new[] { 2, 1, 1 }));
            Assert.That(m.Cards[1].Id, Is.LessThan(m.Cards[2].Id), "같은 등급은 먼저 받은 것이 앞");
            Assert.That(m.Idle, Is.True);
        }

        [Test]
        public void Cascade_MergesLowestTierFirst_ThenChains()
        {
            var m = new WeaponHandModel(new WeaponHandTiming(), MergeN, 12);
            m.Reset(new[] { 0, 2, 2 }, 0);
            var tierOneIds = m.Cards.Where(c => c.Tier == 1).Select(c => c.Id).ToArray();
            m.Deliver(1, 1);
            var (_, actions) = RunToIdle(m, 0);

            var gathers = actions.Where(a => a.Kind == HandActionKind.GatherStart).ToList();
            Assert.That(gathers.Count, Is.EqualTo(2), "1등급 머지 → 2등급 3장 → 연쇄 머지");
            Assert.That(gathers[0].Gathered.Take(2), Is.EqualTo(tierOneIds), "가장 낮은 등급의 먼저 받은 장부터");
            Assert.That(m.Cards.Select(c => c.Tier), Is.EqualTo(new[] { 3 }));
            Assert.That(actions.Last().TopTierUp, Is.True, "장착 등급 상승");
        }

        [Test]
        public void Deliver_OneAtATime_MergesBottomThreeBeforeNextCard_ThenChainsUp()
        {
            // 사용자 2026-11-19: 한 장씩 밑에 들어오고, 밑에서 3장이 되면 바로 합쳐지고, 그게 위 카드와 3장이 되면 또 합쳐진다(다 넣고 한꺼번에 머지하지 않는다).
            var m = new WeaponHandModel(new WeaponHandTiming(), MergeN, 12);
            m.Reset(new[] { 0, 2, 2 }, 0);   // 위에서부터 2, 2, 1, 1
            m.Deliver(4, 1);
            var (_, actions) = RunToIdle(m, 0);
            var kinds = string.Join(",", actions.Select(a => a.Kind == HandActionKind.Dealt ? "D" : a.Kind == HandActionKind.GatherStart ? "G" : "M"));
            Assert.That(kinds, Is.EqualTo("D,G,M,G,M,D,D,D,G,M"), "1장 → 밑 3장 머지 → 위 2등급과 연쇄 → 다음 장들");
            var first = actions.First(a => a.Kind == HandActionKind.GatherStart);
            var bottomThree = first.Gathered;
            Assert.That(bottomThree.Last(), Is.EqualTo(actions[0].Card.Id), "방금 맨 밑에 들어온 장이 모으는 묶음에 든다");
            Assert.That(m.Cards.Select(c => c.Tier), Is.EqualTo(new[] { 3, 2 }));
        }

        [Test]
        public void DealHold_MergeWaitsUntilCardReachesBottomThenRisesToItsSlot()
        {
            // 사용자 2026-11-19: 새 카드는 무조건 맨 밑 → 자기 등급 자리로(맨 밑이 아니면 왼쪽 통로로 우회) → 그 뒤 3장이면 머지.
            var m = new WeaponHandModel(new WeaponHandTiming(), MergeN, 12) { DealStraightSec = 0.3f, DealDetourSec = 0.5f };
            m.Reset(new[] { 0, 1, 2 }, 0);   // 손패 [2, 2, 1]
            m.Deliver(1, 2);                 // 2등급 1장: 자리 = 1등급 위(맨 밑 아님) → 올라감
            var actions = new List<HandAction>();
            m.Step(0, 0.001, actions);
            Assert.That(actions.Single().Kind, Is.EqualTo(HandActionKind.Dealt));
            Assert.That(actions[0].Duration, Is.EqualTo(0.5f).Within(1e-5), "자리가 맨 밑이 아니면 통로 우회 시간");
            Assert.That(m.Cards.Select(c => c.Tier), Is.EqualTo(new[] { 2, 2, 2, 1 }));
            actions.Clear();
            m.Step(0.49, 0.489, actions);
            Assert.That(actions, Is.Empty, "올라가는 중에는 머지하지 않는다");
            m.Step(0.51, 0.02, actions);
            Assert.That(actions.Single().Kind, Is.EqualTo(HandActionKind.GatherStart), "자리를 찾은 뒤 3장 머지");

            var b = new WeaponHandModel(new WeaponHandTiming(), MergeN, 12) { DealStraightSec = 0.3f, DealDetourSec = 0.5f };
            b.Reset(new[] { 0, 0, 1 }, 0);   // 손패 [2]
            b.Deliver(1, 1);                 // 1등급: 자리 = 맨 밑 → 올라가지 않음
            actions.Clear();
            b.Step(0, 0.001, actions);
            Assert.That(actions.Single().Duration, Is.EqualTo(0.3f).Within(1e-5), "자리가 맨 밑이면 곧장 위로만");
        }

        [Test]
        public void MaxTier_StacksWithoutMerging()
        {
            var m = new WeaponHandModel(new WeaponHandTiming(), MergeN, 2);
            m.Reset(new int[3], 0);
            m.Deliver(3, 2);
            var (_, actions) = RunToIdle(m, 0);
            Assert.That(actions.Any(a => a.Kind == HandActionKind.GatherStart), Is.False, "최고 단계는 머지하지 않는다");
            Assert.That(m.Cards.Select(c => c.Tier), Is.EqualTo(new[] { 2, 2, 2 }));
        }

        [Test]
        public void Unit_NoWork_IsUnitMax_ManyWork_ClampsToUnitMin()
        {
            var timing = new WeaponHandTiming();
            var m = new WeaponHandModel(timing, MergeN, 12);
            m.Reset(new int[13], 0);
            Assert.That(m.Unit(0), Is.EqualTo(timing.unitMax), "U = 0 → unitMax");
            m.Deliver(500, 1);
            Assert.That(m.Unit(0), Is.EqualTo(timing.unitMin), "일이 많으면 하한");
            m.Work(out int deals, out int merges);
            Assert.That(deals, Is.EqualTo(500));
            Assert.That(merges, Is.EqualTo(166 + 55 + 18 + 6 + 2), "500 → 166 → 55 → 18 → 6 → 2, 단계마다 ⌊c/3⌋");
        }
    }
}
