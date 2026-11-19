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
            // 빈 손패에 1등급 9장: 전달은 머지를 기다리지 않고 0.05초 간격으로 이어지고(사용자 2026-11-19 「오는데도 순서가 있으니까 그거 정도 딜레이로」),
            // 머지는 3번째 장(0.10초)부터 4 × (md 0.4968 + 쉼 0.04) → 약 2.25초(05 §5.3.3 예시 2.6초는 전달 뒤 머지 순서였을 때).
            var m = new WeaponHandModel(new WeaponHandTiming(), MergeN, 12);
            m.Reset(new int[13], 0);
            m.Deliver(9, 1);
            var (end, actions) = RunToIdle(m, 0);

            Assert.That(actions.Count(a => a.Kind == HandActionKind.Dealt), Is.EqualTo(9));
            Assert.That(actions.Count(a => a.Kind == HandActionKind.GatherStart), Is.EqualTo(4));
            Assert.That(actions.Count(a => a.Kind == HandActionKind.Merged), Is.EqualTo(4));
            Assert.That(end, Is.EqualTo(0.10 + 4 * (0.54 * 0.92 + 0.04)).Within(0.01), "3번째 전달 0.10 + 머지 4 × (md 0.4968 + 쉼 0.04), 나머지 전달은 머지와 함께 흐른다");
            var dealt = actions.Where(a => a.Kind == HandActionKind.Dealt).ToList();
            Assert.That(actions.IndexOf(dealt.Last()), Is.LessThan(actions.FindIndex(a => a.Kind == HandActionKind.Merged)), "9장은 첫 머지가 끝나기 전에 모두 올라온다");
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
        public void TwoTiersReady_MergesUpperGroupFirst()
        {
            // 사용자 2026-11-19 「순서대로 위에 3개 모이면 … 합쳐지고 밑에꺼도 재정렬」: 위(높은 등급) 묶음부터 합친다.
            var m = new WeaponHandModel(new WeaponHandTiming(), MergeN, 12);
            var counts = new int[3];
            counts[1] = MergeN;
            counts[2] = MergeN;
            m.Reset(counts, 0);
            var (_, actions) = RunToIdle(m, 0);
            Assert.That(actions.First(a => a.Kind == HandActionKind.Merged).Card.Tier, Is.EqualTo(3), "위 2등급 묶음이 먼저 3등급으로");
        }

        [Test]
        public void Deliver_OneAtATime_MergesBottomThreeBeforeNextCard_ThenChainsUp()
        {
            // 사용자 2026-11-19: 한 장씩 밑에 들어오고, 밑에서 3장이 되면 바로 합쳐진다. 같은 날 「하나 움직이고 대기가 너무 긴데 … 그거 정도 딜레이로」: 다음 장은 머지를 기다리지 않고 이어서 오른다.
            var m = new WeaponHandModel(new WeaponHandTiming(), MergeN, 12);
            m.Reset(new[] { 0, 2, 2 }, 0);   // 위에서부터 2, 2, 1, 1
            m.Deliver(4, 1);
            var (_, actions) = RunToIdle(m, 0);
            var kinds = string.Join(",", actions.Select(a => a.Kind == HandActionKind.Dealt ? "D" : a.Kind == HandActionKind.GatherStart ? "G" : "M"));
            Assert.That(kinds, Is.EqualTo("D,G,D,D,D,M,G,M,G,M"), "1장 → 곧바로 밑 3장 모으기, 그동안 다음 장들이 오름 → 결과가 위 묶음과 3장이 되면 위(높은 등급)부터 연쇄");
            var first = actions.First(a => a.Kind == HandActionKind.GatherStart);
            Assert.That(first.Gathered.Last(), Is.EqualTo(actions[0].Card.Id), "방금 들어와 3장이 된 장이 모으는 묶음에 든다");
            var later = actions.Where(a => a.Kind == HandActionKind.GatherStart).Skip(1).First();
            Assert.That(later.Gathered.First(), Is.LessThan(later.Gathered.Last()), "그 등급 맨 위(먼저 받은) 장부터 모은다");
            Assert.That(m.Cards.Select(c => c.Tier), Is.EqualTo(new[] { 3, 2 }));
        }

        [Test]
        public void DealHold_MergeWaitsUntilCardReachesBottomThenRisesToItsSlot()
        {
            // 사용자 2026-11-19: 다음 전달은 짧은 간격으로 이어지고, 3장이 되면 올라가는 이동이 끝나기를 기다리지 않고 바로 머지(「3개 배치되면 그때 바로 머지가 일어나야지」).
            var m = new WeaponHandModel(new WeaponHandTiming(), MergeN, 12) { DealStraightSec = 0.5f };
            m.Reset(new[] { 0, 1, 2 }, 0);   // 손패 [2, 2, 1]
            m.Deliver(1, 2);                 // 2등급 1장: 자리 = 1등급 위(맨 밑 아님) → 올라감
            var actions = new List<HandAction>();
            m.Step(0, 0.001, actions);
            Assert.That(actions.Select(a => a.Kind), Is.EqualTo(new[] { HandActionKind.Dealt, HandActionKind.GatherStart }), "3장이 되면 올라가는 중에도 바로 모은다");
            Assert.That(actions[0].Duration, Is.LessThan(0.1f), "다음 전달은 올라가는 연출을 기다리지 않고 짧은 간격으로 이어진다(사용자 2026-11-19 「딜레이만 살짝있게 연속으로」)");
            Assert.That(m.Settling, Is.EqualTo(0.5).Within(1e-5), "올라가는 시간은 알려만 준다");
            Assert.That(actions[1].Gathered, Is.EqualTo(m.Cards.Where(c => c.Tier == 2).Select(c => c.Id).ToArray()), "그 등급 맨 위 3장(위 → 아래)");

            var c = new WeaponHandModel(new WeaponHandTiming(), MergeN, 12) { DealStraightSec = 0.3f };
            c.Reset(new[] { 0, 0, 1 }, 0);   // 손패 [2]
            c.Deliver(2, 1);                 // 1등급 2장(머지 없음) → 짧은 간격으로 연달아 전달
            actions.Clear();
            c.Step(0, 0.001, actions);
            c.Step(0.1, 0.099, actions);
            Assert.That(actions.Count(a => a.Kind == HandActionKind.Dealt), Is.EqualTo(2), "올라가는 중에도 다음 카드가 바로 이어서 출발");

            var b = new WeaponHandModel(new WeaponHandTiming(), MergeN, 12) { DealStraightSec = 0.3f };
            b.Reset(new[] { 0, 0, 1 }, 0);   // 손패 [2]
            b.Deliver(1, 1);                 // 1등급: 자리 = 맨 밑 → 올라가지 않음
            actions.Clear();
            b.Step(0, 0.001, actions);
            Assert.That(b.Settling, Is.EqualTo(0.3).Within(1e-5), "곧장 위로 가는 시간");
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
