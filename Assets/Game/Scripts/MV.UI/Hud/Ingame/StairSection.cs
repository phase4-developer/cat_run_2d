using System;
using jjevol;
using MV.Data;
using MV.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 계단 섹션(<c>IngameScreen/Mid/Stair</c>, MV-UI3-HUD5, HUD_구조_설계안 3.3 StairSection): 계단 수·칸당 계단·계단 레벨 게이지.
    /// 값은 스냅샷(<see cref="RunHudState"/>)에서 읽고, 등급 그림은 표(<see cref="HudGrade.Frame"/>).
    /// </summary>
    public sealed class StairSection : UIBase
    {
        // 문구: 디자이너 [HUD] 인게임 서식(키 자리 ui.hud.*)
        public const string StairValueFormat = "+{0}"; // ui.hud.stairPer
        public const string StairCountFormat = "x{0}"; // ui.hud.stairCount

        [SerializeField] AddressableImage stairGrade;
        /// <summary>「+{칸당 계단}」.</summary>
        [SerializeField] TMP_Text stairValue;
        /// <summary>「x{계단 수}」.</summary>
        [SerializeField] TMP_Text stairCount;
        [SerializeField] Slider gauge;
        [SerializeField] AddressableImage gaugeGrade;
        [SerializeField] TMP_Text gaugeLevel;
        [SerializeField] AddressableImage gaugeNextGrade;
        [SerializeField] TMP_Text gaugeNextLevel;

        public string StairValueText => stairValue ? stairValue.text : null;
        public string StairCountText => stairCount ? stairCount.text : null;
        public string StairGradeSprite => stairGrade ? stairGrade.spriteName : null;
        public string GaugeGradeSprite => gaugeGrade ? gaugeGrade.spriteName : null;
        public float GaugeValue => gauge ? gauge.normalizedValue : -1;

        /// <summary>계단 수(프레젠터 문자열).</summary>
        public void SetStairs(string text) { if (stairCount) stairCount.text = string.Format(StairCountFormat, text); }

        /// <summary>계단 레벨(칸당 계단)과 게이지. 다음 레벨은 표 상한(330 게이지 행 cap)까지.</summary>
        public void SetGauge(MvTables t, int lv, float fill01)
        {
            var cap = t != null ? RunFormulas.GaugeRow(t) : null;
            int next = cap != null ? Math.Min(cap.cap, lv + 1) : lv + 1;
            if (gauge) gauge.normalizedValue = Mathf.Clamp01(fill01);
            HudImage.Set(stairGrade, HudGrade.Frame(t, lv));
            HudImage.Set(gaugeGrade, HudGrade.Frame(t, lv));
            HudImage.Set(gaugeNextGrade, HudGrade.Frame(t, next));
            if (stairValue) stairValue.text = string.Format(StairValueFormat, lv);
            if (gaugeLevel) gaugeLevel.text = lv.ToString();
            if (gaugeNextLevel) gaugeNextLevel.text = next.ToString();
        }
    }
}
