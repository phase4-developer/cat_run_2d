using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// StageRoot를 안전 영역에 맞춘다(05 §5.1.2, D-30 권장안). Canvas 바로 아래 RectTransform에 붙인다.
    /// 폭 = min(안전 폭, 안전 높이 × 9/16)을 1080으로 보는 배율, 높이 = 안전 높이 / 배율, 가로 가운데.
    /// 그래서 자식은 늘 폭 1080(1u = 10.8)을 본다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class StageFitter : MonoBehaviour
    {
        public const float RefWidth = 1080f;
        public const float RefHeight = 1920f;

        /// <summary>Canvas 단위 결과. Position = Canvas 왼쪽 아래 기준 StageRoot 왼쪽 아래.</summary>
        public struct Layout
        {
            public Vector2 Position;
            /// <summary>StageRoot 로컬 크기(폭은 늘 1080).</summary>
            public Vector2 Size;
            public float Scale;
        }

        /// <summary>CanvasScaler Scale With Screen Size 1080×1920 Expand가 만드는 Canvas 크기.</summary>
        public static Vector2 ExpandCanvasSize(Vector2 screenPx)
        {
            float s = Mathf.Min(screenPx.x / RefWidth, screenPx.y / RefHeight);
            return s > 0 ? screenPx / s : new Vector2(RefWidth, RefHeight);
        }

        /// <summary>화면 픽셀·안전 영역 픽셀·Canvas 크기로 StageRoot 배치를 계산한다.</summary>
        public static Layout Compute(Vector2 screenPx, Rect safePx, Vector2 canvasSize)
        {
            if (screenPx.x <= 0 || screenPx.y <= 0)
                return new Layout { Position = Vector2.zero, Size = new Vector2(RefWidth, RefHeight), Scale = 1f };

            float kx = canvasSize.x / screenPx.x, ky = canvasSize.y / screenPx.y;
            var safe = new Rect(safePx.x * kx, safePx.y * ky, safePx.width * kx, safePx.height * ky);
            float w = Mathf.Min(safe.width, safe.height * 9f / 16f);
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
            var screen = new Vector2(Screen.width, Screen.height);
            var safe = Screen.safeArea;
            var canvas = parent.rect.size;
            if (!force && screen == _lastScreen && safe == _lastSafe && canvas == _lastCanvas) return;
            _lastScreen = screen;
            _lastSafe = safe;
            _lastCanvas = canvas;
            Apply(Compute(screen, safe, canvas));
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
