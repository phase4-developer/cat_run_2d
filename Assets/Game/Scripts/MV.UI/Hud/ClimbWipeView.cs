using System;
using MV.Data;
using MV.Run;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 마나 정산(계단) 화면 전환: 별 무늬 종이 막이 쓱 지나간다(새 HTML `drawWipes`·`paperPanel`, 2026-11-13 연출 정본).
    /// 옛 우다다 덮개(MvWarpOverlay)를 대신한다. 막 아래로 옆 시점 계단·고양이가 그대로 보인다.
    /// <list type="bullet">
    /// <item>오르기 시작: 0.25초 동안 오른쪽에서 들어와 화면을 덮고 사라진다(SIDE_IN).</item>
    /// <item>다 오름(Climb.EndAt): `330.sideOut` 동안 오른쪽에서 다시 덮는다.</item>
    /// <item>정산 끝(EndClimb): 0.42초 동안 왼쪽으로 빠진다(warpOutAt).</item>
    /// </list>
    /// HUD 아래(RunHud 첫 자식)에 그린다. 정본 캔버스 px는 ×3(D-31).
    /// </summary>
    public sealed class ClimbWipeView : MonoBehaviour
    {
        const float Px = 3f;
        /// <summary>정본 `SIDE_IN` 0.25 s, 끝난 뒤 빠짐 420 ms(표 키 없음, 정본 상수).</summary>
        public const float SideIn = .25f, OutSec = .42f;
        /// <summary>막 바깥 여유 20 px(정본 `W + 20`).</summary>
        const float Margin = 20 * Px;
        /// <summary>글자 크기 = 화면 너비 × 0.075, 높이 = 화면 50%.</summary>
        const float TextScale = .075f;

        Func<RunState> _state;
        RunState _run;
        bool _wasClimb;
        float _outT = -1;

        RectTransform _root, _clip, _sparksRt;
        PaperPanelGraphic _panel;
        PaperSparkGraphic _sparks;
        Text _text;

        /// <summary>런 상태 출처. null이면 숨긴다.</summary>
        public void Bind(Func<RunState> state)
        {
            _state = state;
            _run = null;
            _wasClimb = false;
            _outT = -1;
        }

        // ---- 시험·QA ----
        public bool Shown => _panel != null && _panel.gameObject.activeSelf;
        public float X0 => _panel != null ? _panel.X0 : 0;
        public float X1 => _panel != null ? _panel.X1 : 0;
        public string Caption => _text != null ? _text.text : null;

        /// <summary>smoothstep(정본 `ease = k*k*(3 − 2k)`).</summary>
        public static float Ease(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3 - 2 * k);
        }

        /// <summary>
        /// 지금 덮는 구간 [x0, x1](화면 왼쪽 0, 너비 w). 정본 drawWipes 순서대로 뒤 막이 앞 막을 덮어 쓴다.
        /// climbT = 오른 시간(Climb.Time), endAt = 다 오른 시각(없으면 음수), outT = 끝난 뒤 경과(없으면 음수).
        /// </summary>
        public static bool Span(float w, double climbT, double endAt, double sideOut, float outT, out float x0, out float x1)
        {
            bool any = false;
            x0 = x1 = 0;
            if (climbT >= 0)
            {
                if (climbT < SideIn) { x0 = w * (1 - Ease((float)(climbT / SideIn))); x1 = w + Margin; any = true; }
                // 정본 두 번째 분기(t < SIDE_LEAD + 0.2)는 SIDE_LEAD + 0.2 = SIDE_IN이라 그려지지 않는다.
                if (endAt >= 0)
                {
                    float k = Ease(sideOut > 0 ? (float)((climbT - endAt) / sideOut) : 1);
                    x0 = w * (1 - k); x1 = w + Margin; any = true;
                }
            }
            if (outT >= 0 && outT < OutSec)
            {
                float k = Ease(outT / OutSec);
                x0 = -w * k - Margin; x1 = w - w * k; any = true;
            }
            return any;
        }

        void EnsureBuilt()
        {
            if (_panel) return;
            _root = (RectTransform)transform;
            var panelGo = MvUi.Stretch(MvUi.Rect(_root, "Panel")).gameObject;
            panelGo.AddComponent<CanvasRenderer>();
            _panel = panelGo.AddComponent<PaperPanelGraphic>();
            _panel.raycastTarget = false;
            _clip = MvUi.Rect(_root, "Clip");
            _clip.anchorMin = Vector2.zero; _clip.anchorMax = new Vector2(0, 1); _clip.pivot = new Vector2(0, .5f);
            _clip.offsetMin = Vector2.zero; _clip.offsetMax = Vector2.zero;
            _clip.gameObject.AddComponent<RectMask2D>();
            _sparksRt = MvUi.Rect(_clip, "Sparks");
            _sparksRt.anchorMin = Vector2.zero; _sparksRt.anchorMax = new Vector2(0, 1); _sparksRt.pivot = new Vector2(0, .5f);
            _sparksRt.gameObject.AddComponent<CanvasRenderer>();
            _sparks = _sparksRt.gameObject.AddComponent<PaperSparkGraphic>();
            _sparks.raycastTarget = false;
            _text = MvUi.Label(MvUi.Stretch(MvUi.Rect(_clip, "Caption")), "Text", 40, PaperPanelGraphic.Title, TextAnchor.MiddleCenter);
            MvUi.Stretch(_text.rectTransform);
            _text.text = "마나 정산"; // ui.hud.climbWipe
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.raycastTarget = false;
            Hide();
        }

        void Hide()
        {
            _panel.gameObject.SetActive(false);
            _clip.gameObject.SetActive(false);
        }

        public void Tick(float dt)
        {
            EnsureBuilt();
            var s = _state?.Invoke();
            if (s != _run) { _run = s; _wasClimb = false; _outT = -1; }
            if (s == null) { Hide(); return; }
            var c = s.Climb;
            bool climb = s.Phase == RunPhase.Climb && c != null;
            if (_wasClimb && !climb) _outT = 0;   // 정본 endClimb: warpOutAt = now
            else if (_outT >= 0) _outT += dt;
            _wasClimb = climb;
            if (_outT >= OutSec) _outT = -1;

            var size = _root.rect.size;
            double sideOut = s.Tables != null ? s.Tables.Climb.Config()["sideOut"] : 0;
            if (!Span(size.x, climb ? c.Time : -1, climb ? c.EndAt : -1, sideOut, _outT, out float x0, out float x1)
                || x1 <= 0 || x0 >= size.x)
            {
                Hide();
                return;
            }
            _panel.gameObject.SetActive(true);
            _clip.gameObject.SetActive(true);
            _panel.SetSpan(x0, x1);
            float cx0 = Mathf.Max(0, x0), cx1 = Mathf.Min(size.x, x1);
            _clip.anchoredPosition = new Vector2(cx0, 0);
            _clip.sizeDelta = new Vector2(cx1 - cx0, 0);
            _sparksRt.anchoredPosition = new Vector2(-cx0, 0);
            _sparksRt.sizeDelta = new Vector2(size.x, 0);
            _text.fontSize = Mathf.Max(1, Mathf.RoundToInt(size.x * TextScale));
        }
    }
}
