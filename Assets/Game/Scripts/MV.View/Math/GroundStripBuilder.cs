using System;
using System.Collections.Generic;
using MV.Data;
using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>옹벽(계단 꼭대기 절벽, 정본 `R.cliff`) — z, 아래·위 높이.</summary>
    public struct CliffInfo
    {
        public double Z, Low, High;
        public CliffInfo(double z, double low, double high) { Z = z; Low = low; High = high; }
    }

    /// <summary>지면 띠 한 프레임 입력(게임 좌표).</summary>
    public struct GroundFrame
    {
        /// <summary>정본 `PZ` = 카메라 z.</summary>
        public double Pz;
        public Vector3d CamPos, CamLook;
        /// <summary>현재 지면 높이(정본 `envG.position.y` = WY).</summary>
        public double GroundY;
        public bool HasCliff;
        public CliffInfo Cliff;
    }

    /// <summary>지면 색 번호(<see cref="GroundPalette"/>).</summary>
    public enum GroundColor : byte { Grass, Grass2, Road, Road2, Joint, Stone, Snow, CliffFace, CliffLine }

    public enum GroundPart : byte { Grass, Road, JointV, JointH, Curb, SnowL, SnowR, CliffFace, CliffLine }

    /// <summary>지면 사각형 하나(a→b→c→d, 게임 좌표).</summary>
    public struct GroundQuad
    {
        public Vector3d A, B, C, D;
        public GroundPart Part;
        public GroundColor Color;
        /// <summary>안개 혼합 단계(`(fk·quant)|0`). 옹벽은 0(정본도 혼합 안 함).</summary>
        public int FogStep;
    }

    /// <summary>
    /// 테마 지면 색과 안개 혼합 결과표. 테마 로드 때 한 번 만들고(문자열 키 조회 없음) 매 프레임은 배열만 읽는다.
    /// 값은 <see cref="MvColor.Mix"/>·<see cref="MvColor.Shade"/>와 같다.
    /// </summary>
    public sealed class GroundPalette
    {
        public readonly int Quant;
        public readonly bool SnowDeco;
        readonly string[] _base;
        readonly string _fog;
        readonly Color32[,] _mix;

        public GroundPalette(ThemeData th, int quant) : this(th.fog, th.grass, th.grass2, th.road, th.road2, th.stone, th.deco == "snow", quant) { }

        public GroundPalette(string fog, string grass, string grass2, string road, string road2, string stone, bool snowDeco, int quant)
        {
            if (quant <= 0) throw new ArgumentOutOfRangeException(nameof(quant));
            Quant = quant;
            SnowDeco = snowDeco;
            _fog = fog;
            _base = new[]
            {
                grass, grass2, road, road2, MvColor.Shade(road, -0.13), stone, "#ffffff",
                MvColor.Shade(stone, -0.25), MvColor.Shade(stone, 0.2),
            };
            _mix = new Color32[_base.Length, quant + 1];
            for (int c = 0; c < _base.Length; c++)
                for (int q = 0; q <= quant; q++)
                    _mix[c, q] = MvColor.ToColor32(Hex((GroundColor)c, q));
        }

        /// <summary>시험·디버그용 문자열 결과: q = 0이면 원색, 아니면 mixC(원색, fog, q/quant).</summary>
        public string Hex(GroundColor c, int q) => MvColor.MixStep(_base[(int)c], _fog, q, Quant);

        public Color32 Get(GroundColor c, int q) => _mix[(int)c, q < 0 ? 0 : q > Quant ? Quant : q];
    }

    /// <summary>
    /// 정본 `drawGround`(L2874~2894, 02 §2.4.3)를 사각형 목록으로 옮긴다. 먼 띠부터, 띠 안은 정본 그리는 순서.
    /// 사각형 꼭짓점 하나라도 d &lt; nearCut이면 정본 `quad`처럼 그 사각형을 뺀다.
    /// </summary>
    public sealed class GroundStripBuilder
    {
        static readonly double[] s_jointX = { -3.0, -1.5, 0, 1.5, 3.0 };

        readonly ViewConfig _cfg;
        /// <summary>옹벽 윗선 3 px 굵기를 m로 바꿀 기준 세로 FOV(320_camera run 행 fovV).</summary>
        readonly double _refFovV;

        public readonly List<GroundQuad> Quads = new List<GroundQuad>(640);

        public GroundStripBuilder(ViewConfig cfg, double refFovV)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _refFovV = refFovV;
        }

        public void Build(in GroundFrame f, bool snowDeco, int quant)
        {
            Quads.Clear();
            double step = _cfg.GroundStep;
            double zN = f.Pz + _cfg.GroundNear, zF = f.Pz + _cfg.GroundFar;
            bool cl = f.HasCliff && f.Cliff.Z > zN && f.Cliff.Z < zF;
            var fwd = DepthMath.Forward(f.CamPos, f.CamLook);

            for (double z = Math.Floor(zF / step) * step; z >= Math.Floor(zN / step) * step; z -= step)
            {
                double a = Math.Max(z, zN), b = z + step;
                if (b <= a) continue;
                if (cl && f.Cliff.Z > a && f.Cliff.Z < b)
                {
                    Strip(f, fwd, z, f.Cliff.Z, b, snowDeco, quant);   // 먼 조각 먼저(정본 segs 순서)
                    Strip(f, fwd, z, a, f.Cliff.Z, snowDeco, quant);
                }
                else Strip(f, fwd, z, a, b, snowDeco, quant);

                if (cl && f.Cliff.Z > a && f.Cliff.Z <= b) CliffWall(f, fwd);
            }
        }

        double GroundYAt(in GroundFrame f, double z) => f.HasCliff && z < f.Cliff.Z ? f.Cliff.Low : f.GroundY;

        void Strip(in GroundFrame f, Vector3d fwd, double z, double s0, double s1, bool snowDeco, int quant)
        {
            double mz = (s0 + s1) / 2;
            double y = GroundYAt(f, mz);
            double md = DepthMath.Depth(new Vector3d(0, y, mz), f.CamPos, fwd);
            double fk = DepthMath.IsNearCut(md, _cfg.NearCut) ? 1 : DepthMath.FogK(md, _cfg.FogStart, _cfg.FogRange);
            int q = (int)(fk * quant);   // JS `k*20|0`
            long odd = (long)Math.Abs(Math.Floor(z / 2)) % 2;
            var gc = (long)Math.Abs(Math.Floor(z / 4)) % 2 != 0 ? GroundColor.Grass : GroundColor.Grass2;
            var rc = odd != 0 ? GroundColor.Road : GroundColor.Road2;

            Flat(f, fwd, s0, s1, -70, 70, y, GroundPart.Grass, gc, q);
            Flat(f, fwd, s0, s1, -4.0, 4.0, y, GroundPart.Road, rc, q);
            foreach (double lx in s_jointX)
            {
                double xx = lx + (odd != 0 ? 0.75 : 0);
                if (Math.Abs(xx) < 3.95) Flat(f, fwd, s0, s1, xx - 0.035, xx + 0.035, y + 0.004, GroundPart.JointV, GroundColor.Joint, q);
            }
            Flat(f, fwd, s0, Math.Min(s1, s0 + 0.07), -4.0, 4.0, y + 0.004, GroundPart.JointH, GroundColor.Joint, q);
            Flat(f, fwd, s0, s1, -4.45, -4.0, y, GroundPart.Curb, GroundColor.Stone, q);
            Flat(f, fwd, s0, s1, 4.0, 4.45, y, GroundPart.Curb, GroundColor.Stone, q);
            if (snowDeco)
            {
                double h1 = FieldGen.Hash(z * 0.31), h2 = FieldGen.Hash(z * 0.57);
                if (h1 > 0.3) Flat(f, fwd, s0 + 0.2, Math.Min(s1, s0 + 1.6), -4.45, -3.4 + h1 * 0.6, y + 0.006, GroundPart.SnowL, GroundColor.Snow, q);
                if (h2 > 0.35) Flat(f, fwd, s0 + 0.4, Math.Min(s1, s0 + 1.4), 3.3 - h2 * 0.6, 4.45, y + 0.006, GroundPart.SnowR, GroundColor.Snow, q);
            }
        }

        void Flat(in GroundFrame f, Vector3d fwd, double z0, double z1, double x0, double x1, double y, GroundPart part, GroundColor col, int q)
        {
            var a = new Vector3d(x0, y, z0);
            var b = new Vector3d(x1, y, z0);
            var c = new Vector3d(x1, y, z1);
            var d = new Vector3d(x0, y, z1);
            if (!Visible(f, fwd, a, b, c, d)) return;
            Quads.Add(new GroundQuad { A = a, B = b, C = c, D = d, Part = part, Color = col, FogStep = q });
        }

        bool Visible(in GroundFrame f, Vector3d fwd, Vector3d a, Vector3d b, Vector3d c, Vector3d d)
        {
            double n = _cfg.NearCut;
            return DepthMath.Depth(a, f.CamPos, fwd) >= n && DepthMath.Depth(b, f.CamPos, fwd) >= n
                && DepthMath.Depth(c, f.CamPos, fwd) >= n && DepthMath.Depth(d, f.CamPos, fwd) >= n;
        }

        void CliffWall(in GroundFrame f, Vector3d fwd)
        {
            var c = f.Cliff;
            var p0 = new Vector3d(-70, c.Low, c.Z);
            var p1 = new Vector3d(70, c.Low, c.Z);
            var p2 = new Vector3d(70, c.High, c.Z);
            var p3 = new Vector3d(-70, c.High, c.Z);
            if (!Visible(f, fwd, p0, p1, p2, p3)) return;
            Quads.Add(new GroundQuad { A = p0, B = p1, C = p2, D = p3, Part = GroundPart.CliffFace, Color = GroundColor.CliffFace, FogStep = 0 });

            // 윗선 3 px: 카메라 위쪽 방향으로 반씩(화면에서 선 굵기가 일정하도록 카메라 평면 안 띠).
            double d = DepthMath.Depth(new Vector3d(0, c.High, c.Z), f.CamPos, fwd);
            double t = DepthMath.PxToMeters(3, d, _refFovV, _cfg.RefHeightPx);
            var up = CameraUp(fwd) * (t * 0.5);
            Quads.Add(new GroundQuad { A = p3 - up, B = p2 - up, C = p2 + up, D = p3 + up, Part = GroundPart.CliffLine, Color = GroundColor.CliffLine, FogStep = 0 });
        }

        /// <summary>시선에 수직이고 세계 위쪽에 가장 가까운 단위 벡터(LookAt의 카메라 위).</summary>
        public static Vector3d CameraUp(Vector3d fwd)
        {
            var wu = new Vector3d(0, 1, 0);
            return (wu - fwd * Vector3d.Dot(wu, fwd)).Normalized;
        }

        /// <summary>사각형을 메쉬 버퍼에 옮긴다(색은 팔레트 결과표).</summary>
        public void Fill(MeshBuffer mb, GroundPalette pal)
        {
            mb.Clear();
            for (int i = 0; i < Quads.Count; i++)
            {
                var g = Quads[i];
                mb.AddQuad(g.A.ToVector3(), g.B.ToVector3(), g.C.ToVector3(), g.D.ToVector3(), pal.Get(g.Color, g.FogStep));
            }
        }
    }
}
