using System.Collections.Generic;
using MV.Data;

namespace MV.Run
{
    /// <summary>런 동안 바뀌지 않는 표 상수 캐시(300_runConfig·330_climb·314_spawnGroup[clone]).</summary>
    public sealed class RunParams
    {
        public double DtCap, HitStopScale;
        public double LaneXMax, KeyMoveSpeed, MoveFollowK, RunSpeed, PhaseRun, PhaseBlocked, PhaseClimb;
        public double PlayerHalfW, WallFaceGap, WallFaceEps, WarnDist;
        /// <summary>중간 구역 전진 속도(MV-SPEC-02 4-1, Q-15), 결계 막힘 뒤 종료 초(6-8, Q-13).</summary>
        public double RunSpeedMid, StuckEndSec;
        /// <summary>지팡이 스택 칸 수(6-3, Q-17). 0 = 상한 없음.</summary>
        public int WeaponStackMax;
        public double[] FirePhase;
        public int MaxClones;
        public double CloneXMax, DblShotDx, ProjSpawnDz, ThrowT, ProjSpeed, DmgMin, DmgMax;
        public double WellFlushBehind, WellBrickDiv, BrickFlushInterval;
        public int WellBrickMax, CpMax, PlayableAreas, MergeN;
        public double BurnTick, ChainFireBox, StormLife, StormK0, StormDk, StormPickDz, StormBox, StormJitTarget, StormJitFree, BlastBox;
        public double MeteorMinDz, MeteorMaxDz, MeteorFall, MeteorBox, OrbZBack;
        public readonly List<(double dx, double dz)> CloneOff = new List<(double, double)>();
        public ClimbData Climb;
        public IReadOnlyList<ClimbData> WarpStages;

        public static RunParams From(MvTables t)
        {
            var c = t.RunConfig;
            var p = new RunParams
            {
                DtCap = c.D(RunConfigKeys.Time_DtCap), HitStopScale = c.D(RunConfigKeys.Time_HitStopScale),
                LaneXMax = c.D(RunConfigKeys.LANE_X_MAX), KeyMoveSpeed = c.D(RunConfigKeys.KEY_MOVE_SPEED),
                MoveFollowK = c.D(RunConfigKeys.MOVE_FOLLOW_K), RunSpeed = c.D(RunConfigKeys.RUN_SPEED),
                PhaseRun = c.D(RunConfigKeys.PHASE_RUN), PhaseBlocked = c.D(RunConfigKeys.PHASE_BLOCKED),
                PhaseClimb = c.D(RunConfigKeys.PHASE_CLIMB),
                PlayerHalfW = c.D(RunConfigKeys.PLAYER_HALF_W), WallFaceGap = c.D(RunConfigKeys.WALL_FACE_GAP),
                WallFaceEps = c.D(RunConfigKeys.WALL_FACE_EPS), WarnDist = c.D(RunConfigKeys.WARN_DIST),
                RunSpeedMid = c.D(RunConfigKeys.RUN_SPEED_MID), StuckEndSec = c.D(RunConfigKeys.STUCK_END_SEC),
                WeaponStackMax = c.I(RunConfigKeys.WEAPON_STACK_MAX),
                MaxClones = RunFormulas.CloneCap(t), CloneXMax = c.D(RunConfigKeys.CLONE_X_MAX),
                DblShotDx = c.D(RunConfigKeys.DBL_SHOT_DX), ProjSpawnDz = c.D(RunConfigKeys.PROJ_SPAWN_DZ),
                ThrowT = c.D(RunConfigKeys.THROW_T), ProjSpeed = c.D(RunConfigKeys.PROJ_SPEED),
                DmgMin = c.D(RunConfigKeys.DMG_MIN), DmgMax = c.D(RunConfigKeys.DMG_MAX),
                WellFlushBehind = c.D(RunConfigKeys.WELL_FLUSH_BEHIND), WellBrickDiv = c.D(RunConfigKeys.WELL_BRICK_DIV),
                WellBrickMax = c.I(RunConfigKeys.WELL_BRICK_MAX), BrickFlushInterval = c.D(RunConfigKeys.Fx_BrickFlushInterval),
                CpMax = c.I(RunConfigKeys.CP_MAX), PlayableAreas = c.I(RunConfigKeys.PLAYABLE_AREAS), MergeN = c.I(RunConfigKeys.MERGE_N),
                BurnTick = c.D(RunConfigKeys.BURN_TICK), ChainFireBox = c.D(RunConfigKeys.CHAIN_FIRE_BOX),
                StormLife = c.D(RunConfigKeys.STORM_LIFE), StormK0 = c.D("STORM_K0"), StormDk = c.D(RunConfigKeys.STORM_DK),
                StormPickDz = c.D(RunConfigKeys.STORM_PICK_DZ), StormBox = c.D(RunConfigKeys.STORM_BOX),
                StormJitTarget = c.D(RunConfigKeys.STORM_JIT_TARGET), StormJitFree = c.D(RunConfigKeys.STORM_JIT_FREE), BlastBox = c.D(RunConfigKeys.BLAST_BOX),
                MeteorMinDz = c.D(RunConfigKeys.METEOR_MIN_DZ), MeteorMaxDz = c.D(RunConfigKeys.METEOR_MAX_DZ),
                MeteorFall = c.D(RunConfigKeys.METEOR_FALL), MeteorBox = c.D(RunConfigKeys.METEOR_BOX), OrbZBack = c.D(RunConfigKeys.ORB_Z_BACK),
            };
            var ph = c.List(RunConfigKeys.FIRE_PHASE);
            p.FirePhase = new double[ph.Count];
            for (int i = 0; i < ph.Count; i++) p.FirePhase[i] = ph[i];
            foreach (var g in t.SpawnGroup.Sorted)
                if (g.key == "clone") p.CloneOff.Add((g.dx, g.dz));
            p.Climb = t.Climb.Config();
            p.WarpStages = t.Climb.WarpStages();
            return p;
        }

        /// <summary>`prefix.amp`/`prefix.time` 흔들림 키 쌍.</summary>
        public static (double amp, double time) Shake(MvTables t, string prefix) =>
            string.IsNullOrEmpty(prefix) ? (0, 0) : (t.RunConfig.D(prefix + ".amp"), t.RunConfig.D(prefix + ".time"));
    }
}
