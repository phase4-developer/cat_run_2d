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
        {
            if (!target) { done?.Invoke(); return; }
            Kill(card);
            Fit(card, target, out var pos, out float scale);
            if (sec <= 0 || !Animate) { card.localPosition = pos; card.localScale = Vector3.one * scale; done?.Invoke(); return; }
            DOTween.Sequence().SetUpdate(true).SetTarget(card)
                .Join(card.DOLocalMove(pos, sec).SetEase(Ease.OutCubic))
                .Join(card.DOScale(scale, sec).SetEase(Ease.OutCubic))
                .OnComplete(done);
        }
    }
}
