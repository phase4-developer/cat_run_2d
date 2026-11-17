using System.Collections.Generic;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEngine;

namespace MV.UI.Tests
{
    /// <summary>
    /// 계단 정산 글자(사용자 계단 규칙 2026-11-16, 새 HTML SIDE.label·toast): 등급 이름, 「냥파워 카드 확정!」, 런 안내 문구.
    /// </summary>
    public sealed class ClimbNoticeViewTests
    {
        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        static MvTables T => MetaUiTestKit.Base();

        ClimbNoticeView NewView(System.Func<RunState> state)
        {
            var go = new GameObject("ClimbNotice", typeof(RectTransform));
            _made.Add(go);
            var v = go.AddComponent<ClimbNoticeView>();
            v.Bind(state);
            return v;
        }

        static string Hex(int tier)
        {
            foreach (var r in T.CardTier.Datas) if (r.tierIndex == tier) return r.color;
            return null;
        }

        [Test]
        public void TierUp_ShowsTierName_LapCard_ShowsCardText_ThenFades()
        {
            var s = new RunState(null, T, new Pcg32Rng(1)) { Phase = RunPhase.Climb, Climb = new ClimbState { N = 100, K = 100, Pw = 1, T = 10 } };
            var v = NewView(() => s);
            v.Tick(0);
            Assert.That(v.Label, Is.Null, "등급 전에는 글자 없음");

            s.Climb.Tier = 2;
            v.Tick(0.01f);
            Assert.That(v.Label, Is.EqualTo("RARE"), "115 행 이름 끝 낱말(새 HTML TIERS.n)");
            Assert.That(ColorUtility.ToHtmlStringRGB(v.LabelColor), Is.EqualTo(Hex(2).TrimStart('#').ToUpperInvariant()));

            s.Climb.Cards.Add(4);
            v.Tick(0.01f);
            Assert.That(v.Label, Is.EqualTo(ClimbNoticeView.LapText));
            int lapTier = (int)System.Math.Round(T.Climb.Config()["lapTier"]);
            Assert.That(ColorUtility.ToHtmlStringRGB(v.LabelColor), Is.EqualTo(Hex(lapTier).TrimStart('#').ToUpperInvariant()));

            s.Climb.Tier = 0;   // 카드 확정 뒤 등급을 다시 센다
            v.Tick(0.01f);
            Assert.That(v.Label, Is.EqualTo("COMMON"));
            v.Tick(ClimbNoticeView.LabelSec + 0.01f);
            Assert.That(v.Label, Is.Null, "1.3초 뒤 사라짐");
        }

        [Test]
        public void LabelCurve_MatchesHtml()
        {
            Assert.That(ClimbNoticeView.Scale(0), Is.EqualTo(0.6f).Within(1e-5));
            Assert.That(ClimbNoticeView.Scale(0.15f), Is.EqualTo(1.1f).Within(1e-5));
            Assert.That(ClimbNoticeView.Scale(1f), Is.EqualTo(1.0f).Within(1e-5));
            Assert.That(ClimbNoticeView.Alpha(0.5f), Is.EqualTo(1f));
            Assert.That(ClimbNoticeView.Alpha(1.3f), Is.EqualTo(0f).Within(1e-5));
        }

        [Test]
        public void Toast_ShowsRunToastText_ForClimbKeys()
        {
            Assert.That(RunToastText.Of(new RunEvent(RunEventType.Toast, key: RunToastText.NoStairs)), Is.EqualTo("모은 마나가 없습니다"));
            Assert.That(RunToastText.Of(new RunEvent(RunEventType.Toast, key: RunToastText.NoCard, a: 3)), Is.EqualTo("포탈 3 · 5 미만이라 냥파워 없음"));
            var s = new RunState(null, T, new Pcg32Rng(1));
            float time = (float)T.RunConfig.D(RunConfigKeys.Toast_Time), fade = (float)T.RunConfig.D(RunConfigKeys.Toast_Fade);
            var v = NewView(() => s);
            v.Toast("모은 마나가 없습니다");
            Assert.That(v.ToastAlpha, Is.EqualTo(0f).Within(1e-5), "나타날 때 투명에서 시작(CSS opacity 전환)");
            v.Tick(fade / 2);
            Assert.That(v.ToastAlpha, Is.EqualTo(0.5f).Within(1e-4));
            v.Tick(time - fade / 2);
            Assert.That(v.ToastText, Is.EqualTo("모은 마나가 없습니다"));
            Assert.That(v.ToastAlpha, Is.EqualTo(1f).Within(1e-4), "300 toast.time 동안 보임");
            v.Tick(fade / 2);
            Assert.That(v.ToastAlpha, Is.EqualTo(0.5f).Within(1e-4), "시간이 끝나면 toast.fade 동안 사라짐");
            v.Tick(fade);
            Assert.That(v.ToastText, Is.Null);
        }

        [Test]
        public void Toast_UsesHtmlPanelAndInk_LabelAt40Percent()
        {
            // 새 HTML #toast background = --panel(v16 :root 덮어씀 rgba(18,15,26,.68)), color = --ink #fbf6ec. SIDE.label y = H·0.4.
            Assert.That(ClimbNoticeView.ToastPanel, Is.EqualTo(new Color(18 / 255f, 15 / 255f, 26 / 255f, .68f)));
            Assert.That(ColorUtility.ToHtmlStringRGB(ClimbNoticeView.ToastInk), Is.EqualTo("FBF6EC"));
            Assert.That(ClimbNoticeView.LabelY, Is.EqualTo(.4f));
        }
    }
}
