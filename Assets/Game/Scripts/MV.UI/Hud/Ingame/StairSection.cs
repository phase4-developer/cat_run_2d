using System;
using jjevol;
using jjevol.UI.Widgets;
using MV.Data;
using MV.Run;
using MV.View;
using TMPro;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 계단 섹션(<c>IngameScreen/Mid/Stair</c>, MV-UI3-HUD5, HUD_구조_설계안 3.3 StairSection): 계단 수·칸당 계단·계단 레벨 게이지.
    /// 값은 스냅샷(<see cref="RunHudState"/>)에서 읽고, 등급 그림은 표(<see cref="HudGrade.Frame"/>).
    /// 게이지 연출(MV-UI3-STAIRFX3·4): 탑에서 나온 계단 경험치 오브(<see cref="StairOrbFlight"/>)가 도착하는 순간 시뮬이 실제 게이지를 그 몫만큼 올리고,
    /// 보이는 막대는 Framework <see cref="AnimatedGauge"/>(디자이너 Slider 막대, 시간은 그 부품의 <c>GaugeTiming</c>)가 따라 오른다.
    /// 레벨 넘김은 바퀴(laps)로 주고, 바퀴가 끝날 때(<see cref="AnimatedGauge.OnLapCompleted"/>) 보이는 레벨을 올리고 계단 칸 등급 업 연출(<see cref="HudGradeUpFx"/>)을 켠다.
    /// 값은 시뮬만 갖고, 여기에는 보여 주는 레벨만 있다.
    /// </summary>
    public sealed class StairSection : UIBase
    {
        // 문구: 디자이너 [HUD] 인게임 서식(키 자리 ui.hud.*)
        public const string StairValueFormat = "+{0}"; // ui.hud.stairPer
        public const string StairCountFormat = "x{0}"; // ui.hud.stairCount
        const string GaugePath = "IngameScreen/Mid/Stair/Progress_StairLevel/Progress_Level (AnimatedGauge)";

        [SerializeField] AddressableImage stairGrade;
        /// <summary>「+{칸당 계단}」.</summary>
        [SerializeField] TMP_Text stairValue;
        /// <summary>「x{계단 수}」.</summary>
        [SerializeField] TMP_Text stairCount;
        [Tooltip("계단 레벨 게이지(디자이너 Slider 막대에 붙인 Framework AnimatedGauge, 따라 오르는 시간은 그 부품의 Timing)")]
        [SerializeField] AnimatedGauge gaugeBar;
        [SerializeField] AddressableImage gaugeGrade;
        [SerializeField] TMP_Text gaugeLevel;
        [SerializeField] AddressableImage gaugeNextGrade;
        [SerializeField] TMP_Text gaugeNextLevel;

        [Header("연출(MV-UI3-STAIRFX)")]
        [Tooltip("계단 경험치 오브가 도착하는 자리(디자이너 Mid/Stair/OrbPos Anchor)")]
        [SerializeField] RectTransform orbTarget;
        [Tooltip("레벨이 오를 때 계단 칸(Slot_Stair) 크기·Flash·Sparkle 연출")]
        [SerializeField] HudGradeUpFx gradeUpFx;

        public string StairValueText => stairValue ? stairValue.text : null;
        public string StairCountText => stairCount ? stairCount.text : null;
        public string StairGradeSprite => stairGrade ? stairGrade.spriteName : null;
        public string GaugeGradeSprite => gaugeGrade ? gaugeGrade.spriteName : null;
        /// <summary>보이는 막대 채움(0~1). 게이지가 없으면 −1.</summary>
        public float GaugeValue => gaugeBar ? gaugeBar.Model.Fill : -1;
        public AnimatedGauge Gauge => gaugeBar;
        public HudGradeUpFx GradeUpFx => gradeUpFx;
        public RectTransform OrbTarget => orbTarget;
        /// <summary>지금 보여 주는 레벨(시뮬보다 늦을 수 있다). 처음 그리기 전 −1.</summary>
        public int ShownLevel => _shownLv;

        int _simLv = -1, _shownLv = -1;
        float _simFill;
        bool _hooked, _warned;
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

        /// <summary>새 런: 보여 주는 진행을 비우고 다음 그리기에서 시뮬 값으로 바로 맞춘다.</summary>
        public void ResetRun()
        {
            _simLv = _shownLv = -1;
            _simFill = 0;
            if (gradeUpFx) gradeUpFx.ResetLook();
        }

        /// <summary>계단 레벨(칸당 계단)과 게이지(시뮬 값). 다음 레벨은 표 상한(330 게이지 행 cap)까지.
        /// HUD 값이 바뀐 프레임에만 불린다. 첫 그리기·새 런·되감김은 연출 없이 바로(<see cref="AnimatedGauge.SetInstant"/>),
        /// 오르면 레벨 차이를 바퀴로 주어 따라 오른다(<see cref="AnimatedGauge.SetValue"/>).</summary>
        public void SetGauge(MvTables t, int lv, float fill01)
        {
            _t = t;
            fill01 = Mathf.Clamp01(fill01);
            Hook();
            bool instant = _shownLv < 0 || lv < _simLv || (lv == _simLv && fill01 < _simFill);
            if (instant)
            {
                _simLv = _shownLv = lv;
                _simFill = fill01;
                if (gaugeBar) gaugeBar.SetInstant((decimal)fill01, 1m);
                Draw();
                return;
            }
            if (lv == _simLv && fill01 == _simFill) return;
            // 바퀴 = 시뮬 레벨 − 보이는 레벨. 바퀴 도중 새 값이 와도 AnimatedGauge가 바퀴를 다시 짜므로 남은 바퀴를 모두 준다.
            int laps = lv - _shownLv;
            _simLv = lv;
            _simFill = fill01;
            // 바퀴가 끝날 때마다 OnLapCompleted(바퀴 시간이 0이면 이 호출 안에서).
            if (gaugeBar) gaugeBar.SetValue((decimal)fill01, 1m, laps);
            else { _shownLv = lv; Draw(); }
        }

        /// <summary>매 프레임(<see cref="IngameHudView.TickRun"/>). 막대 움직임은 <see cref="AnimatedGauge"/>가 스스로 돈다.
        /// 게이지가 꺼지며 남은 바퀴를 건너뛰었으면(Mid 숨김 등, AnimatedGauge는 꺼질 때 끝 상태로 정리한다) 보이는 레벨을 연출 없이 시뮬에 맞춘다.</summary>
        public void Tick(float dt)
        {
            if (_shownLv < 0 || _shownLv == _simLv) return;
            if (gaugeBar && gaugeBar.Model.IsAnimating) return;
            _shownLv = _simLv;
            Draw();
        }

        void Hook()
        {
            if (gaugeBar)
            {
                if (_hooked) return;
                _hooked = true;
                gaugeBar.OnLapCompleted.AddListener(OnLap);
            }
            else if (!_warned)
            {
                _warned = true;
                Debug.LogWarning($"[StairSection] 계단 게이지(AnimatedGauge) 연결 없음: {GaugePath}", this);
            }
        }

        void OnDestroy()
        {
            if (_hooked && gaugeBar) gaugeBar.OnLapCompleted.RemoveListener(OnLap);
        }

        // 바퀴 하나 = 보이는 레벨 하나. 등급 업 연출 시작 경로는 여기 하나.
        void OnLap()
        {
            if (_shownLv < 0 || _shownLv >= _simLv) return;
            _shownLv++;
            Draw();
            if (gradeUpFx) gradeUpFx.Play();
        }

        void Draw()
        {
            var t = _t;
            int lv = _shownLv;
            var cap = t != null ? RunFormulas.GaugeRow(t) : null;
            int next = cap != null ? Math.Min(cap.cap, lv + 1) : lv + 1;
            HudImage.Set(stairGrade, HudGrade.Frame(t, lv));
            HudImage.Set(gaugeGrade, HudGrade.Frame(t, lv));
            HudImage.Set(gaugeNextGrade, HudGrade.Frame(t, next));
            if (stairValue) stairValue.text = string.Format(StairValueFormat, lv);
            if (gaugeLevel) gaugeLevel.text = lv.ToString();
            if (gaugeNextLevel) gaugeNextLevel.text = next.ToString();
        }
    }
}
