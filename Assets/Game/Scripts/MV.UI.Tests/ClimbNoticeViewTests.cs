using System.Collections.Generic;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI.Tests
{
    /// <summary>
    /// 계단 정산 글자(사용자 계단 규칙 2026-11-16, 새 HTML SIDE.label·toast): 등급 이름, 「냥파워 카드 확정!」, 런 안내 문구.
    /// </summary>
    public sealed class ClimbNoticeViewTests
    {
        const string PrefabPath = "Assets/Game/Art/UI/Prefab/HUD/HUD_ClimbNotice.prefab";

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
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            var v = go.GetComponent<ClimbNoticeView>();
            Assert.That(v, Is.Not.Null, "프리팹 뿌리의 ClimbNoticeView");
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
        public void Prefab_StartsWithHtmlPanelAndInk_LabelAt40Percent()
        {
            // 처음 값은 새 HTML: #toast background = --panel(v16 :root 덮어씀 rgba(18,15,26,.68)), color = --ink #fbf6ec, y 58%. SIDE.label y = H·0.4.
            // 디자이너가 프리팹에서 바꾸면 이 시험도 함께 바꾼다(모양의 정본은 프리팹).
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            var so = new SerializedObject(prefab.GetComponent<ClimbNoticeView>());
            var bg = (Image)so.FindProperty("toastBg").objectReferenceValue;
            var ink = (TMPro.TMP_Text)so.FindProperty("toast").objectReferenceValue;
            var label = (RectTransform)so.FindProperty("labelRoot").objectReferenceValue;
            var toast = (RectTransform)so.FindProperty("toastRoot").objectReferenceValue;
            var panel = new Color(18 / 255f, 15 / 255f, 26 / 255f, .68f);
            for (int i = 0; i < 4; i++) Assert.That(bg.color[i], Is.EqualTo(panel[i]).Within(1e-4), "패널 색 " + i);
            Assert.That(ColorUtility.ToHtmlStringRGB(ink.color), Is.EqualTo("FBF6EC"));
            Assert.That(label.anchorMin.y, Is.EqualTo(.6f).Within(1e-5), "화면 위에서 40%");
            Assert.That(toast.anchorMin.y, Is.EqualTo(.42f).Within(1e-5), "화면 위에서 58%");
            foreach (var g in prefab.GetComponentsInChildren<Graphic>(true)) Assert.That(g.raycastTarget, Is.False, g.name + ": 누르기를 막지 않음");
        }
    }
}
