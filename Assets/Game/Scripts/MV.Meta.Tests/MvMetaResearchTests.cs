using MV.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MV.Meta.Tests
{
    /// <summary>
    /// 연구 요청(08 §8.5.3): 비용 먼저·끝 시각 저장, 끝 시각 경계 수령, 수령 1번, 비용 부족·잠김·다른 연구 중·MAX 거절.
    /// 비용·시간은 표(<c>440_research</c>)대로: 캣닢 10/20/30/40/50, 50/120/900/2400/4800초.
    /// </summary>
    public sealed class MvMetaResearchTests : MvMetaTestBase
    {
        static readonly long[] Cost = { 10, 20, 30, 40, 50 };
        static readonly long[] Seconds = { 50, 120, 900, 2400, 4800 };

        void OpenResearch() => SetContainer(MvSaveKeys.Progress, "\"area\":4"); // 0기반 4 = Area 5

        MvMetaResponse Start(int group) => Meta.Send(new MvMetaRequest(MvMetaRequestNames.StartResearch, new JObject { ["group"] = group }));
        MvMetaResponse Claim(int group) => Meta.Send(new MvMetaRequest(MvMetaRequestNames.ClaimResearch, new JObject { ["group"] = group }));

        [Test]
        public void Start_ThenClaim_FollowsTableCostAndSeconds_ToMax()
        {
            OpenResearch();
            SetWealth((MvWealthIds.Dia, 1000));
            for (int i = 0; i < Cost.Length; i++)
            {
                long before = W(MvWealthIds.Dia);
                MvMetaResponse started = Start(1);
                Assert.That(started.Ok, Is.True, $"Lv{i + 1} {started}");
                Assert.That(before - W(MvWealthIds.Dia), Is.EqualTo(Cost[i]), $"Lv{i + 1} 비용은 시작할 때 낸다");
                Assert.That(Store.Research.RunningKey, Is.EqualTo("1"));
                Assert.That(Store.Research.RunningFinishAt, Is.EqualTo(Clock.NowMs + Seconds[i] * 1000), $"Lv{i + 1} 끝 시각");
                Clock.NowMs = Store.Research.RunningFinishAt;
                Assert.That(Claim(1).Ok, Is.True, $"Lv{i + 1} 수령");
                Assert.That(Store.Research.GetLevel("1"), Is.EqualTo(i + 1));
                Assert.That(Store.Research.RunningKey, Is.Null);
            }
            long left = W(MvWealthIds.Dia);
            Assert.That(Start(1).Code, Is.EqualTo(MvMetaErrors.Max));
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(left), "MAX 거절은 재화를 바꾸지 않는다");
        }

        /// <summary>끝 시각 경계: 1ms 전이면 거절, 같거나 지나면 완료. 수령은 1번만(두 번째는 거절, 단계 그대로).</summary>
        [TestCase(-1, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(86_400_000, true)] // 앱이 꺼져 있던 동안 지나도 완료
        public void Claim_AtFinishBoundary_OnlyOnce(int offsetMs, bool ok)
        {
            OpenResearch();
            SetWealth((MvWealthIds.Dia, 100));
            Assert.That(Start(2).Ok, Is.True);
            Clock.NowMs = Store.Research.RunningFinishAt + offsetMs;

            MvMetaResponse first = Claim(2);
            Assert.That(first.Ok, Is.EqualTo(ok), first.ToString());
            if (!ok)
            {
                Assert.That(first.Code, Is.EqualTo(MvMetaErrors.NotDone));
                Assert.That(Store.Research.GetLevel("2"), Is.EqualTo(0));
                Assert.That(Store.Research.RunningKey, Is.EqualTo("2"), "거절은 진행을 지우지 않는다");
                return;
            }
            Assert.That(Store.Research.GetLevel("2"), Is.EqualTo(1));
            Assert.That(Claim(2).Code, Is.EqualTo(MvMetaErrors.NotDone), "수령 1번");
            Assert.That(Store.Research.GetLevel("2"), Is.EqualTo(1));
        }

        [TestCase(9, false)]
        [TestCase(10, true)]
        public void Start_CostBoundary(int dia, bool ok)
        {
            OpenResearch();
            SetWealth((MvWealthIds.Dia, dia));
            MvMetaResponse r = Start(3);
            Assert.That(r.Ok, Is.EqualTo(ok), r.ToString());
            if (ok)
            {
                Assert.That(W(MvWealthIds.Dia), Is.EqualTo(0));
                return;
            }
            Assert.That(r.Code, Is.EqualTo(MvMetaErrors.NotEnough));
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(dia), "부족 거절은 재화를 바꾸지 않는다");
            Assert.That(Store.Research.RunningKey, Is.Null, "부족 거절은 진행을 쓰지 않는다");
        }

        /// <summary>해금 05 행 5001009(Area 5, 0기반 <c>save.area + 1 ≥ 5</c>).</summary>
        [TestCase(3, false)]
        [TestCase(4, true)]
        public void Start_UnlockBoundary(int area, bool ok)
        {
            SetContainer(MvSaveKeys.Progress, "\"area\":" + area);
            SetWealth((MvWealthIds.Dia, 100));
            MvMetaResponse r = Start(1);
            Assert.That(r.Ok, Is.EqualTo(ok), r.ToString());
            if (!ok) Assert.That(r.Code, Is.EqualTo(MvMetaErrors.Locked));
        }

        [Test]
        public void Start_WhileAnotherRuns_IsBusy_AndNoInstantFinish()
        {
            OpenResearch();
            SetWealth((MvWealthIds.Dia, 100));
            Assert.That(Start(1).Ok, Is.True);
            Assert.That(Start(4).Code, Is.EqualTo(MvMetaErrors.Busy));
            Assert.That(Start(1).Code, Is.EqualTo(MvMetaErrors.Busy));
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(90));
            Assert.That(Claim(4).Code, Is.EqualTo(MvMetaErrors.NotDone), "진행 중이 아닌 계열은 받을 것이 없다");
        }

        [TestCase(0)]
        [TestCase(99)]
        public void Start_UnknownGroup_Rejected(int group)
        {
            OpenResearch();
            SetWealth((MvWealthIds.Dia, 100));
            MvMetaResponse r = Start(group);
            Assert.That(r.Code, Is.EqualTo(group == 0 ? MvMetaErrors.BadParams : MvMetaErrors.UnknownKey));
        }

        [Test]
        public void Claim_KeepsOtherGroupLevels()
        {
            OpenResearch();
            SetContainer(MvSaveKeys.Research, "\"lv\":{\"1\":3,\"5\":2}");
            SetWealth((MvWealthIds.Dia, 100));
            Assert.That(Start(5).Ok, Is.True);
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(70), "Lv3 비용 30");
            Clock.NowMs = Store.Research.RunningFinishAt;
            Assert.That(Claim(5).Ok, Is.True);
            Assert.That(Store.Research.GetLevel("1"), Is.EqualTo(3));
            Assert.That(Store.Research.GetLevel("5"), Is.EqualTo(3));
        }
    }
}
