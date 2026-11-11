using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.Fx
{
    /// <summary>
    /// 우다다 화면 식(06 §6.11, 정본 `WARP_ST`·`drawWarp`·`drawWarpOut`·집중선 L3155~3209, L3814~3817). 그림 없이 값만 계산한다.
    /// 단계 행은 330_climb WarpStage(at 오름차순).
    /// </summary>
    public sealed class MvWarpModel
    {
        public struct Stage
        {
            public int At;
            public string Accent, Sky1, Sky2, Sky3;
            public double Night;
        }

        public readonly List<Stage> Stages = new List<Stage>(4);
        /// <summary>`300_runConfig` warp.* 값(<see cref="FromTables"/>가 채운다, 코드 기본값 없음).</summary>
        public double FadeIn, VanishY, StageNameSec, OutSec;
        public int RoofCount, StepCount, SpeedLines;

        /// <summary>표에서 만든다. 표가 없거나 키가 없으면 예외(대체값 없음).</summary>
        public static MvWarpModel FromTables(MvTables t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var m = new MvWarpModel();
            foreach (var r in t.Climb.WarpStages())
                m.Stages.Add(new Stage { At = r.at, Accent = r.accent, Sky1 = r.sky1, Sky2 = r.sky2, Sky3 = r.sky3, Night = r.night });
            var c = t.RunConfig;
            m.FadeIn = c.D(RunConfigKeys.Warp_FadeIn);
            m.VanishY = c.D(RunConfigKeys.Warp_VanishY);
            m.StageNameSec = c.D(RunConfigKeys.Warp_StageNameMs) / 1000.0;
            m.OutSec = c.D(RunConfigKeys.Warp_OutMs) / 1000.0;
            m.RoofCount = c.I(RunConfigKeys.Warp_RoofCount);
            m.StepCount = c.I(RunConfigKeys.Warp_StepCount);
            m.SpeedLines = c.I(RunConfigKeys.Warp_SpeedLines);
            return m;
        }

        /// <summary>정본 `warpStage(v)`: v ≥ at인 마지막 단계.</summary>
        public int StageFor(int shown)
        {
            int s = 0;
            for (int i = 0; i < Stages.Count; i++) if (shown >= Stages[i].At) s = i;
            return s;
        }

        public Stage Get(int st) => Stages.Count == 0 ? default : Stages[Math.Max(0, Math.Min(Stages.Count - 1, st))];

        /// <summary>전체 알파 `min(1, C.t/0.35)`.</summary>
        public double Alpha(double climbT) => Math.Min(1, climbT / FadeIn);

        /// <summary>속도 `1 + st·0.35 + min(1.5, T·0.08)`.</summary>
        public static double Speed(int st, double T) => 1 + st * 0.35 + Math.Min(1.5, T * 0.08);

        /// <summary>단계 이름 알파(실시간 dtx초, 1.8 s 동안).</summary>
        public double StageNameAlpha(double a, double dtx) =>
            dtx >= StageNameSec ? 0 : a * Math.Min(1, dtx * 4) * Math.Min(1, (StageNameSec - dtx) * 2);

        /// <summary>단계 이름 글자 크기 `min(W,H)·0.068·(1 + max(0, 0.25 − dtx))`.</summary>
        public static double StageNameSize(double w, double h, double dtx) => Math.Round(Math.Min(w, h) * 0.068 * (1 + Math.Max(0, 0.25 - dtx)));

        /// <summary>정본 `hash(n)`(L858).</summary>
        public static double Hash(double n)
        {
            double x = Math.Sin(n * 127.1 + 311.7) * 43758.5453;
            return x - Math.Floor(x);
        }

        /// <summary>지붕 i(side ±1): 반환 (x 기준, y 바닥, 폭, 높이, kk). 표 6.11.2 5번.</summary>
        public void Roof(int i, int sd, double T, double sp, double W, double H, out double x0, out double y, out double bw, out double bh, out double kk, out double hh)
        {
            double raw = (double)i / RoofCount + T * sp * 0.22 + (sd > 0 ? 0.05 : 0);
            double k = raw - Math.Floor(raw);
            kk = k * k;
            double vy = H * VanishY, vx = W / 2;
            y = vy + (H * 1.1 - vy) * kk;
            double hw = W * 0.07 + W * 0.62 * kk;
            double x = vx + sd * (hw + W * 0.04 + W * 0.25 * kk);
            bw = W * (0.06 + 0.3 * kk);
            bh = H * (0.03 + 0.2 * kk);
            x0 = sd < 0 ? x - bw : x;
            hh = Hash(i * 3.1 + (sd > 0 ? 9 : 0) + Math.Floor(raw) * 1.7);
        }

        /// <summary>계단 띠 k(0~1.1)의 y·반폭(`yk`·`hk`).</summary>
        public void StepAt(double k, double W, double H, out double y, out double hw)
        {
            double vy = H * VanishY, p = Math.Pow(k, 1.9);
            y = vy + (H * 1.08 - vy) * p;
            hw = W * 0.06 + W * 0.58 * p;
        }

        /// <summary>종료 섬광: 흰 알파·링 굵기·반지름(wo = 경과/0.7).</summary>
        public static void Out(double wo, double W, double H, out double whiteA, out double ringW, out double ringR)
        {
            whiteA = (1 - wo) * 0.85;
            ringW = 14 * (1 - wo) + 2;
            ringR = Math.Sqrt(W * W + H * H) * wo * 0.7;
        }

        /// <summary>집중선 i(t = floor(ms/40)): 각, 시작 반지름, 끝 반지름, 굵기.</summary>
        public static void SpeedLine(int i, long t, double W, double H, out double ang, out double r0, out double r1, out double width)
        {
            ang = Hash(i * 1.7 + t * 0.13) * Math.PI * 2;
            r0 = Math.Min(W, H) * (0.42 + Hash(i + t) * 0.1);
            r1 = r0 + H * 0.2;
            width = 1 + Hash(i * 3 + t) * 3;
        }
    }
}
