using System;
using MV.Data;
using MV.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 계단 정산 글자(사용자 계단 규칙 2026-11-16, 새 HTML `drawClimbOverlay` SIDE.label·`toast`).
    /// <list type="bullet">
    /// <item>가운데 큰 글자: 등급이 오르면 그 등급 이름(115 행 이름 끝 낱말, 예 COMMON), 75칸 카드가 확정되면 「냥파워 카드 확정!」(lapTier 색).
    /// 카드 확정 뒤 등급은 다시 센다. 1.3초 동안 0.6 → 1.1배로 커졌다 1.0배로, 끝 0.33초 사라짐. 글자 = 화면 너비 × 0.085.</item>
    /// <item>안내(<see cref="Toast"/>): 패널 위 글자. 300 `toast.time` 동안 보이고 `toast.fade`로 나타나고 사라진다(CSS opacity 전환).</item>
    /// </list>
    /// 모양(위치·패널·글자 색·글꼴·외곽선)은 프리팹 <c>HUD_ClimbNotice</c>가 가진다(디자이너가 고친다). 이 컴포넌트는 직렬화 참조만 쓰고 UI를 만들지 않는다.
    /// 처음 값은 새 HTML(SIDE.label y = H·0.4, #toast y 58%·`--panel` rgba(18,15,26,.68)·`--ink` #fbf6ec). 크기 곡선·시간은 새 HTML 상수, 안내 시간은 300 표.
    /// </summary>
    public sealed class ClimbNoticeView : MonoBehaviour
    {
        public const float LabelSec = 1.3f, LabelScale = .085f, ToastScale = .04f;
        public const string LapText = "냥파워 카드 확정!"; // ui.hud.climbLapCard

        [SerializeField] RectTransform labelRoot;
        [SerializeField] TMP_Text label;
        [SerializeField] RectTransform toastRoot;
        [SerializeField] Image toastBg;
        [SerializeField] TMP_Text toast;

        Func<RunState> _state;
        RunState _run;
        ClimbState _climb;
        int _tier = -1, _laps;
        float _labelT = -1, _toastT = -1;

        Color _panel, _ink;
        bool _ready;
        float _toastTime, _toastFade;

        // ---- 시험·QA ----
        public string Label => _labelT >= 0 && label != null ? label.text : null;
        public Color LabelColor => label != null ? label.color : Color.clear;
        public string ToastText => _toastT >= 0 && toast != null ? toast.text : null;
        /// <summary>안내 불투명도(0~1, 페이드 포함).</summary>
        public float ToastAlpha { get; private set; }

        public void Bind(Func<RunState> state)
        {
            _state = state;
            _run = null;
            _climb = null;
            _labelT = _toastT = -1;
        }

        /// <summary>안내 한 줄(모은 마나 없음·5 미만 등). <see cref="MvUiToast"/> 처리기로 걸린다.</summary>
        public void Toast(string text)
        {
            if (string.IsNullOrEmpty(text) || !Ready()) return;
            toast.text = text;
            _toastT = 0;
            var t = _state?.Invoke()?.Tables;
            _toastTime = t != null ? (float)t.RunConfig.D(RunConfigKeys.Toast_Time) : 0;
            _toastFade = t != null ? (float)t.RunConfig.D(RunConfigKeys.Toast_Fade) : 0;
            SetToastAlpha(ToastFadeAlpha(0, _toastTime, _toastFade));
            toastRoot.gameObject.SetActive(true);
        }

        /// <summary>새 HTML 크기 곡선: k &lt; 0.15면 0.6 + k/0.15·0.5, 아니면 1.1 − min(0.1, k − 0.15).</summary>
        public static float Scale(float k) => k < .15f ? .6f + k / .15f * .5f : 1.1f - Mathf.Min(.1f, k - .15f);

        /// <summary>
        /// 안내 불투명도(새 HTML: `on`이 붙으면 fade초에 0→1, toastTimer가 끝나면 fade초에 1→0). fade ≤ 0이면 바로 바뀐다.
        /// </summary>
        public static float ToastFadeAlpha(float k, float time, float fade)
        {
            if (k < 0) return 0;
            float a = fade > 0 ? Mathf.Clamp01(k / fade) : 1;
            if (k > time) a = Mathf.Min(a, fade > 0 ? Mathf.Clamp01(1 - (k - time) / fade) : 0);
            return a;
        }

        void SetToastAlpha(float a)
        {
            ToastAlpha = a;
            var c = _panel; c.a *= a; toastBg.color = c;
            var i = _ink; i.a *= a; toast.color = i;
        }

        /// <summary>새 HTML 알파: min(1, (1.3 − k)·3).</summary>
        public static float Alpha(float k) => Mathf.Clamp01((LabelSec - k) * 3f);

        /// <summary>표 115 행 이름의 끝 낱말을 대문자로(「… 등급 Common」 → COMMON, 새 HTML TIERS.n).</summary>
        public static string TierName(MvTables t, int tier)
        {
            if (t == null) return null;
            foreach (var r in t.CardTier.Datas)
            {
                if (r.tierIndex != tier) continue;
                string n = MvUiText.Name(r.id, MvUiText.RawName(t.CardTier.RawRows, r.id) ?? "").Trim();
                if (n.Length == 0) return null;
                int sp = n.LastIndexOf(' ');
                return (sp >= 0 ? n.Substring(sp + 1) : n).ToUpperInvariant();
            }
            return null;
        }

        static Color TierColor(MvTables t, int tier)
        {
            if (t != null)
                foreach (var r in t.CardTier.Datas)
                    if (r.tierIndex == tier && ColorUtility.TryParseHtmlString(r.color, out var c)) return c;
            return Color.white;
        }

        /// <summary>프리팹 참조가 모두 있으면 처음 색을 기억하고 글자를 숨긴다. 빠졌으면 경고로 드러낸다(코드로 대신 만들지 않는다).</summary>
        bool Ready()
        {
            if (_ready) return true;
            if (!labelRoot || !label || !toastRoot || !toastBg || !toast)
            {
                Debug.LogWarning("[ClimbNoticeView] 프리팹 참조가 비었습니다(HUD_ClimbNotice).", this);
                enabled = false;
                return false;
            }
            _panel = toastBg.color;
            _ink = toast.color;
            labelRoot.gameObject.SetActive(false);
            toastRoot.gameObject.SetActive(false);
            return _ready = true;
        }

        void ShowLabel(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            label.text = text;
            label.color = color;
            _labelT = 0;
            labelRoot.gameObject.SetActive(true);
        }

        public void Tick(float dt)
        {
            if (!Ready()) return;
            var s = _state?.Invoke();
            if (s != _run) { _run = s; _climb = null; _labelT = -1; }
            var c = s != null && s.Phase == RunPhase.Climb ? s.Climb : null;
            if (c != null && !ReferenceEquals(c, _climb)) { _climb = c; _tier = -1; _laps = 0; }
            if (c != null)
            {
                // 새 HTML: 등급이 오르면 등급 이름, 카드가 늘면 「냥파워 카드 확정!」 뒤 등급을 다시 센다.
                if (c.Tier > _tier) { _tier = c.Tier; if (_tier >= 0) ShowLabel(TierName(s.Tables, _tier), TierColor(s.Tables, _tier)); }
                if (c.Cards.Count > _laps)
                {
                    _laps = c.Cards.Count;
                    int lapTier = s.Tables != null ? (int)Math.Round(s.Tables.Climb.Config()["lapTier"]) : -1;
                    ShowLabel(LapText, TierColor(s.Tables, lapTier));
                    _tier = -1;
                }
            }

            var size = ((RectTransform)transform).rect.size;
            if (_labelT >= 0)
            {
                _labelT += dt;
                if (_labelT > LabelSec) { _labelT = -1; labelRoot.gameObject.SetActive(false); }
                else
                {
                    label.fontSize = Mathf.Max(1, Mathf.RoundToInt(size.x * LabelScale * Scale(_labelT)));
                    var col = label.color; col.a = Alpha(_labelT); label.color = col;
                }
            }
            if (_toastT >= 0)
            {
                _toastT += dt;
                if (_toastT > _toastTime + _toastFade) { _toastT = -1; ToastAlpha = 0; toastRoot.gameObject.SetActive(false); }
                else
                {
                    toast.fontSize = Mathf.Max(1, Mathf.RoundToInt(size.x * ToastScale));
                    SetToastAlpha(ToastFadeAlpha(_toastT, _toastTime, _toastFade));
                }
            }
        }
    }
}
