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

        /// <summary>card를 target 자리·크기로(같은 부모 공간에서 target 중심, 크기는 target에 맞춘 배율).</summary>
        public static void Fit(RectTransform card, RectTransform target, out Vector3 localPos, out float scale)
        {
            var space = card.parent;
            localPos = space.InverseTransformPoint(target.TransformPoint(target.rect.center));
            var size = card.rect.size;
            Vector2 world = Vector2.Scale(target.rect.size, target.lossyScale);
            Vector2 unit = Vector2.Scale(size, space.lossyScale);
            scale = size.x > 1 && size.y > 1 && world.x > 1 && world.y > 1 ? Mathf.Min(world.x / unit.x, world.y / unit.y) : 1f;
        }

        public static void Place(RectTransform card, RectTransform target)
        {
            if (!target) return;
            Kill(card);
            Fit(card, target, out var pos, out float scale);
            card.localPosition = pos;
            card.localScale = Vector3.one * scale;
        }

        public static void Move(RectTransform card, RectTransform target, float sec, TweenCallback done)
            => Move(card, target, sec, done, HudEase.OutCubic, HudEase.OutCubic);

        /// <summary>위치·크기 보간 곡선을 따로 받는다(곡선은 연출을 맡는 섹션 프리팹 필드).</summary>
        public static void Move(RectTransform card, RectTransform target, float sec, TweenCallback done, HudEase moveEase, HudEase scaleEase)
        {
            if (!target) { done?.Invoke(); return; }
            Kill(card);
            Fit(card, target, out var pos, out float scale);
            if (sec <= 0 || !Animate) { card.localPosition = pos; card.localScale = Vector3.one * scale; done?.Invoke(); return; }
            DOTween.Sequence().SetUpdate(true).SetTarget(card)
                .Join(moveEase.Apply(card.DOLocalMove(pos, sec)))
                .Join(scaleEase.Apply(card.DOScale(scale, sec)))
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
