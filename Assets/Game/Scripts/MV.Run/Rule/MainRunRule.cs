using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.Run
{
    /// <summary>
    /// 정본 런 규칙(04 §4.20.2). 훅은 정본 동작을 그대로 하거나 아무것도 하지 않는다.
    /// 본편 동일 조건: 이 규칙 + 모드 필드 기본값 + 모디파이어 0 + 동행 없음이면 RunResult·이벤트 열·RunRng 소비가 정본과 같다.
    /// </summary>
    public sealed class MainRunRule : IRunRule
    {
        readonly IRunLevelBuilder _builder;

        public MainRunRule(IRunLevelBuilder builder)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        /// <summary>스냅숏 복사본에 메타 능력치·동행을 그대로 넣는다.</summary>
        public RunSetup Build(RunContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (ctx.Snapshot == null) throw new ArgumentException("RunContext.Snapshot이 없습니다", nameof(ctx));
            var s = ctx.Snapshot.Clone();
            s.Modifiers = ctx.MetaModifiers;
            s.Companions = ctx.Companions != null ? new List<CompanionSpec>(ctx.Companions) : new List<CompanionSpec>();
            return s;
        }

        /// <summary>정본 `buildLevel(0, level, saved)`(§4.14.4) 그대로.</summary>
        public void BuildCourse(RunState s, CourseRequest req) => _builder.BuildLevel(s, req.BaseZ, req.Level, req.Saved);

        public void OnHit(RunState s, in HitInfo h) { }

        public void Tick(RunState s, double dt) { }

        public RunEnd CheckEnd(RunState s) => RunEnd.None;

        public RunResult Settle(RunState s, FinishReason reason) => RunSettlement.Main(s, reason, _builder);

        public void OnEvent(RunState s, in RunEvent e) { }
    }

    /// <summary>정본 정산 계산(04 §4.11.1, §4.7.4). 모드 규칙도 이 결과를 그대로 넘기고 SettleMode에서 모드 보상을 계산한다.</summary>
    public static class RunSettlement
    {
        /// <summary>
        /// finish(reason)의 결과 계산. 장애물 기록·체크포인트(`s.CpRun` 갱신)·개방·목숨 조각.
        /// `useCheckpoint`·`keepBars` = 0이면 CpRun 0·BarRec null(§4.20.3). Pass는 정본 계산 그대로(모드에선 무시).
        /// </summary>
        public static RunResult Main(RunState s, FinishReason reason, IRunLevelBuilder builder)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var t = s.Tables;
            var rec = s.SavedBars != null ? s.SavedBars.Clone() : new BarRecord();
            foreach (var o in s.Objs) if (o.IsField && o.Lvl == s.Level) rec.Set(o.Bar, o.Idx, Math.Max(0, o.Hp));
            int cpMax = t.RunConfig.I(RunConfigKeys.CP_MAX);
            int cpN = 0;
            while (cpN < cpMax)
            {
                var (have, tot) = FloorSouls(builder, s.Area, rec, cpN);
                if (have < tot) break;
                cpN++;
            }
            s.CpRun = Math.Max(s.Cp, cpN);
            int need = 0;
            foreach (var a in t.Area.Datas) if (a.areaNo == s.Area + 1) need = a.needSouls;
            bool gate = reason == FinishReason.Gate, last = s.Area >= t.RunConfig.I(RunConfigKeys.PLAYABLE_AREAS) - 1;
            bool enough = s.Setup.Souls + s.Souls >= need;
            var r = new RunResult
            {
                // 「레벨당 골드·블루 마법석 획득」(13 GoldPerLevel·DiaPerLevel)은 런 종료에 더한다(MV-SPEC-02 529~530).
                Reason = reason, Souls = s.Souls,
                Gold = s.Gold + RunFormulas.PerLevelReward(t, s.Setup, AreaStatKey.GoldPerLevel, s.Level),
                Dia = s.Dia + (int)Math.Floor(RunFormulas.PerLevelReward(t, s.Setup, AreaStatKey.DiaPerLevel, s.Level)),
                ZoneEnd = gate && enough && last, Pass = gate && enough && !last,
                CpRun = s.Setup.UseCheckpoint ? s.CpRun : 0, BarRec = s.Setup.KeepBars ? rec : null,
                Climbed = s.Climbed, Walls = s.Walls, GaugeLv = s.GaugeLv, EqTier = s.EqTier(), Clones = s.Clones,
                CardCount = s.CardOrder.Count, HitWallBar = s.HitWall?.Bar ?? -1,
            };
            r.Tok = RunFormulas.Tokens(t, s.Souls, gate, r.Pass || r.ZoneEnd);
            r.PendingSettle.AddRange(s.PendingSettle);
            return r;
        }

        static (int have, int tot) FloorSouls(IRunLevelBuilder builder, int area, BarRecord rec, int f)
        {
            int have = 0, tot = 0;
            foreach (var c in builder.FieldSpec(area, f))
            {
                if (c.Rew != FieldReward.Soul) continue;
                tot += c.Amt;
                if (rec.TryGet(f, c.Idx, out var hp) && hp <= 0) have += c.Amt;
            }
            return (have, tot);
        }
    }
}
