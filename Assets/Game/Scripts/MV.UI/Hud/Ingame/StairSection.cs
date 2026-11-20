using System;
using jjevol;
using MV.Data;
using MV.Run;
using MV.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 계단 섹션(<c>IngameScreen/Mid/Stair</c>, MV-UI3-HUD5, HUD_구조_설계안 3.3 StairSection): 계단 수·칸당 계단·계단 레벨 게이지.
    /// 값은 스냅샷(<see cref="RunHudState"/>)에서 읽고, 등급 그림은 표(<see cref="HudGrade.Frame"/>).
    /// 게이지 연출(MV-UI3-STAIRFX3): 탑에서 나온 계단 경험치 오브(<see cref="StairOrbFlight"/>)가 도착하는 순간 시뮬이 실제 게이지를 그 몫만큼 올리고,
    /// 여기서는 시뮬 값까지 짧게(<see cref="gaugeStepSec"/>) 따라 오른다. 오브 도착 자리(OrbPos Anchor)와 오브를 놓을 UI 판을 입구에 꽂는다.
    /// 값은 시뮬만 갖고, 여기에는 보여 주는 진행(레벨 + 채움)만 있다. 레벨이 오르면 계단 칸 등급 업 연출(<see cref="HudGradeUpFx"/>).
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

        [Header("연출(MV-UI3-STAIRFX)")]
        [Tooltip("계단 경험치 오브가 도착하는 자리(디자이너 Mid/Stair/OrbPos Anchor)")]
        [SerializeField] RectTransform orbTarget;
        [Tooltip("레벨이 오를 때 계단 칸(Slot_Stair) 크기·Flash·Sparkle 연출")]
        [SerializeField] HudGradeUpFx gradeUpFx;
        [Tooltip("시뮬 게이지 값이 오를 때(오브 하나 도착) 보이는 게이지가 따라 오르는 초(임시)")]
        [SerializeField] float gaugeStepSec = 0.15f;

        public string StairValueText => stairValue ? stairValue.text : null;
        public string StairCountText => stairCount ? stairCount.text : null;
        public string StairGradeSprite => stairGrade ? stairGrade.spriteName : null;
        public string GaugeGradeSprite => gaugeGrade ? gaugeGrade.spriteName : null;
        public float GaugeValue => gauge ? gauge.normalizedValue : -1;
        public HudGradeUpFx GradeUpFx => gradeUpFx;
        public RectTransform OrbTarget => orbTarget;
        /// <summary>지금 보여 주는 레벨(시뮬보다 늦을 수 있다). 처음 그리기 전 −1.</summary>
        public int ShownLevel => _drawnLv;

        // 보여 주는 진행 = 레벨 + 채움(0~1). 시뮬 진행까지 따라간다.
        double _sim = -1, _shown = -1;
        double _stepSpeed;
        int _drawnLv = -1;
        MvTables _t;

        /// <summary>계단 수(프레젠터 문자열).</summary>
        public void SetStairs(string text) { if (stairCount) stairCount.text = string.Format(StairCountFormat, text); }

        protected override bool SetSubscribe(bool subscribe)
        {
            if (!base.SetSubscribe(subscribe)) return false;
            if (subscribe)
            {
                StairOrbFlight.Target = TryOrbTarget;
                StairOrbFlight.UiRoot = OrbUiRoot;
            }
            else
            {
                if (StairOrbFlight.Target == (StairOrbFlight.TargetFn)TryOrbTarget) StairOrbFlight.Target = null;
                if (StairOrbFlight.UiRoot == (Func<Transform>)OrbUiRoot) StairOrbFlight.UiRoot = null;
            }
            return true;
        }

        /// <summary>오브 도착 자리(화면 px). Mid가 꺼져 있어도 마지막 배치 자리를 쓴다.</summary>
        public bool TryOrbTarget(out Vector2 screen)
        {
            screen = default;
            var rt = orbTarget ? orbTarget : (gradeUpFx ? gradeUpFx.transform as RectTransform : null);
            if (!rt) return false;
            var canvas = rt.GetComponentInParent<Canvas>(true);
            var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            screen = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            return true;
        }

        /// <summary>
        /// 오브를 놓을 UI 판(MV-UI3-STAIRFX2): Framework <c>UIManager.PlayScreenEffect</c>와 같은 순서로 지금 화면의 Effect 루트, 없으면 UIManager Effect 루트.
        /// UI 카메라가 그리므로 오브가 HUD 앞에 보인다. UIManager가 없으면 null(오브 없이 게이지만 오른다).
        /// </summary>
        public static Transform OrbUiRoot()
        {
            if (!UIManager.HasInstance) return null;
            var ui = UIManager.Instance;
            var screen = ui.screenManager != null ? ui.screenManager.CurrentScreen : null;
            var root = screen != null ? screen.EffectRoot : null;
            return root != null ? root : ui.EffectRoot;
        }

        /// <summary>새 런: 연출 진행을 비우고 다음 그리기에서 시뮬 값으로 바로 맞춘다.</summary>
        public void ResetRun()
        {
            _sim = _shown = -1;
            _drawnLv = -1;
            if (gradeUpFx) gradeUpFx.ResetLook();
        }

        /// <summary>계단 레벨(칸당 계단)과 게이지(시뮬 값). 다음 레벨은 표 상한(330 게이지 행 cap)까지.
        /// HUD 값이 바뀐 프레임에만 불린다. 따라 오르는 진행은 <see cref="Tick"/>이 매 프레임 한다(MV-UI3-STAIRFX2).</summary>
        public void SetGauge(MvTables t, int lv, float fill01)
        {
            _t = t;
            double sim = lv + Mathf.Clamp01(fill01) * 0.999999;
            if (_shown < 0 || sim < _shown || gaugeStepSec <= 0) _shown = sim; // 첫 그리기·새 런·되감김은 바로
            else if (sim > _sim) _stepSpeed = (sim - _shown) / gaugeStepSec;
            _sim = sim;
            Draw();
        }

        /// <summary>매 프레임(<see cref="IngameHudView.TickRun"/>): 보이는 진행을 시뮬 값까지 따라 올린다.</summary>
        public void Tick(float dt)
        {
            if (_sim < 0 || _shown >= _sim) return;
            _shown = Math.Min(_sim, _shown + Math.Max(_stepSpeed, 1e-6) * Math.Max(0, dt));
            Draw();
        }

        void Draw()
        {
            var t = _t;
            int lv = (int)Math.Floor(_shown);
            float fill01 = (float)(_shown - lv);
            var cap = t != null ? RunFormulas.GaugeRow(t) : null;
            int next = cap != null ? Math.Min(cap.cap, lv + 1) : lv + 1;
            if (gauge) gauge.normalizedValue = Mathf.Clamp01(fill01);
            HudImage.Set(stairGrade, HudGrade.Frame(t, lv));
            HudImage.Set(gaugeGrade, HudGrade.Frame(t, lv));
            HudImage.Set(gaugeNextGrade, HudGrade.Frame(t, next));
            if (stairValue) stairValue.text = string.Format(StairValueFormat, lv);
            if (gaugeLevel) gaugeLevel.text = lv.ToString();
            if (gaugeNextLevel) gaugeNextLevel.text = next.ToString();
            // 등급 업 연출 시작 경로는 여기 하나(보이는 레벨이 오를 때). 첫 그리기·새 런은 연출 없이 맞춘다.
            if (_drawnLv >= 0 && lv > _drawnLv && gradeUpFx) gradeUpFx.Play();
            _drawnLv = lv;
        }
    }
}
