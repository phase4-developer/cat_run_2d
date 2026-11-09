using MV.Data;

namespace MV.View
{
    /// <summary>
    /// `300_runConfig`의 뷰·정렬 키(기획서 02 §2.2.6, §2.3.2). 표를 읽은 뒤 한 번 만든다(매 프레임 키 조회 금지).
    /// 값은 모두 표에서 오고 코드에는 기본값이 없다.
    /// </summary>
    public sealed class ViewConfig
    {
        public double NearCut, FogStart, FogRange;
        public double ObjCullBack, ObjCullFront, ObjDrawBack, ObjDrawFront, PropDrawBack, PropDrawFront;
        public double GroundNear, GroundFar, GroundStep;
        public int MixQuant;
        public double ClothFrom, ClothTo, ClothStep, ClothSkip, ClothY;
        public double LightAlpha;
        public int SnowCount;
        public double RefWidthPx, RefHeightPx;
        public string OutlineColor;
        public double PlayerCatScale, CloneCatScale;
        public double SortQuant, ObjProbeY, CatProbeY, SortCloth, SortLabel, SortBlade, SortProj;

        /// <summary>
        /// 계단 오르기 출렁임 높이 m = `330_climb` 설정 행 `bumpH`(정본 L2758 `sin(frac·π)·0.12`, 07 §7.3.5). 설정 행이 없으면 0.
        /// </summary>
        public static double ClimbBumpOf(ClimbTable climb)
        {
            var c = climb?.Config();
            return c != null ? c["bumpH"] : 0;
        }

        /// <summary>
        /// 옛 상수(정본 0.12). 교체 대기: <see cref="RunStateViewSource"/>(MV-PLAY-INT가 쓰는 중)가 <see cref="ClimbBumpOf"/>로 바꾸면 지운다.
        /// </summary>
        public const double ClimbBump = 0.12;

        public static ViewConfig From(RunConfigTable t)
        {
            return new ViewConfig
            {
                NearCut = t.D(RunConfigKeys.View_NearCut),
                FogStart = t.D(RunConfigKeys.View_FogStart),
                FogRange = t.D(RunConfigKeys.View_FogRange),
                ObjCullBack = t.D(RunConfigKeys.View_ObjCullBack),
                ObjCullFront = t.D(RunConfigKeys.View_ObjCullFront),
                ObjDrawBack = t.D(RunConfigKeys.View_ObjDrawBack),
                ObjDrawFront = t.D(RunConfigKeys.View_ObjDrawFront),
                PropDrawBack = t.D(RunConfigKeys.View_PropDrawBack),
                PropDrawFront = t.D(RunConfigKeys.View_PropDrawFront),
                GroundNear = t.D(RunConfigKeys.View_GroundNear),
                GroundFar = t.D(RunConfigKeys.View_GroundFar),
                GroundStep = t.D(RunConfigKeys.View_GroundStep),
                MixQuant = t.I(RunConfigKeys.View_MixQuant),
                ClothFrom = t.D(RunConfigKeys.View_ClothFrom),
                ClothTo = t.D(RunConfigKeys.View_ClothTo),
                ClothStep = t.D(RunConfigKeys.View_ClothStep),
                ClothSkip = t.D(RunConfigKeys.View_ClothSkip),
                ClothY = t.D(RunConfigKeys.View_ClothY),
                LightAlpha = t.D(RunConfigKeys.View_LightAlpha),
                SnowCount = t.I(RunConfigKeys.View_SnowCount),
                RefWidthPx = t.D(RunConfigKeys.View_RefWidthPx),
                RefHeightPx = t.D(RunConfigKeys.View_RefHeightPx),
                OutlineColor = t.Raw(RunConfigKeys.View_OutlineColor),
                PlayerCatScale = t.D(RunConfigKeys.View_PlayerCatScale),
                CloneCatScale = t.D(RunConfigKeys.View_CloneCatScale),
                SortQuant = t.D(RunConfigKeys.Sort_Quant),
                ObjProbeY = t.D(RunConfigKeys.Sort_ObjProbeY),
                CatProbeY = t.D(RunConfigKeys.Sort_CatProbeY),
                SortCloth = t.D(RunConfigKeys.Sort_Cloth),
                SortLabel = t.D(RunConfigKeys.Sort_Label),
                SortBlade = t.D(RunConfigKeys.Sort_Blade),
                SortProj = t.D(RunConfigKeys.Sort_Proj),
            };
        }
    }
}
