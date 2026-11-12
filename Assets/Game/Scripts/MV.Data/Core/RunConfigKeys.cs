// 생성 파일: scratch build_tsv.py가 300_runConfig.txt에서 만든다. 키를 더하면 TSV와 이 목록을 같이 고친다(07 §7.3.3).
namespace MV.Data
{
    /// <summary>`300_runConfig` 키 상수. 코드는 이 상수로만 키를 쓴다. 검증기가 TSV와 대조한다(누락·남음 = 오류).</summary>
    public static class RunConfigKeys
    {
        public const string Time_DtCap = "time.dtCap";
        public const string Time_HitStopScale = "time.hitStopScale";
        public const string Time_TargetFps = "time.targetFps";
        public const string View_NearCut = "view.nearCut";
        public const string View_FogStart = "view.fogStart";
        public const string View_FogRange = "view.fogRange";
        public const string View_ObjCullBack = "view.objCullBack";
        public const string View_ObjCullFront = "view.objCullFront";
        public const string View_ObjDrawBack = "view.objDrawBack";
        public const string View_ObjDrawFront = "view.objDrawFront";
        public const string View_PropDrawBack = "view.propDrawBack";
        public const string View_PropDrawFront = "view.propDrawFront";
        public const string View_GroundNear = "view.groundNear";
        public const string View_GroundFar = "view.groundFar";
        public const string View_GroundStep = "view.groundStep";
        public const string View_MixQuant = "view.mixQuant";
        public const string View_ClothFrom = "view.clothFrom";
        public const string View_ClothTo = "view.clothTo";
        public const string View_ClothStep = "view.clothStep";
        public const string View_ClothSkip = "view.clothSkip";
        public const string View_ClothY = "view.clothY";
        public const string View_LightAlpha = "view.lightAlpha";
        public const string View_SnowCount = "view.snowCount";
        public const string View_RefWidthPx = "view.refWidthPx";
        public const string View_RefHeightPx = "view.refHeightPx";
        public const string View_OutlineColor = "view.outlineColor";
        public const string View_PlayerCatScale = "view.playerCatScale";
        public const string View_CloneCatScale = "view.cloneCatScale";
        public const string Sort_Quant = "sort.quant";
        public const string Sort_ObjProbeY = "sort.objProbeY";
        public const string Sort_CatProbeY = "sort.catProbeY";
        public const string Sort_Cloth = "sort.cloth";
        public const string Sort_Label = "sort.label";
        public const string Sort_Blade = "sort.blade";
        public const string Sort_Proj = "sort.proj";
        public const string Shake_WallBreak_Amp = "shake.wallBreak.amp";
        public const string Shake_WallBreak_Time = "shake.wallBreak.time";
        public const string Shake_Steal_Amp = "shake.steal.amp";
        public const string Shake_Steal_Time = "shake.steal.time";
        public const string Shake_Blocked_Amp = "shake.blocked.amp";
        public const string Shake_Blocked_Time = "shake.blocked.time";
        public const string Shake_GodBlast_Amp = "shake.godBlast.amp";
        public const string Shake_GodBlast_Time = "shake.godBlast.time";
        public const string Shake_ClimbLap_Amp = "shake.climbLap.amp";
        public const string Shake_ClimbLap_Time = "shake.climbLap.time";
        public const string Shake_ClimbTier_Amp = "shake.climbTier.amp";
        public const string Shake_ClimbTier_Time = "shake.climbTier.time";
        public const string Shake_WarpStage_Amp = "shake.warpStage.amp";
        public const string Shake_WarpStage_Time = "shake.warpStage.time";
        public const string Shake_HolyBeam_Amp = "shake.holyBeam.amp";
        public const string Shake_HolyBeam_Time = "shake.holyBeam.time";
        public const string Shake_Meteor_Amp = "shake.meteor.amp";
        public const string Shake_Meteor_Time = "shake.meteor.time";
        public const string Hitstop_EnemyKill = "hitstop.enemyKill";
        public const string Hitstop_WallBreak = "hitstop.wallBreak";
        public const string Hitstop_CoinBreak = "hitstop.coinBreak";
        public const string Fx_SparkCapThreshold = "fx.sparkCapThreshold";
        public const string Fx_SparkCapCount = "fx.sparkCapCount";
        public const string Fx_MaxHandles = "fx.maxHandles";
        public const string Fx_TrailInterval = "fx.trailInterval";
        public const string Fx_BrickFlushInterval = "fx.brickFlushInterval";
        public const string Fx_TowerHitChance = "fx.towerHitChance";
        public const string Pop_Max = "pop.max";
        public const string Pop_Life = "pop.life";
        public const string Pop_Rise = "pop.rise";
        public const string Pop_ScaleIn = "pop.scaleIn";
        public const string Pop_ScaleBoost = "pop.scaleBoost";
        public const string Pop_FadeFrom = "pop.fadeFrom";
        public const string Pop_FontU = "pop.fontU";
        public const string Pop_CritColor = "pop.critColor";
        public const string Fly_Time = "fly.time";
        public const string Fly_RemoveMs = "fly.removeMs";
        public const string Fly_Bezier = "fly.bezier";
        public const string Fly_StartScale = "fly.startScale";
        public const string Fly_EndScale = "fly.endScale";
        public const string Fly_EndAlpha = "fly.endAlpha";
        public const string Fly_CloneGapMs = "fly.cloneGapMs";
        public const string FlyCard_Time = "flyCard.time";
        public const string FlyCard_RemoveMs = "flyCard.removeMs";
        public const string FlyCard_Bezier = "flyCard.bezier";
        public const string Banner_Time = "banner.time";
        public const string Toast_Time = "toast.time";
        public const string Toast_Fade = "toast.fade";
        public const string Flash_Fade = "flash.fade";
        public const string Flash_GodBlast = "flash.godBlast";
        public const string Flash_GodBlastMs = "flash.godBlastMs";
        public const string Flash_ClimbTier = "flash.climbTier";
        public const string Flash_ClimbTierMs = "flash.climbTierMs";
        public const string Warp_FadeIn = "warp.fadeIn";
        public const string Warp_VanishY = "warp.vanishY";
        public const string Warp_RoofCount = "warp.roofCount";
        public const string Warp_StepCount = "warp.stepCount";
        public const string Warp_StageNameMs = "warp.stageNameMs";
        public const string Warp_OutMs = "warp.outMs";
        public const string Warp_SpeedLines = "warp.speedLines";
        public const string DRAG_SCREEN_M = "DRAG_SCREEN_M";
        public const string LANE_X_MAX = "LANE_X_MAX";
        public const string KEY_MOVE_SPEED = "KEY_MOVE_SPEED";
        public const string MOVE_FOLLOW_K = "MOVE_FOLLOW_K";
        public const string RUN_SPEED = "RUN_SPEED";
        /// <summary>중간 구역 전진 속도(MV-SPEC-02 4-1, Q-15). 구간 위치는 <c>310_template.midFromZ·midToZ</c>.</summary>
        public const string RUN_SPEED_MID = "RUN_SPEED_MID";
        public const string PHASE_RUN = "PHASE_RUN";
        public const string PHASE_BLOCKED = "PHASE_BLOCKED";
        public const string PHASE_CLIMB = "PHASE_CLIMB";
        public const string CLIMB_X_FOLLOW_K = "CLIMB_X_FOLLOW_K";
        public const string PLAYER_HALF_W = "PLAYER_HALF_W";
        public const string WALL_FACE_GAP = "WALL_FACE_GAP";
        public const string WALL_FACE_EPS = "WALL_FACE_EPS";
        public const string WARN_DIST = "WARN_DIST";
        public const string FIRE_PHASE = "FIRE_PHASE";
        public const string MAX_CLONES = "MAX_CLONES";
        public const string CLONE_X_MAX = "CLONE_X_MAX";
        public const string DBL_SHOT_DX = "DBL_SHOT_DX";
        public const string PROJ_SPAWN_DZ = "PROJ_SPAWN_DZ";
        public const string THROW_T = "THROW_T";
        public const string PROJ_SPEED = "PROJ_SPEED";
        public const string PROJ_RANGE = "PROJ_RANGE";
        public const string DMG_MIN = "DMG_MIN";
        public const string DMG_MAX = "DMG_MAX";
        public const string INCOME_PER_LV = "INCOME_PER_LV";
        public const string ENEMY_X_MAX = "ENEMY_X_MAX";
        public const string WELL_FLUSH_BEHIND = "WELL_FLUSH_BEHIND";
        public const string WELL_BRICK_MAX = "WELL_BRICK_MAX";
        public const string WELL_BRICK_DIV = "WELL_BRICK_DIV";
        public const string WELL_BRICK_LIFE = "WELL_BRICK_LIFE";
        public const string ORE_ICON_GAP_MS = "ORE_ICON_GAP_MS";
        public const string CRAFT_ICON_MAX = "CRAFT_ICON_MAX";
        public const string CP_MAX = "CP_MAX";
        public const string PLAYABLE_AREAS = "PLAYABLE_AREAS";
        public const string TOK_BASE = "TOK_BASE";
        public const string TOK_SOUL_DIV = "TOK_SOUL_DIV";
        public const string TOK_GATE = "TOK_GATE";
        public const string TOK_OPEN = "TOK_OPEN";
        public const string FAIL_SFX_DELAY_MS = "FAIL_SFX_DELAY_MS";
        public const string WEAPON_EXTRA_BUYS = "WEAPON_EXTRA_BUYS";
        public const string ARCH_LABEL_HIDE = "ARCH_LABEL_HIDE";
        public const string MERGE_N = "MERGE_N";
        /// <summary>지팡이 스택 칸 수(MV-SPEC-02 6-3, Q-17). 0 = 상한 없음(값은 기획 쪽 확인).</summary>
        public const string WEAPON_STACK_MAX = "WEAPON_STACK_MAX";
        /// <summary>결계에 막힌 뒤 종료까지 초(MV-SPEC-02 6-8, Q-13). 더미 값, 적용은 MV-RUN-SPEC2.</summary>
        public const string STUCK_END_SEC = "STUCK_END_SEC";
        public const string ORB_COUNT = "ORB_COUNT";
        public const string ORB_RADIUS = "ORB_RADIUS";
        public const string ORB_ANG_SPEED = "ORB_ANG_SPEED";
        public const string ORB_Z_BACK = "ORB_Z_BACK";
        public const string BURN_TICK = "BURN_TICK";
        public const string CHAIN_FIRE_BOX = "CHAIN_FIRE_BOX";
        public const string STORM_LIFE = "STORM_LIFE";
        public const string STORM_K0 = "STORM_K0";
        public const string STORM_DK = "STORM_DK";
        public const string STORM_PICK_DZ = "STORM_PICK_DZ";
        public const string STORM_BOX = "STORM_BOX";
        public const string BLAST_BOX = "BLAST_BOX";
        public const string METEOR_MIN_DZ = "METEOR_MIN_DZ";
        public const string METEOR_MAX_DZ = "METEOR_MAX_DZ";
        public const string METEOR_FALL = "METEOR_FALL";
        public const string METEOR_BOX = "METEOR_BOX";
        public const string BLADE_ANG_SPEED = "BLADE_ANG_SPEED";
        /// <summary>폭풍 착탄 x 흔들림 반폭(대상 있을 때, 04 §4.9.3 L2168).</summary>
        public const string STORM_JIT_TARGET = "STORM_JIT_TARGET";
        /// <summary>폭풍 착탄 x·z 흔들림 반폭(대상 없을 때, 04 §4.9.3 L2168).</summary>
        public const string STORM_JIT_FREE = "STORM_JIT_FREE";
        public const string Ui_Hand_Budget = "ui.hand.budget";
        public const string Ui_Hand_LeftMin = "ui.hand.leftMin";
        public const string Ui_Hand_DealCost = "ui.hand.dealCost";
        public const string Ui_Hand_UnitMin = "ui.hand.unitMin";
        public const string Ui_Hand_UnitMax = "ui.hand.unitMax";
        public const string Ui_Hand_DealGapMax = "ui.hand.dealGapMax";
        public const string Ui_Hand_DealGapMul = "ui.hand.dealGapMul";
        public const string Ui_Hand_MergeMul = "ui.hand.mergeMul";
        public const string Ui_Hand_MoveMul = "ui.hand.moveMul";
        public const string Ui_Hand_AfterMax = "ui.hand.afterMax";
        public const string Ui_Hand_AfterMul = "ui.hand.afterMul";
        /// <summary>캐릭터 그림 한 칸 시간(디자이너 PDF 재생 규칙, MV-SPEC-02 ArtResourcePlan §3).</summary>
        public const string Char_FrameSec = "char.frameSec";
        /// <summary>걷기 내려감 시점(한 칸 안 비율).</summary>
        public const string Char_DipAt = "char.dipAt";
        /// <summary>걷기 내려감 깊이(m).</summary>
        public const string Char_DipM = "char.dipM";
        /// <summary>공격 그림(C·D) 유지 칸 수.</summary>
        public const string Char_AtkFrames = "char.atkFrames";
        /// <summary>기쁨 포즈 세로축 1바퀴 시간(s).</summary>
        public const string Char_HappySpinSec = "char.happySpinSec";

        /// <summary>모든 키(02·06 그룹 001~008 + 07 그룹 101~106 + 캐릭터 107. 개수는 이 배열 길이. FLOOR_COUNT·LOBBY_STAIR_MAX_LV는 표 행으로 계산 — MvTables.FloorCount·StairMaxLv).</summary>
        public static readonly string[] All =
        {
            Time_DtCap,
            Time_HitStopScale,
            Time_TargetFps,
            View_NearCut,
            View_FogStart,
            View_FogRange,
            View_ObjCullBack,
            View_ObjCullFront,
            View_ObjDrawBack,
            View_ObjDrawFront,
            View_PropDrawBack,
            View_PropDrawFront,
            View_GroundNear,
            View_GroundFar,
            View_GroundStep,
            View_MixQuant,
            View_ClothFrom,
            View_ClothTo,
            View_ClothStep,
            View_ClothSkip,
            View_ClothY,
            View_LightAlpha,
            View_SnowCount,
            View_RefWidthPx,
            View_RefHeightPx,
            View_OutlineColor,
            View_PlayerCatScale,
            View_CloneCatScale,
            Sort_Quant,
            Sort_ObjProbeY,
            Sort_CatProbeY,
            Sort_Cloth,
            Sort_Label,
            Sort_Blade,
            Sort_Proj,
            Shake_WallBreak_Amp,
            Shake_WallBreak_Time,
            Shake_Steal_Amp,
            Shake_Steal_Time,
            Shake_Blocked_Amp,
            Shake_Blocked_Time,
            Shake_GodBlast_Amp,
            Shake_GodBlast_Time,
            Shake_ClimbLap_Amp,
            Shake_ClimbLap_Time,
            Shake_ClimbTier_Amp,
            Shake_ClimbTier_Time,
            Shake_WarpStage_Amp,
            Shake_WarpStage_Time,
            Shake_HolyBeam_Amp,
            Shake_HolyBeam_Time,
            Shake_Meteor_Amp,
            Shake_Meteor_Time,
            Hitstop_EnemyKill,
            Hitstop_WallBreak,
            Hitstop_CoinBreak,
            Fx_SparkCapThreshold,
            Fx_SparkCapCount,
            Fx_MaxHandles,
            Fx_TrailInterval,
            Fx_BrickFlushInterval,
            Fx_TowerHitChance,
            Pop_Max,
            Pop_Life,
            Pop_Rise,
            Pop_ScaleIn,
            Pop_ScaleBoost,
            Pop_FadeFrom,
            Pop_FontU,
            Pop_CritColor,
            Fly_Time,
            Fly_RemoveMs,
            Fly_Bezier,
            Fly_StartScale,
            Fly_EndScale,
            Fly_EndAlpha,
            Fly_CloneGapMs,
            FlyCard_Time,
            FlyCard_RemoveMs,
            FlyCard_Bezier,
            Banner_Time,
            Toast_Time,
            Toast_Fade,
            Flash_Fade,
            Flash_GodBlast,
            Flash_GodBlastMs,
            Flash_ClimbTier,
            Flash_ClimbTierMs,
            Warp_FadeIn,
            Warp_VanishY,
            Warp_RoofCount,
            Warp_StepCount,
            Warp_StageNameMs,
            Warp_OutMs,
            Warp_SpeedLines,
            DRAG_SCREEN_M,
            LANE_X_MAX,
            KEY_MOVE_SPEED,
            MOVE_FOLLOW_K,
            RUN_SPEED,
            RUN_SPEED_MID,
            PHASE_RUN,
            PHASE_BLOCKED,
            PHASE_CLIMB,
            CLIMB_X_FOLLOW_K,
            PLAYER_HALF_W,
            WALL_FACE_GAP,
            WALL_FACE_EPS,
            WARN_DIST,
            FIRE_PHASE,
            MAX_CLONES,
            CLONE_X_MAX,
            DBL_SHOT_DX,
            PROJ_SPAWN_DZ,
            THROW_T,
            PROJ_SPEED,
            PROJ_RANGE,
            DMG_MIN,
            DMG_MAX,
            INCOME_PER_LV,
            ENEMY_X_MAX,
            WELL_FLUSH_BEHIND,
            WELL_BRICK_MAX,
            WELL_BRICK_DIV,
            WELL_BRICK_LIFE,
            ORE_ICON_GAP_MS,
            CRAFT_ICON_MAX,
            CP_MAX,
            PLAYABLE_AREAS,
            TOK_BASE,
            TOK_SOUL_DIV,
            TOK_GATE,
            TOK_OPEN,
            FAIL_SFX_DELAY_MS,
            WEAPON_EXTRA_BUYS,
            ARCH_LABEL_HIDE,
            MERGE_N,
            WEAPON_STACK_MAX,
            STUCK_END_SEC,
            ORB_COUNT,
            ORB_RADIUS,
            ORB_ANG_SPEED,
            ORB_Z_BACK,
            BURN_TICK,
            CHAIN_FIRE_BOX,
            STORM_LIFE,
            STORM_K0,
            STORM_DK,
            STORM_PICK_DZ,
            STORM_BOX,
            BLAST_BOX,
            METEOR_MIN_DZ,
            METEOR_MAX_DZ,
            METEOR_FALL,
            METEOR_BOX,
            BLADE_ANG_SPEED,
            STORM_JIT_TARGET,
            STORM_JIT_FREE,
            Ui_Hand_Budget,
            Ui_Hand_LeftMin,
            Ui_Hand_DealCost,
            Ui_Hand_UnitMin,
            Ui_Hand_UnitMax,
            Ui_Hand_DealGapMax,
            Ui_Hand_DealGapMul,
            Ui_Hand_MergeMul,
            Ui_Hand_MoveMul,
            Ui_Hand_AfterMax,
            Ui_Hand_AfterMul,
            Char_FrameSec,
            Char_DipAt,
            Char_DipM,
            Char_AtkFrames,
            Char_HappySpinSec,
        };
    }
}
