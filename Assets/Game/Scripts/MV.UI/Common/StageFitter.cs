using MV.Data;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// StageRoot를 안전 영역에 맞춘다(05 §5.1.2, D-30 권장안). Canvas 바로 아래 RectTransform에 붙인다.
    /// 폭 = min(안전 폭, 안전 높이 × 기준 화면비)을 1080으로 보는 배율, 높이 = 안전 높이 / 배율, 가로 가운데.
    /// 기준 화면비 = <c>300 view.refWidthPx / view.refHeightPx</c>(표 한 곳, 옛 코드 9/16·1920). 자식은 늘 폭 1080(1u = 10.8)을 본다.
    /// 1080은 UI 좌표 단위 폭이다(장면 CanvasScaler 기준 해상도와 같아야 함 — 통합 Writer 경로라 표로 옮기지 않음).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class StageFitter : MonoBehaviour
    {
        /// <summary>UI 좌표 단위 폭(장면 CanvasScaler 기준 폭). 높이는 표 화면비로 계산한다(<see cref="RefHeight"/>).</summary>
        public const float RefWidth = 1080f;

        /// <summary>기준 화면비(폭/높이) = 300 view.refWidthPx / view.refHeightPx.</summary>
        public static float TableAspect(MvTables t) =>
            (float)(t.RunConfig.D(RunConfigKeys.View_RefWidthPx) / t.RunConfig.D(RunConfigKeys.View_RefHeightPx));

        /// <summary>UI 단위 기준 높이 = 1080 / 화면비.</summary>
        public static float RefHeight(float aspect) => RefWidth / aspect;

        /// <summary>Canvas 단위 결과. Position = Canvas 왼쪽 아래 기준 StageRoot 왼쪽 아래.</summary>
        public struct Layout
        {
            public Vector2 Position;
            /// <summary>StageRoot 로컬 크기(폭은 늘 1080).</summary>
            public Vector2 Size;
            public float Scale;
        }

        /// <summary>CanvasScaler Scale With Screen Size(1080 × 1080/화면비) Expand가 만드는 Canvas 크기.</summary>
        public static Vector2 ExpandCanvasSize(Vector2 screenPx, float aspect)
        {
            float refH = RefHeight(aspect);
            float s = Mathf.Min(screenPx.x / RefWidth, screenPx.y / refH);
            return s > 0 ? screenPx / s : new Vector2(RefWidth, refH);
        }

        /// <summary>화면 픽셀·안전 영역 픽셀·Canvas 크기로 StageRoot 배치를 계산한다.</summary>
        public static Layout Compute(Vector2 screenPx, Rect safePx, Vector2 canvasSize, float aspect)
        {
            if (screenPx.x <= 0 || screenPx.y <= 0)
                return new Layout { Position = Vector2.zero, Size = new Vector2(RefWidth, RefHeight(aspect)), Scale = 1f };

            float kx = canvasSize.x / screenPx.x, ky = canvasSize.y / screenPx.y;
            var safe = new Rect(safePx.x * kx, safePx.y * ky, safePx.width * kx, safePx.height * ky);
            float w = Mathf.Min(safe.width, safe.height * aspect);
            float scale = w > 0 ? w / RefWidth : 1f;
            return new Layout
            {
                Position = new Vector2(safe.x + (safe.width - w) * 0.5f, safe.y),
                Size = new Vector2(RefWidth, safe.height / scale),
                Scale = scale,
            };
        }

        RectTransform _rt;
        Vector2 _lastScreen;
        Rect _lastSafe;
        Vector2 _lastCanvas;

        public Layout Current { get; private set; }

        RectTransform Rt => _rt ? _rt : (_rt = (RectTransform)transform);

        void OnEnable() => Refit(true);

        void LateUpdate() => Refit(false);

        /// <summary>해상도·안전 영역·Canvas 크기가 바뀌면 다시 계산한다(정본 resize).</summary>
        public void Refit(bool force)
        {
            var parent = Rt.parent as RectTransform;
            if (!parent) return;
            var tables = MvTables.Current;
            if (tables == null) return; // 표가 읽히기 전에는 맞추지 않는다(대체 화면비 없음).
            var screen = new Vector2(Screen.width, Screen.height);
            var safe = Screen.safeArea;
            var canvas = parent.rect.size;
            if (!force && screen == _lastScreen && safe == _lastSafe && canvas == _lastCanvas) return;
            _lastScreen = screen;
            _lastSafe = safe;
            _lastCanvas = canvas;
            Apply(Compute(screen, safe, canvas, TableAspect(tables)));
        }

        public void Apply(Layout l)
        {
            Current = l;
            var rt = Rt;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = l.Position;
            rt.sizeDelta = l.Size;
            rt.localScale = new Vector3(l.Scale, l.Scale, 1f);
        }
    }
}
