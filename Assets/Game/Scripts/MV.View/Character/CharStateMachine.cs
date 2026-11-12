using System;
using MV.Data;

namespace MV.View
{
    /// <summary>캐릭터 상태(MV-SPEC-02 ArtResourcePlan §5.1). 플레이어·분신 같은 것.</summary>
    public enum CharState : byte
    {
        /// <summary>그리지 않는다(분신 자리 비활성, 런 없음).</summary>
        Hidden,
        /// <summary>서 있음(런 끝·막힘): 왼발 그림, 내려감 없음.</summary>
        Stand,
        /// <summary>걷기: A·B 교대, 칸 안에서 내려갔다 올라옴. 공격(쏜 순간부터 정해진 칸 수)이 겹친다.</summary>
        Walk,
        /// <summary>기쁨(게이트 통과·계단 Lv 상승): happy 그림을 세로축으로 1바퀴 돌린다. 걷기·서 있음 위에 겹치고 끝나면 돌아간다.</summary>
        Happy,
        /// <summary>계단 정산: stair 그림 고정, 흔들림은 코드(디자이너 PDF 「정산 옆모습」).</summary>
        Stair,
    }

    /// <summary>그릴 그림 칸(116_charLook 열).</summary>
    public enum CharPose : byte { WalkA, WalkB, AtkA, AtkB, Happy, Stair }

    /// <summary>재생 값(`300_runConfig` `char.*`, 디자이너 PDF 재생 규칙·웹 프로토). 표를 읽은 뒤 한 번 만든다.</summary>
    public readonly struct CharAnimParams
    {
        /// <summary>그림 한 칸 시간(s).</summary>
        public readonly double FrameSec;
        /// <summary>걷기 내려감이 가장 깊은 시점(한 칸 안 비율 0~1).</summary>
        public readonly double DipAt;
        /// <summary>걷기 내려감 깊이(m).</summary>
        public readonly double DipM;
        /// <summary>공격 그림 유지 칸 수.</summary>
        public readonly int AtkFrames;
        /// <summary>기쁨 세로축 1바퀴 시간(s).</summary>
        public readonly double HappySpinSec;
        /// <summary>계단 정산 흔들림: 오르는 중 각속도(rad/s)·기울기(rad)·위아래 튐(m)·좌우(m), 오르기 전 각속도·기울기.</summary>
        public readonly double StairRate, StairRockRad, StairBobM, StairSwayM, StairIdleRate, StairIdleRockRad;

        public CharAnimParams(double frameSec, double dipAt, double dipM, int atkFrames, double happySpinSec,
            double stairRate, double stairRockRad, double stairBobM, double stairSwayM, double stairIdleRate, double stairIdleRockRad)
        {
            if (!(frameSec > 0)) throw new ArgumentOutOfRangeException(nameof(frameSec), frameSec, RunConfigKeys.Char_FrameSec + " > 0");
            if (!(dipAt > 0 && dipAt < 1)) throw new ArgumentOutOfRangeException(nameof(dipAt), dipAt, RunConfigKeys.Char_DipAt + " 0~1 사이");
            if (!(happySpinSec > 0)) throw new ArgumentOutOfRangeException(nameof(happySpinSec), happySpinSec, RunConfigKeys.Char_HappySpinSec + " > 0");
            FrameSec = frameSec;
            DipAt = dipAt;
            DipM = dipM;
            AtkFrames = atkFrames;
            HappySpinSec = happySpinSec;
            StairRate = stairRate;
            StairRockRad = stairRockRad;
            StairBobM = stairBobM;
            StairSwayM = stairSwayM;
            StairIdleRate = stairIdleRate;
            StairIdleRockRad = stairIdleRockRad;
        }

        public static CharAnimParams From(MvTables t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var c = t.RunConfig;
            return new CharAnimParams(c.D(RunConfigKeys.Char_FrameSec), c.D(RunConfigKeys.Char_DipAt), c.D(RunConfigKeys.Char_DipM), (int)c.D(RunConfigKeys.Char_AtkFrames),
                c.D(RunConfigKeys.Char_HappySpinSec), c.D(RunConfigKeys.Char_StairRate), c.D(RunConfigKeys.Char_StairRockRad), c.D(RunConfigKeys.Char_StairBobM),
                c.D(RunConfigKeys.Char_StairSwayM), c.D(RunConfigKeys.Char_StairIdleRate), c.D(RunConfigKeys.Char_StairIdleRockRad));
        }
    }

    /// <summary>
    /// 한 프레임에 그릴 것(카메라 평면 기준): 상태, 그림 칸, 내려감(m, 아래 +), 좌우(m, 오른쪽 +), 기울기(rad, 화면 시계 방향 +),
    /// 가로 배율(세로축 회전 = cos, 음수면 뒷면 차례).
    /// </summary>
    public readonly struct CharFrame
    {
        public readonly CharState State;
        public readonly CharPose Pose;
        public readonly double Dip, SwayX, Roll, FlipX;

        public CharFrame(CharState state, CharPose pose, double dip, double swayX = 0, double roll = 0, double flipX = 1)
        {
            State = state;
            Pose = pose;
            Dip = dip;
            SwayX = swayX;
            Roll = roll;
            FlipX = flipX;
        }
    }

    /// <summary>
    /// 캐릭터 상태머신 1개(캐릭터 1마리). 순수 계산이라 EditMode로 시험한다. 시각은 뷰 시계(초).
    /// 걷기(디자이너 PDF 「재생 규칙」): 칸 = floor(now / frameSec + unit), A·B 교대. unit(0 플레이어, 1.. 분신)만큼 칸을 어긋나게 해 분신이 같이 걷지 않는다(웹 프로토 `newCatFrame(run, off)`).
    /// 공격: 쏜 시각부터 frameSec·atkFrames 동안, 그 칸이 A면 atkA(C), B면 atkB(D). 내려감: 칸 안 dipAt 지점에서 dipM 깊이(사인 반 주기, 앞뒤를 dipAt에서 나눈다).
    /// 기쁨(웹 프로토 `paperSpin`·`spinFx`): happySpinSec 동안 가로 배율 = cos(2π·e), e = 1 − (1 − k)³. 걷기·서 있음 위에서만 돌고, 다시 받으면 처음부터.
    /// 계단(웹 프로토 `drawClimbCat`): 오르는 중이면 s = sin(now·stairRate)로 기울기 s·stairRockRad, 위로 |s|·stairBobM, 좌우 s·stairSwayM.
    /// 오르기 전이면 기울기 sin(now·stairIdleRate)·stairIdleRockRad만.
    /// </summary>
    public sealed class CharStateMachine
    {
        double _shotAt = double.NegativeInfinity;
        double _happyAt = double.NegativeInfinity;

        /// <summary>바탕 상태(뷰가 정한다). 기쁨은 이 위에 겹쳐 <see cref="CharFrame.State"/>로 나온다.</summary>
        public CharState State { get; private set; } = CharState.Hidden;
        public double EnteredAt { get; private set; }
        /// <summary>계단 상태에서 오르는 중인지(뷰가 매 프레임 넣는다).</summary>
        public bool Climbing { get; set; }

        /// <summary>상태를 바꾼다. 같은 상태면 그대로(들어간 시각 유지).</summary>
        public void Set(CharState state, double now)
        {
            if (state == State) return;
            State = state;
            EnteredAt = now;
        }

        /// <summary>쏜 순간(런 이벤트 Fire).</summary>
        public void Shot(double now) => _shotAt = now;

        /// <summary>기쁨 시작(게이트 통과·계단 Lv 상승). 돌던 중이면 처음부터.</summary>
        public void Happy(double now) => _happyAt = now;

        public void Reset()
        {
            State = CharState.Hidden;
            EnteredAt = 0;
            Climbing = false;
            _shotAt = double.NegativeInfinity;
            _happyAt = double.NegativeInfinity;
        }

        public CharFrame Evaluate(in CharAnimParams p, double now, int unit)
        {
            switch (State)
            {
                case CharState.Hidden: return new CharFrame(CharState.Hidden, CharPose.WalkA, 0);
                case CharState.Stair: return StairFrame(p, now, Climbing);
            }
            double k = (now - _happyAt) / p.HappySpinSec;
            if (k >= 0 && k < 1) return new CharFrame(CharState.Happy, CharPose.Happy, 0, flipX: SpinFlip(k));
            if (State != CharState.Walk) return new CharFrame(State, CharPose.WalkA, 0);
            double steps = now / p.FrameSec + unit;
            double step = Math.Floor(steps);
            bool b = ((long)step & 1) == 1;
            bool atk = now >= _shotAt && now - _shotAt < p.FrameSec * p.AtkFrames;
            var pose = atk ? (b ? CharPose.AtkB : CharPose.AtkA) : (b ? CharPose.WalkB : CharPose.WalkA);
            return new CharFrame(CharState.Walk, pose, DipCurve(steps - step, p.DipAt) * p.DipM);
        }

        static CharFrame StairFrame(in CharAnimParams p, double now, bool climbing)
        {
            if (!climbing) return new CharFrame(CharState.Stair, CharPose.Stair, 0, roll: Math.Sin(now * p.StairIdleRate) * p.StairIdleRockRad);
            double w = Math.Sin(now * p.StairRate);
            return new CharFrame(CharState.Stair, CharPose.Stair, -Math.Abs(w) * p.StairBobM, w * p.StairSwayM, w * p.StairRockRad);
        }

        /// <summary>기쁨 진행 k(0~1) → 가로 배율. 감속(ease-out 3제곱)으로 1바퀴. 범위 밖이면 1.</summary>
        public static double SpinFlip(double k)
        {
            if (k <= 0 || k >= 1) return 1;
            double e = 1 - Math.Pow(1 - k, 3);
            return Math.Cos(e * Math.PI * 2);
        }

        /// <summary>칸 안 위치 u(0~1) → 내려감 비율 0~1. u = dipAt에서 1, 칸 처음·끝에서 0.</summary>
        public static double DipCurve(double u, double dipAt)
        {
            if (u <= 0 || u >= 1) return 0;
            return u < dipAt ? Math.Sin(Math.PI / 2 * u / dipAt) : Math.Sin(Math.PI / 2 * (1 - u) / (1 - dipAt));
        }

        /// <summary>그림 칸 → 116_charLook 행의 그림 이름.</summary>
        public static string SpriteOf(CharLookData row, CharPose pose)
        {
            if (row == null) return null;
            switch (pose)
            {
                case CharPose.WalkB: return row.walkB;
                case CharPose.AtkA: return row.atkA;
                case CharPose.AtkB: return row.atkB;
                case CharPose.Happy: return row.happy;
                case CharPose.Stair: return row.stair;
                default: return row.walkA;
            }
        }
    }
}
