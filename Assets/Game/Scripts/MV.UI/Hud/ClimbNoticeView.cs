using System;
using MV.Data;
using MV.Run;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 계단 정산 글자(사용자 계단 규칙 2026-11-16, 새 HTML `drawClimbOverlay` SIDE.label·`toast`).
    /// <list type="bullet">
    /// <item>가운데 큰 글자: 등급이 오르면 그 등급 이름(115 행 이름 끝 낱말, 예 COMMON), 75칸 카드가 확정되면 「냥파워 카드 확정!」(lapTier 색).
    /// 카드 확정 뒤 등급은 다시 센다. 1.3초 동안 0.6 → 1.1배로 커졌다 1.0배로, 끝 0.33초 사라짐. 화면 위에서 36%, 글자 = 화면 너비 × 0.085.</item>
    /// <item>안내(<see cref="Toast"/>): 화면 위에서 58%, 종이 판 위 글자, 1.4초.</item>
    /// </list>
    /// 수치는 새 HTML 상수 그대로(표 키 없음).
    /// </summary>
    public sealed class ClimbNoticeView : MonoBehaviour
    {
        public const float LabelSec = 1.3f, LabelY = .36f, LabelScale = .085f, ToastSec = 1.4f, ToastY = .58f, ToastScale = .04f;
        public const string LapText = "냥파워 카드 확정!"; // ui.hud.climbLapCard
        /// <summary>새 HTML `PAPER`·`INK`(안내 종이 판·글자).</summary>
        static readonly Color Paper = MvUi.Hex("#fff4e2"), Ink = MvUi.Hex("#3a2440");

        Func<RunState> _state;
        RunState _run;
        ClimbState _climb;
        int _tier = -1, _laps;
        float _labelT = -1, _toastT = -1;

        RectTransform _root, _labelRt, _toastRt;
        Text _label, _toast;

        // ---- 시험·QA ----
        public string Label => _labelT >= 0 && _label != null ? _label.text : null;
        public Color LabelColor => _label != null ? _label.color : Color.clear;
        public string ToastText => _toastT >= 0 && _toast != null ? _toast.text : null;

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
            if (string.IsNullOrEmpty(text)) return;
            EnsureBuilt();
            _toast.text = text;
            _toastT = 0;
            _toastRt.gameObject.SetActive(true);
        }

        /// <summary>새 HTML 크기 곡선: k &lt; 0.15면 0.6 + k/0.15·0.5, 아니면 1.1 − min(0.1, k − 0.15).</summary>
        public static float Scale(float k) => k < .15f ? .6f + k / .15f * .5f : 1.1f - Mathf.Min(.1f, k - .15f);

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

        void EnsureBuilt()
        {
            if (_label) return;
            _root = (RectTransform)transform;
            _labelRt = MvUi.Rect(_root, "Label");
            _labelRt.anchorMin = new Vector2(0, 1 - LabelY); _labelRt.anchorMax = new Vector2(1, 1 - LabelY);
            _labelRt.sizeDelta = new Vector2(0, 400);
            _label = MvUi.Label(_labelRt, "Text", 60, Color.white, TextAnchor.MiddleCenter);
            MvUi.Stretch(_label.rectTransform);
            Outline(_label);
            _toastRt = MvUi.Rect(_root, "Toast");
            _toastRt.anchorMin = new Vector2(.5f, 1 - ToastY); _toastRt.anchorMax = new Vector2(.5f, 1 - ToastY);
            _toastRt.gameObject.AddComponent<Image>().color = Paper;
            var fit = _toastRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            fit.padding = new RectOffset(48, 48, 20, 20);
            fit.childControlWidth = fit.childControlHeight = true;
            _toastRt.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            _toastRt.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _toast = MvUi.Label(_toastRt, "Text", 40, Ink, TextAnchor.MiddleCenter);
            _toast.horizontalOverflow = HorizontalWrapMode.Overflow;
            foreach (var g in GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            _labelRt.gameObject.SetActive(false);
            _toastRt.gameObject.SetActive(false);
        }

        static void Outline(Text t)
        {
            var a = t.gameObject.AddComponent<Outline>();
            a.effectColor = Ink;
            a.effectDistance = new Vector2(6, -6);
            var b = t.gameObject.AddComponent<Outline>();
            b.effectColor = Paper;
            b.effectDistance = new Vector2(3, -3);
        }

        void ShowLabel(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            _label.text = text;
            _label.color = color;
            _labelT = 0;
            _labelRt.gameObject.SetActive(true);
        }

        public void Tick(float dt)
        {
            EnsureBuilt();
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

            var size = _root.rect.size;
            if (_labelT >= 0)
            {
                _labelT += dt;
                if (_labelT > LabelSec) { _labelT = -1; _labelRt.gameObject.SetActive(false); }
                else
                {
                    _label.fontSize = Mathf.Max(1, Mathf.RoundToInt(size.x * LabelScale * Scale(_labelT)));
                    var col = _label.color; col.a = Alpha(_labelT); _label.color = col;
                }
            }
            if (_toastT >= 0)
            {
                _toastT += dt;
                if (_toastT > ToastSec) { _toastT = -1; _toastRt.gameObject.SetActive(false); }
                else _toast.fontSize = Mathf.Max(1, Mathf.RoundToInt(size.x * ToastScale));
            }
        }
    }
}
