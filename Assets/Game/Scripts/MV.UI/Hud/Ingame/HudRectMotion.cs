using DG.Tweening;
using UnityEngine;

namespace MV.UI
{
    /// <summary>HUD 카드를 다른 자리(RectTransform)의 위치·크기로 옮기는 공용 도우미(계단 카드·Card Pos 비행, MV-UI3-HUD6).</summary>
    public static class HudRectMotion
    {
        /// <summary>연출은 재생 중에만(에디터 시험은 끝 상태로 바로 놓는다).</summary>
        public static bool Animate => Application.isPlaying;

        public static void Kill(object target)
        {
            if (Animate) DOTween.Kill(target);
        }

        /// <summary>
        /// card를 target 자리·크기로(같은 부모 공간에서 target 중심, 크기는 target에 맞춘 배율).
        /// 크기를 구할 수 없으면(card·target rect 또는 배율이 0) false이고 <paramref name="scale"/>은 card의 지금 배율이다.
        /// 다른 값(예: 1)으로 바꾸지 않는다: 대체 값이 원인을 숨겨 도착 배율이 늘 1이던 문제가 사흘 동안 드러나지 않았다(MV-UI3-CLIMBFX3).
        /// world·unit은 월드 단위라 캔버스 배율(lossyScale ≈ 0.005)이면 1보다 훨씬 작다. 0만 막는다.
        /// </summary>
        public static bool Fit(RectTransform card, RectTransform target, out Vector3 localPos, out float scale)
        {
            var space = card.parent;
            localPos = space.InverseTransformPoint(target.TransformPoint(target.rect.center));
            Vector2 world = Vector2.Scale(target.rect.size, target.lossyScale);
            Vector2 unit = Vector2.Scale(card.rect.size, space.lossyScale);
            bool ok = Mathf.Abs(unit.x) > 0 && Mathf.Abs(unit.y) > 0 && Mathf.Abs(world.x) > 0 && Mathf.Abs(world.y) > 0;
            scale = ok ? Mathf.Min(world.x / unit.x, world.y / unit.y) : card.localScale.x;
            return ok;
        }

        /// <summary>크기를 구하지 못한 자리를 경로로 알린다(배율은 그대로 두고 위치만 옮긴다).</summary>
        static void WarnNoSize(RectTransform card, RectTransform target) =>
            Debug.LogWarning($"[HudRectMotion] 크기를 구할 수 없어 배율을 그대로 둡니다(rect 또는 배율 0): 카드 {PathOf(card)} {card.rect.size}, 자리 {PathOf(target)} {target.rect.size} lossy {target.lossyScale}", card);

        static string PathOf(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        public static void Place(RectTransform card, RectTransform target)
        {
            if (!target) return;
            Kill(card);
            if (!Fit(card, target, out var pos, out float scale)) WarnNoSize(card, target);
            card.localPosition = pos;
            card.localScale = Vector3.one * scale;
        }

        public static void Move(RectTransform card, RectTransform target, float sec, TweenCallback done)
            => Move(card, target, sec, done, HudEase.OutCubic, HudEase.OutCubic);

        /// <summary>
        /// 위치·크기 보간 곡선을 따로 받는다(곡선은 연출을 맡는 섹션 프리팹 필드).
        /// 도착 자리는 매 프레임 target에서 다시 읽는다: 막 복제한 Holder는 레이아웃이 다음 갱신 때 자리를 잡고,
        /// 비행 중 Holder가 늘어 레이아웃이 움직여도 카드가 실제 배치될 자리로 간다(MV-UI3-CLIMBFX2).
        /// </summary>
        public static void Move(RectTransform card, RectTransform target, float sec, TweenCallback done, HudEase moveEase, HudEase scaleEase)
        {
            if (!target) { done?.Invoke(); return; }
            Kill(card);
            if (!Fit(card, target, out var pos, out float scale)) WarnNoSize(card, target);
            if (sec <= 0 || !Animate) { card.localPosition = pos; card.localScale = Vector3.one * scale; done?.Invoke(); return; }
            Vector3 fromPos = card.localPosition;
            float fromScale = card.localScale.x;
            float pm = 0, sm = 0;
            DOTween.Sequence().SetTarget(card)
                .Join(moveEase.Apply(DOTween.To(() => pm, v => pm = v, 1f, sec)))
                .Join(scaleEase.Apply(DOTween.To(() => sm, v => sm = v, 1f, sec)))
                .OnUpdate(() =>
                {
                    if (!card) return;
                    if (target)
                    {
                        // 비행 중 크기를 못 구한 프레임은 마지막으로 구한 도착 배율을 그대로 쓴다(위치는 계속 따라간다).
                        bool ok = Fit(card, target, out pos, out float s2);
                        if (ok) scale = s2;
                    }
                    card.localPosition = Vector3.LerpUnclamped(fromPos, pos, pm);
                    card.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, scale, sm);
                })
                .OnComplete(done);
        }
    }

    /// <summary>
    /// 인스펙터에서 고르는 보간 곡선: DOTween Ease 프리셋, 또는 <see cref="useCurve"/>면 직접 그린 곡선(0~1 → 0~1).
    /// </summary>
    [System.Serializable]
    public struct HudEase
    {
        [Tooltip("DOTween Ease 프리셋")]
        public Ease ease;
        [Tooltip("켜면 프리셋 대신 아래 곡선을 쓴다")]
        public bool useCurve;
        public AnimationCurve curve;

        public HudEase(Ease ease)
        {
            this.ease = ease;
            useCurve = false;
            curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        }

        public static HudEase OutCubic => new HudEase(Ease.OutCubic);

        public T Apply<T>(T tween) where T : Tween
        {
            if (useCurve && curve != null && curve.length > 1) tween.SetEase(curve);
            else tween.SetEase(ease == Ease.Unset ? Ease.OutCubic : ease);
            return tween;
        }
    }
}
