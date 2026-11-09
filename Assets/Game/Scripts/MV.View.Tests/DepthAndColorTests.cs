using NUnit.Framework;

namespace MV.View.Tests
{
    /// <summary>티켓 02 순수 계산: 뷰 깊이·안개 알파·정렬 순서(02 §2.3·§2.4), 색 도우미(02 §2.6.1).</summary>
    public sealed class DepthAndColorTests
    {
        [Test]
        public void Depth_IsForwardProjectionNotEuclidean()
        {
            var cam = new Vector3d(0, 13, -9);
            var fwd = DepthMath.Forward(cam, new Vector3d(0, 0.5, 5.5));
            var p = new Vector3d(10, 0.7, 0);   // 옆으로 10 m 비켜도 전방 깊이는 같다
            Assert.That(DepthMath.Depth(p, cam, fwd), Is.EqualTo(DepthMath.Depth(new Vector3d(0, 0.7, 0), cam, fwd)).Within(1e-12));
        }

        [TestCase(10.0, 1.0)]
        [TestCase(34.0, 1.0)]
        [TestCase(59.0, 0.5)]
        [TestCase(84.0, 0.0)]
        [TestCase(120.0, 0.0)]
        public void FogAlpha_MatchesReferenceFogK(double d, double alpha)
        {
            var cfg = ViewTestTables.Config();
            Assert.That(DepthMath.FogAlpha(d, cfg.FogStart, cfg.FogRange), Is.EqualTo(alpha).Within(1e-12));
        }

        [Test]
        public void SortingOrder_FarDrawsFirstAndBiasAndRankSeparate()
        {
            var cfg = ViewTestTables.Config();
            double q = cfg.SortQuant;
            Assert.That(DepthMath.SortingOrder(10, 0, DepthMath.RankObject, q), Is.EqualTo(-640 * 4 + 1));
            Assert.That(DepthMath.SortingOrder(30, 0, 0, q), Is.LessThan(DepthMath.SortingOrder(10, 0, 0, q)), "먼 것이 먼저");
            // 정본 편향: 투사체 −0.02는 같은 깊이 고양이보다 앞(나중에 그림)
            Assert.That(DepthMath.SortingOrder(10, cfg.SortProj, DepthMath.RankUnit, q),
                Is.GreaterThan(DepthMath.SortingOrder(10, 0, DepthMath.RankUnit, q)));
            // 빨랫줄 +0.5는 뒤
            Assert.That(DepthMath.SortingOrder(10, cfg.SortCloth, DepthMath.RankProp, q),
                Is.LessThan(DepthMath.SortingOrder(10, 0, DepthMath.RankProp, q)));
            // 같은 깊이 칸이면 먼저 넣은 범주(소품)가 뒤
            Assert.That(DepthMath.SortingOrder(10, 0, DepthMath.RankProp, q), Is.LessThan(DepthMath.SortingOrder(10, 0, DepthMath.RankFx, q)));
            Assert.That(DepthMath.SortingOrder(127, 0, DepthMath.RankFx, q), Is.GreaterThanOrEqualTo(DepthMath.MinOrder));
            Assert.That(DepthMath.SortingOrder(500, 0, 0, q), Is.EqualTo(DepthMath.MinOrder), "int16 하한으로 자름");
        }

        [Test]
        public void NearCut_HidesAnchorsCloserThanTable()
        {
            var cfg = ViewTestTables.Config();
            Assert.That(DepthMath.IsNearCut(0.29, cfg.NearCut), Is.True);
            Assert.That(DepthMath.IsNearCut(0.3, cfg.NearCut), Is.False);
        }

        [Test]
        public void RankFarFirst_IsStableDescending()
        {
            var d = new[] { 5.0, 9.0, 5.0, 1.0 };
            var order = new int[4];
            DepthMath.RankFarFirst(d, 4, order, new int[4]);
            Assert.That(order, Is.EqualTo(new[] { 1, 0, 2, 3 }));
        }

        [Test]
        public void PxToMeters_IsInverseOfPxPerMeter()
        {
            double s = CameraModel.PxPerMeter(52, 640, 20);
            Assert.That(DepthMath.PxToMeters(3, 20, 52, 640), Is.EqualTo(3 / s).Within(1e-12));
        }

        [Test]
        public void ColorHelpers_MatchReferenceRounding()
        {
            Assert.That(MvColor.Shade("#ff0000", -0.5), Is.EqualTo("#7f0000"), "×(1+k) 뒤 내림");
            Assert.That(MvColor.Shade("#000000", 0.5), Is.EqualTo("#7f7f7f"), "흰색 쪽 lerp 뒤 내림");
            Assert.That(MvColor.Mix("#000000", "#ffffff", 0.5), Is.EqualTo("#7f7f7f"));
            Assert.That(MvColor.Mix("#000000", "#ffffff", 0.549), Is.EqualTo("#7f7f7f"), "1/20 단위 내림");
            Assert.That(MvColor.Mix("#000000", "#ffffff", 0.06), Is.EqualTo("#0c0c0c"));
            Assert.That(MvColor.MixStep("#000000", "#ffffff", 3, 20), Is.EqualTo(MvColor.Mix("#000000", "#ffffff", 0.15 + 1e-9)));
            var c = MvColor.WithAlpha("#4a3446", 0.467);
            Assert.That(c.r, Is.EqualTo(0x4a));
            Assert.That(c.a, Is.EqualTo(119));
        }
    }
}
