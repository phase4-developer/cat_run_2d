using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MV.View.Tests
{
    /// <summary>
    /// 티켓 03 순수 계산: 지면 띠(02 §2.4.3), 상자 빌보드(02 §2.6.4).
    /// </summary>
    public sealed class WorldLayoutTests
    {
        static readonly Vector3d s_runCam = new Vector3d(0, 13, -9), s_runLook = new Vector3d(0, 0.5, 5.5);

        static GroundStripBuilder Builder(ViewConfig cfg) => new GroundStripBuilder(cfg, ViewTestTables.Camera().run.fovV);

        static GroundFrame RunFrame(double groundY = 0) =>
            new GroundFrame { Pz = s_runCam.z, CamPos = s_runCam, CamLook = s_runLook, GroundY = groundY };

        [Test]
        public void Ground_FarStripsFirst_AllCornersPastNearCut()
        {
            var cfg = ViewTestTables.Config();
            var b = Builder(cfg);
            b.Build(RunFrame(), false, cfg.MixQuant);
            Assert.That(b.Quads.Count, Is.GreaterThan(400));
            var fwd = DepthMath.Forward(s_runCam, s_runLook);
            double prevZ = double.MaxValue;
            foreach (var q in b.Quads)
            {
                foreach (var p in new[] { q.A, q.B, q.C, q.D })
                    Assert.That(DepthMath.Depth(p, s_runCam, fwd), Is.GreaterThanOrEqualTo(cfg.NearCut));
                if (q.Part == GroundPart.Grass)
                {
                    Assert.That(q.A.z, Is.LessThan(prevZ), "먼 띠부터");
                    prevZ = q.A.z;
                }
            }
            Assert.That(b.Quads.Max(q => q.C.z), Is.LessThanOrEqualTo(s_runCam.z + cfg.GroundFar + cfg.GroundStep));
            Assert.That(b.Quads.Any(q => q.Part == GroundPart.SnowL || q.Part == GroundPart.SnowR), Is.False);
        }

        [Test]
        public void Ground_FogStepAndPaletteMatchMixC()
        {
            var cfg = ViewTestTables.Config();
            var th = ViewTestTables.Theme("market");
            var pal = new GroundPalette(th, cfg.MixQuant);
            var b = Builder(cfg);
            b.Build(RunFrame(), false, cfg.MixQuant);
            var fwd = DepthMath.Forward(s_runCam, s_runLook);
            foreach (var q in b.Quads.Where(q => q.Part == GroundPart.Grass))
            {
                double mz = (q.A.z + q.C.z) / 2;
                double fk = DepthMath.FogK(DepthMath.Depth(new Vector3d(0, 0, mz), s_runCam, fwd), cfg.FogStart, cfg.FogRange);
                Assert.That(q.FogStep, Is.EqualTo((int)(fk * cfg.MixQuant)), "z " + mz);
            }
            Assert.That(b.Quads.First().FogStep, Is.GreaterThan(10), "맨 먼 띠는 안개가 짙다");
            Assert.That(b.Quads.Last(q => q.Part == GroundPart.Grass).FogStep, Is.EqualTo(0), "가까운 띠는 안개 없음");
            for (int s = 0; s <= cfg.MixQuant; s += 7)
                Assert.That(pal.Hex(GroundColor.Grass, s), Is.EqualTo(MvColor.MixStep(th.grass, th.fog, s, cfg.MixQuant)));
            Assert.That(pal.Hex(GroundColor.Joint, 0), Is.EqualTo(MvColor.Shade(th.road, -0.13)));
            Assert.That(pal.Get(GroundColor.Grass, 0), Is.EqualTo(MvColor.ToColor32(th.grass)));
        }

        [Test]
        public void Ground_CliffSplitsStripAndAddsWall()
        {
            var cfg = ViewTestTables.Config();
            var b = Builder(cfg);
            var f = RunFrame(3.36);
            f.HasCliff = true;
            f.Cliff = new CliffInfo(41, 0, 3.36);
            b.Build(f, false, cfg.MixQuant);
            Assert.That(b.Quads.Count(q => q.Part == GroundPart.CliffFace), Is.EqualTo(1));
            Assert.That(b.Quads.Count(q => q.Part == GroundPart.CliffLine), Is.EqualTo(1));
            var grass = b.Quads.Where(q => q.Part == GroundPart.Grass).ToList();
            var farPiece = grass.Single(q => q.A.z == 41 && q.C.z == 42);
            var nearPiece = grass.Single(q => q.A.z == 40 && q.C.z == 41);
            Assert.That(farPiece.A.y, Is.EqualTo(3.36), "절벽 뒤는 현재 지면");
            Assert.That(nearPiece.A.y, Is.EqualTo(0), "절벽 앞은 cliff.low");
            Assert.That(grass.IndexOf(farPiece), Is.LessThan(grass.IndexOf(nearPiece)), "먼 조각 먼저");
            var wall = b.Quads.Single(q => q.Part == GroundPart.CliffFace);
            Assert.That(b.Quads.IndexOf(wall), Is.GreaterThan(b.Quads.IndexOf(nearPiece)), "옹벽은 그 띠 뒤에 그린다");
            Assert.That(wall.FogStep, Is.EqualTo(0));
        }

        [Test]
        public void Ground_SnowDecoAddsReferenceDrifts()
        {
            var cfg = ViewTestTables.Config();
            var b = Builder(cfg);
            b.Build(RunFrame(), true, cfg.MixQuant);
            // 정본 hash: z=4 → h1 0.0887(없음), h2 0.8335(있음) / z=8 → h1 0.2234(없음), h2 0.8113(있음)
            Assert.That(b.Quads.Any(q => q.Part == GroundPart.SnowL && q.A.z >= 4 && q.A.z < 6), Is.False);
            Assert.That(b.Quads.Any(q => q.Part == GroundPart.SnowR && q.A.z >= 4 && q.A.z < 6), Is.True);
            Assert.That(b.Quads.Any(q => q.Part == GroundPart.SnowL && q.A.z >= 2 && q.A.z < 4), Is.True);
        }

        [Test]
        public void BoxShape_ReferenceThicknessAndClamps()
        {
            var house = BoxBillboardBuilder.Shape(4.6, 2.2, 3.6, double.PositiveInfinity);
            Assert.That(house.T, Is.EqualTo(1.152).Within(1e-12), "집 윗면 두께 고정");
            Assert.That(house.Line, Is.EqualTo(0.035).Within(1e-12));
            Assert.That(house.ShadeW, Is.EqualTo(0.184).Within(1e-12));
            Assert.That(BoxBillboardBuilder.Shape(1.2, 0.6, 0.8, double.PositiveInfinity).T, Is.EqualTo(0.256).Within(1e-12), "우다다 입구 계단");
            var near = BoxBillboardBuilder.Shape(4.6, 2.2, 3.6, 10);   // s = 10 px/m: 1 px = 0.1 m
            Assert.That(near.Line, Is.EqualTo(0.1).Within(1e-12), "1 px 하한");
            Assert.That(near.Highlight, Is.EqualTo(0.132).Within(1e-12));
            var big = BoxBillboardBuilder.Shape(4.6, 2.2, 3.6, 200);
            Assert.That(big.Line, Is.EqualTo(0.015).Within(1e-12), "3 px 상한");
        }

        [Test]
        public void BoxBuild_PartOrderAndColors()
        {
            var mb = new MeshBuffer();
            BoxBillboardBuilder.Build(mb, 4.6, 2.2, 3.6, "#c8a8e8", null, double.PositiveInfinity, "#4a3446");
            // 그늘 4 + 윗면 4 + 윗면 외곽선 16 + 정면 4 + 하이라이트 4 + 정면 외곽선 16
            Assert.That(mb.VertexCount, Is.EqualTo(48));
            Assert.That(mb.Colors[0], Is.EqualTo(MvColor.ToColor32(MvColor.Shade("#c8a8e8", -0.28))), "그늘 띠 먼저");
            Assert.That(mb.Colors[4], Is.EqualTo(MvColor.ToColor32(MvColor.Shade("#c8a8e8", 0.18))), "윗면 기본색");
            Assert.That(mb.Colors[24], Is.EqualTo(MvColor.ToColor32(MvColor.Shade("#c8a8e8", -0.14))), "정면 아래");
            Assert.That(mb.Colors[26], Is.EqualTo(MvColor.ToColor32(MvColor.Shade("#c8a8e8", 0.04))), "정면 위");
            Assert.That(mb.Triangles.Count % 3, Is.EqualTo(0));
            mb.Clear();
            BoxBillboardBuilder.Build(mb, 4.6, 2.2, 3.6, "#c8a8e8", "#a05050", double.PositiveInfinity, "#4a3446", outline: false);
            Assert.That(mb.VertexCount, Is.EqualTo(16));
            Assert.That(mb.Colors[4], Is.EqualTo(MvColor.ToColor32("#a05050")), "topCol");
        }
    }
}
