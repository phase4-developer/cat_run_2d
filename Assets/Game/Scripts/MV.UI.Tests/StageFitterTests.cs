using NUnit.Framework;
using UnityEngine;

namespace MV.UI.Tests
{
    /// <summary>StageFitter 계산(05 §5.1.2, D-30 권장안): 9:16·9:19.5·3:4와 노치.</summary>
    public sealed class StageFitterTests
    {
        const float Eps = 0.01f;

        /// <summary>표 300 view.refWidthPx / refHeightPx(360/640 = 9:16).</summary>
        static float Aspect => StageFitter.TableAspect(MetaUiTestKit.Base());

        static StageFitter.Layout Full(float w, float h)
        {
            var screen = new Vector2(w, h);
            return StageFitter.Compute(screen, new Rect(0, 0, w, h), StageFitter.ExpandCanvasSize(screen, Aspect), Aspect);
        }

        static void AreClose(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, "x");
            Assert.AreEqual(expected.y, actual.y, Eps, "y");
        }

        [Test]
        public void Aspect9x16_StageIsReference()
        {
            var l = Full(1080, 1920);
            AreClose(Vector2.zero, l.Position);
            AreClose(new Vector2(1080, 1920), l.Size);
            Assert.AreEqual(1f, l.Scale, Eps);
        }

        [Test]
        public void Aspect9x16_HigherResolution_SameCanvasUnits()
        {
            var l = Full(1440, 2560);
            AreClose(Vector2.zero, l.Position);
            AreClose(new Vector2(1080, 1920), l.Size);
            Assert.AreEqual(1f, l.Scale, Eps);
        }

        [Test]
        public void Aspect9x19_5_WidthStays1080_HeightGrows()
        {
            var canvas = StageFitter.ExpandCanvasSize(new Vector2(1080, 2340), Aspect);
            AreClose(new Vector2(1080, 2340), canvas);
            var l = Full(1080, 2340);
            AreClose(Vector2.zero, l.Position);
            AreClose(new Vector2(1080, 2340), l.Size);
            Assert.AreEqual(1f, l.Scale, Eps);
        }

        [Test]
        public void Aspect3x4_HeightStays1920_CenteredHorizontally()
        {
            var canvas = StageFitter.ExpandCanvasSize(new Vector2(1536, 2048), Aspect);
            AreClose(new Vector2(1440, 1920), canvas);
            var l = Full(1536, 2048);
            AreClose(new Vector2(180, 0), l.Position);
            AreClose(new Vector2(1080, 1920), l.Size);
            Assert.AreEqual(1f, l.Scale, Eps);
        }

        [Test]
        public void Notch_SafeAreaTopInset_ShrinksHeightOnly()
        {
            var screen = new Vector2(1080, 2340);
            var l = StageFitter.Compute(screen, new Rect(0, 60, 1080, 2148), StageFitter.ExpandCanvasSize(screen, Aspect), Aspect);
            AreClose(new Vector2(0, 60), l.Position);
            AreClose(new Vector2(1080, 2148), l.Size);
            Assert.AreEqual(1f, l.Scale, Eps);
        }

        [Test]
        public void SideInsets_NarrowSafeWidth_ScalesDown()
        {
            var screen = new Vector2(1080, 1920);
            var l = StageFitter.Compute(screen, new Rect(54, 0, 972, 1920), StageFitter.ExpandCanvasSize(screen, Aspect), Aspect);
            Assert.AreEqual(0.9f, l.Scale, Eps);
            AreClose(new Vector2(54, 0), l.Position);
            AreClose(new Vector2(1080, 1920 / 0.9f), l.Size);
        }

        [Test]
        public void Apply_SetsStageRootRect()
        {
            var go = new GameObject("StageRoot", typeof(RectTransform));
            try
            {
                var fitter = go.AddComponent<StageFitter>();
                fitter.Apply(new StageFitter.Layout { Position = new Vector2(180, 0), Size = new Vector2(1080, 1920), Scale = 1f });
                var rt = (RectTransform)go.transform;
                AreClose(new Vector2(180, 0), rt.anchoredPosition);
                AreClose(new Vector2(1080, 1920), rt.sizeDelta);
                AreClose(Vector2.zero, rt.anchorMin);
                AreClose(Vector2.zero, rt.pivot);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
