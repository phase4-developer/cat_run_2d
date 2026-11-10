using NUnit.Framework;

namespace MV.View.Tests
{
    /// <summary>
    /// MV-VIEW 4차: 투사체·창·회전 장난감 순수 계산(02 §2.1.4 「던진 장난감 L3804, 회전 장난감 L3800」·「창 궤적 L3802~3803」 행).
    /// 골든 값은 정본 draw2D L3800~3804·syncScene L2765 식을 Python(double)으로 돌린 결과다.
    /// </summary>
    public sealed class ProjLayoutTests
    {
        [Test]
        public void SpinAngles_AreNegativeRealtimeMsFormula()
        {
            // 던진 장난감 −(ms/110 + sz), 회전 장난감 −(ms/180 + i·2)
            Assert.That(ProjLayout.ProjAngle(12.345, 3.4), Is.EqualTo(-115.62727272727274).Within(1e-9));
            Assert.That(ProjLayout.BladeAngle(12.345, 2), Is.EqualTo(-72.58333333333333).Within(1e-9));
            // 도 변환은 2π로 접어 −180~180, 큰 실시간에서도 같은 각
            Assert.That(ProjLayout.ToDegrees(-System.Math.PI / 2), Is.EqualTo(-90f).Within(1e-4f));
            Assert.That(ProjLayout.ToDegrees(-System.Math.PI / 2 - 2 * System.Math.PI * 1000), Is.EqualTo(-90f).Within(1e-3f));
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(6, 6)]
        [TestCase(8, 8)]
        [TestCase(9, 8)]
        public void ProjSpriteName_ReadsItemTableAndClampsTier(int tier, int drawn)
        {
            string expected = "World_Proj_" + drawn;
            var names = ProjLayout.NamesByTier(ViewTestTables.Load());
            Assert.That(ProjLayout.ProjSpriteName(tier, names), Is.EqualTo(expected));
            Assert.That(ProjLayout.ProjSpriteName(tier, null), Is.EqualTo(expected), "표 없음 = 기본 이름");
        }

        [Test]
        public void SpearHeadAndBladePattern_FollowElement()
        {
            Assert.That(ProjLayout.SpearHeadName("fire"), Is.EqualTo("World_Proj_SpearHead_Fire"));
            Assert.That(ProjLayout.SpearHeadName("bolt"), Is.EqualTo("World_Proj_SpearHead_Bolt"));
            Assert.That(ProjLayout.SpearHeadName(null), Is.EqualTo("World_Proj_SpearHead_Bolt"));
            Assert.That(ProjLayout.SpearColor("fire"), Is.EqualTo("#ffb03a"));
            Assert.That(ProjLayout.SpearColor("bolt"), Is.EqualTo("#ff3a4a"));
            int[] other = { 1, 6, 1, 1, 6 };
            for (int i = 0; i < other.Length; i++)
            {
                Assert.That(ProjLayout.BladeTier(i, false), Is.EqualTo(other[i]), "그 외 " + i);
                Assert.That(ProjLayout.BladeTier(i, true), Is.EqualTo(8), "불 " + i);
            }
        }

        [Test]
        public void SpearTail_Is2Point8MetersBehind()
        {
            ProjLayout.SpearTailPoint(1.25, 0.95, 40, out double x, out double y, out double z);
            Assert.That(x, Is.EqualTo(1.25));
            Assert.That(y, Is.EqualTo(0.95));
            Assert.That(z, Is.EqualTo(37.2).Within(1e-12));
        }

        [Test]
        public void BladePos_MatchesReferenceOrbit()
        {
            // (spinA, i) → (x, y, z), 플레이어 (0.3, 10)
            double[,] g =
            {
                { 0.0, 0, 1.8, 1.0, 10.0 },
                { 0.5, 1, -0.9809789648991944, 0.8667023477486399, 10.780444034819785 },
                { 7.25, 2, 0.9432702959232617, 0.8837768561168614, 8.644934198504442 },
            };
            for (int r = 0; r < g.GetLength(0); r++)
            {
                ProjLayout.BladePos(g[r, 0], (int)g[r, 1], 0.3, 10, out double x, out double y, out double z);
                Assert.That(x, Is.EqualTo(g[r, 2]).Within(1e-12), "x " + r);
                Assert.That(y, Is.EqualTo(g[r, 3]).Within(1e-12), "y " + r);
                Assert.That(z, Is.EqualTo(g[r, 4]).Within(1e-12), "z " + r);
            }
        }

        [Test]
        public void DepthBias_BladeInFrontOfProjInFrontOfCat()
        {
            var cfg = ViewTestTables.Config();
            Assert.That(cfg.SortProj, Is.EqualTo(-0.02).Within(1e-12));
            Assert.That(cfg.SortBlade, Is.EqualTo(-0.03).Within(1e-12));
            double q = cfg.SortQuant;   // 64칸/m: 0.01 m 차가 갈리는 깊이를 고른다
            int cat = DepthMath.SortingOrder(10.01, 0, DepthMath.RankUnit, q);
            int proj = DepthMath.SortingOrder(10.01, cfg.SortProj, DepthMath.RankUnit, q);
            int blade = DepthMath.SortingOrder(10.01, cfg.SortBlade, DepthMath.RankUnit, q);
            Assert.That(proj, Is.GreaterThan(cat));
            Assert.That(blade, Is.GreaterThan(proj));
        }
    }
}
