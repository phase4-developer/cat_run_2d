using NUnit.Framework;

namespace MV.View.Tests
{
    /// <summary>티켓 01 카메라 리그 순수 계산(기획서 02 §2.2, §2.7.2). 기준 기하 검산은 02 §2.2.3 표.</summary>
    public sealed class CameraModelTests
    {
        const double H = 640;

        static CameraModel NewModel() => CameraModel.FromTables(ViewTestTables.Load());

        /// <summary>옆 시점(side 행) 없이 기본 시점만 보는 모델.</summary>
        static CameraModel NoSide()
        {
            var t = ViewTestTables.Load();
            return new CameraModel(t.Camera.ByKey(CameraModel.KeyRun), t.Camera.ByKey(CameraModel.KeyClimb), t.Camera.ByKey(CameraModel.KeyLobby));
        }

        static CameraModel Settled(WorldViewMode mode, bool side = false)
        {
            var m = side ? NewModel() : NoSide();
            var input = new CameraInput { Mode = mode, Px = 0, Pz = 0, Wy = 0, ClimbPy = 0 };
            // dt = 1이면 모든 보간 계수가 1로 잘려 목표에 바로 선다.
            m.Step(input, 1, default);
            return m;
        }

        [Test]
        public void RunPose_MatchesReferenceGeometry()
        {
            var m = Settled(WorldViewMode.Run);
            AssertVec(m.Pos, 0, 13, -9);
            AssertVec(m.Look, 0, 0.5, 5.5);
            var cfg = ViewTestTables.Config();
            double fov = m.RunRow.fovV;
            double pitch = DepthMath.PitchDeg(m.Pos, m.Look);
            Assert.That(pitch, Is.EqualTo(40.76).Within(0.01), "내려다보는 각");
            Assert.That(pitch - fov / 2, Is.EqualTo(14.76).Within(0.01), "화면 맨 위 광선");
            double d = CameraModel.ViewDepth(new Vector3d(0, 0.7, 0), m.Pos, m.Look);
            Assert.That(d, Is.EqualTo(14.85).Within(0.01), "플레이어 몸통 깊이");
            Assert.That(CameraModel.PxPerMeter(fov, H, d), Is.EqualTo(44.2).Within(0.05), "플레이어 s");
            Assert.That(DepthMath.GroundZAtDepth(m.Pos, m.Look, 0, 0, cfg.FogStart), Is.EqualTo(24.7).Within(0.05), "안개 시작 지면 z");
            Assert.That(DepthMath.GroundZAtDepth(m.Pos, m.Look, 0, 0, cfg.FogStart + cfg.FogRange), Is.EqualTo(90.7).Within(0.05), "안개 끝 지면 z");
        }

        [Test]
        public void ClimbPose_MatchesReferenceGeometry()
        {
            var m = Settled(WorldViewMode.Climb);
            Assert.That(m.Ck, Is.EqualTo(1).Within(1e-12));
            AssertVec(m.Pos, 0, 15, -10.5);
            AssertVec(m.Look, 0, 0.5, 5.5);
            var cfg = ViewTestTables.Config();
            double fov = m.RunRow.fovV;
            double pitch = DepthMath.PitchDeg(m.Pos, m.Look);
            Assert.That(pitch, Is.EqualTo(42.18).Within(0.01), "내려다보는 각");
            Assert.That(pitch - fov / 2, Is.EqualTo(16.18).Within(0.01), "화면 맨 위 광선");
            double d = CameraModel.ViewDepth(new Vector3d(0, 0.7, 0), m.Pos, m.Look);
            Assert.That(d, Is.EqualTo(17.38).Within(0.01), "플레이어 몸통 깊이");
            Assert.That(CameraModel.PxPerMeter(fov, H, d), Is.EqualTo(37.7).Within(0.05), "플레이어 s");
            Assert.That(DepthMath.GroundZAtDepth(m.Pos, m.Look, 0, 0, cfg.FogStart), Is.EqualTo(21.8).Within(0.05), "안개 시작 지면 z");
            Assert.That(DepthMath.GroundZAtDepth(m.Pos, m.Look, 0, 0, cfg.FogStart + cfg.FogRange), Is.EqualTo(89.3).Within(0.05), "안개 끝 지면 z");
        }

        [Test]
        public void LobbyPose_SnapsToWorldRow()
        {
            var m = NewModel();
            m.Snap(new Vector3d(5, 5, 5), new Vector3d(0, 0, 0));
            m.Step(new CameraInput { Mode = WorldViewMode.Lobby, Px = 3, Pz = 100 }, 0.016, default);
            AssertVec(m.Pos, 0, 4.2, -4.4);
            AssertVec(m.Look, 0, 0.2, 3.4);
        }

        [Test]
        public void RunStep_LerpsPositionAndLiftWithTableCoefficients()
        {
            var m = NewModel();
            m.Snap(new Vector3d(0, 4.2, -4.4), new Vector3d(0, 0.2, 3.4));
            const double dt = 0.016;
            m.Step(new CameraInput { Mode = WorldViewMode.Run, Px = 2, Pz = 10, Wy = 1 }, dt, default);
            // camLift: 0 + (1 − 0)·min(1, dt·6)
            Assert.That(m.CamLift, Is.EqualTo(dt * 6).Within(1e-12));
            double k = dt * 10;
            var target = new Vector3d(2 * 0.25, m.CamLift + 13, 10 - 9);
            AssertVec(m.Pos, 0 + (target.x - 0) * k, 4.2 + (target.y - 4.2) * k, -4.4 + (target.z + 4.4) * k);
            AssertVec(m.Look, 0.5, m.CamLift + 0.5, 15.5);
        }

        [Test]
        public void ClimbPath_FollowsHtmlClimbCatPos()
        {
            var path = new ClimbPath(0.42, 0.72, 0.6, 1.4);
            path.Cat(0, 3, 10, out double py, out double pz);
            Assert.That(py, Is.EqualTo(0).Within(1e-12));
            Assert.That(pz, Is.EqualTo(10 - 1.4).Within(1e-12), "출발 = 계단 입구(z0 − z0Gap)");
            path.Cat(0.5, 3, 10, out py, out pz);
            Assert.That(py, Is.EqualTo(0.5 * 0.42 + 0.5 * 0.42 * 0.6).Within(1e-12));
            Assert.That(pz, Is.EqualTo(10 - 1.4 * 0.5 + 0.5 * 0.72).Within(1e-12));
            path.Cat(9, 3, 10, out py, out pz);
            Assert.That(py, Is.EqualTo(3 * 0.42 + 0.42 * 0.6).Within(1e-12), "k에서 멈춤");
            Assert.That(pz, Is.EqualTo(10 + 3 * 0.72).Within(1e-12));
        }

        [Test]
        public void ClimbPath_ReadsClimbTable()
        {
            var p = ClimbPath.From(ViewTestTables.Load().Climb);
            Assert.That(p.StepH, Is.EqualTo(0.42).Within(1e-12), "Obj_ClimbStep 블록 높이");
            Assert.That(p.StepD, Is.EqualTo(0.72).Within(1e-12), "Obj_ClimbStep 블록 깊이");
            Assert.That(p.Lift, Is.EqualTo(0.6).Within(1e-12), "새 HTML climbCatPos 0.6");
            Assert.That(p.Z0Gap, Is.EqualTo(1.4).Within(1e-12));
            var edited = ViewTestTables.WithCell(MV.Data.ClimbData.Table, "default", "catLift", "0.3");
            Assert.That(ClimbPath.From(edited.Climb).Lift, Is.EqualTo(0.3).Within(1e-12), "표만 바꿔도 고양이 높이가 바뀐다");
        }

        [Test]
        public void ClimbSide_SettlesBesideCat()
        {
            var m = NewModel();
            var side = m.SideRow;
            var input = new CameraInput { Mode = WorldViewMode.Climb, Px = 0.5, Pz = 20, Wy = 2, ClimbPy = 1 };
            m.Step(input, 1, default);
            Assert.That(m.OrbitE, Is.EqualTo(1).Within(1e-12));
            AssertVec(m.Pos, 0.5 * side.posXMul + side.posX, 3 + side.posY, 20 + side.posZ);
            AssertVec(m.Look, 0.5 * side.lookXMul + side.lookX, 3 + side.lookY, 20 + side.lookZ);
        }

        [Test]
        public void ClimbSide_TurnsOverTurnSecWithSmoothstep()
        {
            var m = NewModel();
            double half = m.SideRow.turnSec / 2;
            Assert.That(m.SideRow.turnSec, Is.EqualTo(0.42).Within(1e-12), "새 HTML ORB.dur");
            var input = new CameraInput { Mode = WorldViewMode.Climb, Px = 0, Pz = 0, Wy = 0, ClimbPy = 0 };
            m.Step(input, half, default);
            Assert.That(m.OrbitK, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(m.OrbitE, Is.EqualTo(0.5).Within(1e-9), "smooth(0.5) = 0.5");
            input.Mode = WorldViewMode.Run;
            m.Step(input, half, default);
            Assert.That(m.OrbitK, Is.EqualTo(0).Within(1e-9), "오르기가 끝나면 같은 시간에 되돌아온다");
        }

        [Test]
        public void Shake_MergesMaxAndClearsAmplitudeWhenTimeRunsOut()
        {
            var s = new CameraShake();
            s.Request(0.2, 0.1);
            s.Request(0.1, 0.3);
            Assert.That(s.A, Is.EqualTo(0.2));
            Assert.That(s.T, Is.EqualTo(0.3));
            var o = s.Step(0.2, 1, -0.5);
            Assert.That(o.x, Is.EqualTo(0.2).Within(1e-12));
            Assert.That(o.y, Is.EqualTo(-0.1).Within(1e-12));
            Assert.That(o.z, Is.EqualTo(0));
            o = s.Step(0.2, 1, 1);
            Assert.That(o.x, Is.EqualTo(0.2).Within(1e-12), "마지막 프레임도 가산한 뒤 A = 0");
            Assert.That(s.A, Is.EqualTo(0));
            o = s.Step(0.2, 1, 1);
            Assert.That(o.x, Is.EqualTo(0));
        }

        // 이름이 정수만 갖도록 화면비는 폭·높이, 기대 FOV는 0.01° 단위.
        [TestCase(18, 32, 5200)]
        [TestCase(18, 36, 5751)]
        [TestCase(18, 39, 6146)]
        [TestCase(18, 42, 6525)]
        [TestCase(18, 60, 6525)]
        [TestCase(3, 4, 5200)]
        public void VerticalFov_ExtendsTallScreens(int w, int h, int expectedCentiDeg)
        {
            Assert.That(CameraModel.VerticalFov(52, w / (double)h), Is.EqualTo(expectedCentiDeg / 100.0).Within(0.01));
        }

        static void AssertVec(Vector3d v, double x, double y, double z)
        {
            Assert.That(v.x, Is.EqualTo(x).Within(1e-9), "x " + v);
            Assert.That(v.y, Is.EqualTo(y).Within(1e-9), "y " + v);
            Assert.That(v.z, Is.EqualTo(z).Within(1e-9), "z " + v);
        }
    }
}
