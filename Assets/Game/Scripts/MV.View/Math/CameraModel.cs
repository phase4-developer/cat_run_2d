using System;
using MV.Data;
using UnityEngine;

namespace MV.View
{
    /// <summary>월드 카메라 모드(02 §2.2.2 `CameraRig.LateUpdate` 1단계).</summary>
    public enum WorldViewMode { Lobby, Run, Climb }

    /// <summary>카메라가 한 프레임에 읽는 런 상태(게임 좌표, m). `IWorldViewSource`가 채운다.</summary>
    public struct CameraInput
    {
        public WorldViewMode Mode;
        public double Px, Pz;
        /// <summary>현재 지면 높이 `WY`.</summary>
        public double Wy;
        /// <summary>계단 오르기 중 플레이어 높이(지면 기준, <see cref="CameraModel.ClimbPlayer"/>).</summary>
        public double ClimbPy;
    }

    /// <summary>
    /// 정본 `syncScene` 카메라 공식(L2746~2779, 02 §2.2). 값은 `320_camera` 행(key run/climb/lobby)에서 읽는다.
    /// 위치·주시점은 double로 계산하고 Unity Transform에 넣을 때만 float로 바꾼다.
    /// </summary>
    public sealed class CameraModel
    {
        public const string KeyRun = "run", KeyClimb = "climb", KeyLobby = "lobby";

        readonly CameraData _run, _climb, _lobby;

        public double CamLift { get; private set; }
        public double Ck { get; private set; }
        public Vector3d Pos { get; private set; }
        public Vector3d Look { get; private set; }
        bool _hasPos;

        public CameraModel(CameraData run, CameraData climb, CameraData lobby)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _climb = climb ?? throw new ArgumentNullException(nameof(climb));
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
        }

        public static CameraModel FromTables(MvTables t) =>
            new CameraModel(Row(t, KeyRun), Row(t, KeyClimb), Row(t, KeyLobby));

        static CameraData Row(MvTables t, string key) =>
            t.Camera.ByKey(key) ?? throw new InvalidOperationException($"320_camera: key '{key}' 행이 없습니다");

        public CameraData RunRow => _run;
        public CameraData LobbyRow => _lobby;

        /// <summary>계단 오르기 중 플레이어 위치(L2758): pz = z0 + sp·stepD, py = (floor(sp)+1)·stepH + sin(frac·π)·bump, sp &lt; 1이면 상한.</summary>
        public static void ClimbPlayer(double pos, int k, double z0, double stepH, double stepD, double bump, out double py, out double pz)
        {
            double sp = Math.Max(0, Math.Min(k - 0.001, pos));
            pz = z0 + sp * stepD;
            double f = sp % 1;
            py = (Math.Floor(sp) + 1) * stepH + Math.Sin(f * Math.PI) * bump;
            if (sp < 1) py = Math.Min(py, sp * stepH * 1.2);
        }

        /// <summary>
        /// 한 프레임(실시간 dt). camLift·ck 갱신 → 목표 계산 → 위치 보간 → 흔들림 가산(<paramref name="shake"/>, x·y만).
        /// 주시점은 흔들지 않는다(D-43, 정본 그대로).
        /// </summary>
        public void Step(in CameraInput input, double dt, Vector3d shake)
        {
            Vector3d target, look;
            double lerpK;
            if (input.Mode == WorldViewMode.Lobby)
            {
                target = new Vector3d(0, _lobby.posY, _lobby.posZ);   // anchor=World
                look = new Vector3d(0, _lobby.lookY, _lobby.lookZ);
                lerpK = _lobby.snap ? 1 : Math.Min(1, dt * _lobby.posLerp);
            }
            else
            {
                bool c = input.Mode == WorldViewMode.Climb;
                double liftT = input.Wy + (c ? input.ClimbPy : 0);
                CamLift += (liftT - CamLift) * Math.Min(1, dt * (c ? _climb.liftLerp : _run.liftLerp));
                Ck += ((c ? 1 : 0) - Ck) * Math.Min(1, dt * (c ? _climb.blendLerp : _run.blendLerp));
                double k = Ck;
                double posXMul = Lerp(_run.posXMul, _climb.posXMul, k), posY = Lerp(_run.posY, _climb.posY, k), posZ = Lerp(_run.posZ, _climb.posZ, k);
                double lookXMul = Lerp(_run.lookXMul, _climb.lookXMul, k), lookY = Lerp(_run.lookY, _climb.lookY, k), lookZ = Lerp(_run.lookZ, _climb.lookZ, k);
                target = new Vector3d(input.Px * posXMul, CamLift + posY, input.Pz + posZ);
                look = new Vector3d(input.Px * lookXMul, CamLift + lookY, input.Pz + lookZ);
                bool snap = c ? _climb.snap : _run.snap;
                lerpK = snap ? 1 : Math.Min(1, dt * (c ? _climb.posLerp : _run.posLerp));
            }

            Vector3d p = _hasPos ? Pos + (target - Pos) * lerpK : target;
            _hasPos = true;
            p = new Vector3d(p.x + shake.x, p.y + shake.y, p.z);
            Pos = p;
            Look = look;
        }

        /// <summary>위치를 즉시 정한다(첫 진입·시험).</summary>
        public void Snap(Vector3d pos, Vector3d look, double camLift = 0, double ck = 0)
        {
            Pos = pos; Look = look; CamLift = camLift; Ck = ck; _hasPos = true;
        }

        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        /// <summary>
        /// 화면비 확장(D-41 권장 ②, 02 §2.7.2): 9:16보다 길면 가로 시야 고정으로 세로 FOV를 늘린다. 범위 9:21~3:4로 자른다.
        /// </summary>
        public static double VerticalFov(double baseFovDeg, double aspect, double refAspect = 9.0 / 16.0, double minAspect = 9.0 / 21.0, double maxAspect = 3.0 / 4.0)
        {
            double a = Math.Max(minAspect, Math.Min(maxAspect, aspect));
            if (a >= refAspect) return baseFovDeg;
            double half = baseFovDeg * 0.5 * Math.PI / 180.0;
            return 2 * Math.Atan(Math.Tan(half) * refAspect / a) * 180.0 / Math.PI;
        }

        /// <summary>정본 `pj`의 s: 1 m당 px = P[5]·H/2/d.</summary>
        public static double PxPerMeter(double vFovDeg, double heightPx, double depth) =>
            1.0 / Math.Tan(vFovDeg * 0.5 * Math.PI / 180.0) * heightPx * 0.5 / depth;

        /// <summary>뷰 전방 깊이 d = (p − cam)·forward(유클리드 거리 아님, L2796).</summary>
        public static double ViewDepth(Vector3d p, Vector3d camPos, Vector3d camLook)
        {
            var f = (camLook - camPos).Normalized;
            return Vector3d.Dot(p - camPos, f);
        }
    }

    /// <summary>
    /// 흔들림(02 §2.2.4): 요청은 최댓값 병합, 매 프레임 x·y에 균등 난수 [−1,1]·A, z 없음, 감쇠 없음.
    /// 난수는 표시 전용(<see cref="UnityEngine.Random"/>)이라 호출자가 넣는다.
    /// </summary>
    public sealed class CameraShake
    {
        public double A { get; private set; }
        public double T { get; private set; }

        public void Request(double amp, double time)
        {
            A = Math.Max(A, amp);
            T = Math.Max(T, time);
        }

        /// <summary>정본 순서: shakeT −= dt, 위치에 난수·A 가산, shakeT ≤ 0이면 A = 0.</summary>
        public Vector3d Step(double dt, double rx, double ry)
        {
            if (T <= 0) return default;
            T -= dt;
            var o = new Vector3d(rx * A, ry * A, 0);
            if (T <= 0) A = 0;
            return o;
        }

        public void Clear() { A = 0; T = 0; }
    }

    /// <summary>double 3차원 벡터(카메라·정렬 계산용, 게임 좌표).</summary>
    public readonly struct Vector3d
    {
        public readonly double x, y, z;
        public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3d operator *(Vector3d a, double k) => new Vector3d(a.x * k, a.y * k, a.z * k);
        public static double Dot(Vector3d a, Vector3d b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public double Length => Math.Sqrt(x * x + y * y + z * z);
        public Vector3d Normalized { get { double l = Length; return l > 0 ? this * (1 / l) : this; } }
        public Vector3 ToVector3() => new Vector3((float)x, (float)y, (float)z);
        public static Vector3d From(Vector3 v) => new Vector3d(v.x, v.y, v.z);
        public override string ToString() => $"({x:0.###}, {y:0.###}, {z:0.###})";
    }
}
