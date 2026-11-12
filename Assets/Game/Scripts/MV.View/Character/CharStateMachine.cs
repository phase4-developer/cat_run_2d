using System;
using MV.Data;

namespace MV.View
{
    /// <summary>캐릭터 상태(MV-SPEC-02 ArtResourcePlan §5.1). 플레이어·분신 같은 것. Happy·Stair는 4단계에서 더한다.</summary>
    public enum CharState : byte
    {
        /// <summary>그리지 않는다(분신 자리 비활성, 런 없음).</summary>
        Hidden,
        /// <summary>서 있음(런 끝·막힘): 왼발 그림, 내려감 없음.</summary>
        Stand,
        /// <summary>걷기: A·B 교대, 칸 안에서 내려갔다 올라옴. 공격(쏜 순간부터 정해진 칸 수)이 겹친다.</summary>
        Walk,
    }

    /// <summary>그릴 그림 칸(116_charLook 열).</summary>
    public enum CharPose : byte { WalkA, WalkB, AtkA, AtkB }

    /// <summary>재생 값(`300_runConfig` `char.*`, 디자이너 PDF 재생 규칙). 표를 읽은 뒤 한 번 만든다.</summary>
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

        public CharAnimParams(double frameSec, double dipAt, double dipM, int atkFrames)
        {
            if (!(frameSec > 0)) throw new ArgumentOutOfRangeException(nameof(frameSec), frameSec, RunConfigKeys.Char_FrameSec + " > 0");
            if (!(dipAt > 0 && dipAt < 1)) throw new ArgumentOutOfRangeException(nameof(dipAt), dipAt, RunConfigKeys.Char_DipAt + " 0~1 사이");
            FrameSec = frameSec;
            DipAt = dipAt;
            DipM = dipM;
            AtkFrames = atkFrames;
        }

        public static CharAnimParams From(MvTables t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var c = t.RunConfig;
            return new CharAnimParams(c.D(RunConfigKeys.Char_FrameSec), c.D(RunConfigKeys.Char_DipAt), c.D(RunConfigKeys.Char_DipM), (int)c.D(RunConfigKeys.Char_AtkFrames));
        }
    }

    /// <summary>한 프레임에 그릴 것: 그림 칸과 내려감(m, 아래 +).</summary>
    public readonly struct CharFrame
    {
        public readonly CharPose Pose;
        public readonly double Dip;
        public CharFrame(CharPose pose, double dip) { Pose = pose; Dip = dip; }
    }

    /// <summary>
    /// 캐릭터 상태머신 1개(캐릭터 1마리). 순수 계산이라 EditMode로 시험한다. 시각은 뷰 시계(초).
    /// 걷기(디자이너 PDF 「재생 규칙」): 칸 = floor(now / frameSec + unit), A·B 교대. unit(0 플레이어, 1.. 분신)만큼 칸을 어긋나게 해 분신이 같이 걷지 않는다(웹 프로토 `newCatFrame(run, off)`).
    /// 공격: 쏜 시각부터 frameSec·atkFrames 동안, 그 칸이 A면 atkA(C), B면 atkB(D). 내려감: 칸 안 dipAt 지점에서 dipM 깊이(사인 반 주기, 앞뒤를 dipAt에서 나눈다).
    /// </summary>
    public sealed class CharStateMachine
    {
        double _shotAt = double.NegativeInfinity;

        public CharState State { get; private set; } = CharState.Hidden;
        public double EnteredAt { get; private set; }

        /// <summary>상태를 바꾼다. 같은 상태면 그대로(들어간 시각 유지).</summary>
        public void Set(CharState state, double now)
        {
            if (state == State) return;
            State = state;
            EnteredAt = now;
        }

        /// <summary>쏜 순간(런 이벤트 Fire).</summary>
        public void Shot(double now) => _shotAt = now;

        public void Reset()
        {
            State = CharState.Hidden;
            EnteredAt = 0;
            _shotAt = double.NegativeInfinity;
        }

        public CharFrame Evaluate(in CharAnimParams p, double now, int unit)
        {
            if (State != CharState.Walk) return new CharFrame(CharPose.WalkA, 0);
            double steps = now / p.FrameSec + unit;
            double step = Math.Floor(steps);
            bool b = ((long)step & 1) == 1;
            bool atk = now >= _shotAt && now - _shotAt < p.FrameSec * p.AtkFrames;
            var pose = atk ? (b ? CharPose.AtkB : CharPose.AtkA) : (b ? CharPose.WalkB : CharPose.WalkA);
            return new CharFrame(pose, DipCurve(steps - step, p.DipAt) * p.DipM);
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
                default: return row.walkA;
            }
        }
    }
}
