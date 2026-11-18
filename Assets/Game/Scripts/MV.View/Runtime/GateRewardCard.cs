using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 문 보상 묶음의 카드 한 장(<see cref="GateRewardDisplay"/>의 복제 원본). 틀·그림 SpriteRenderer 참조와 그림 맞춤 값만 갖는다.
    /// 카드 크기·배치는 묶음이 정하고, 이 부품은 받은 크기에 틀을 채우고 그림을 가운데 맞춘다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GateRewardCard : MonoBehaviour
    {
        [Tooltip("등급 틀(카드 크기를 꽉 채운다).")]
        [SerializeField] SpriteRenderer _frame;
        [Tooltip("무기 그림(카드 안 가운데, 비율 유지).")]
        [SerializeField] SpriteRenderer _icon;
        [Tooltip("그림 크기: 카드 너비·높이 중 작은 쪽에 대한 비율.")]
        [SerializeField] float _iconFill = 0.8f;
        [Tooltip("그림 높이 자리: 카드 높이에 대한 비율(+ 위).")]
        [SerializeField] float _iconOffsetY = 0.05f;

        public SpriteRenderer Frame => _frame;
        public SpriteRenderer Icon => _icon;

        /// <summary>틀·그림을 바꾸고(null이면 그대로) 카드 크기 <paramref name="size"/>(m)에 맞춘다. 정렬 = 틀 order, 그림 order + 1.</summary>
        public void Paint(Sprite frame, Sprite icon, Vector2 size, int order)
        {
            if (_frame != null)
            {
                if (frame != null) _frame.sprite = frame;
                var s = _frame.sprite;
                if (s != null)
                {
                    var b = s.bounds.size;
                    _frame.transform.localScale = new Vector3(b.x > 0 ? size.x / b.x : 1f, b.y > 0 ? size.y / b.y : 1f, 1f);
                }
                _frame.sortingOrder = order;
            }
            if (_icon != null)
            {
                if (icon != null) _icon.sprite = icon;
                var s = _icon.sprite;
                if (s != null)
                {
                    var b = s.bounds.size;
                    float k = Mathf.Min(b.x > 0 ? size.x / b.x : 1f, b.y > 0 ? size.y / b.y : 1f) * _iconFill;
                    _icon.transform.localScale = new Vector3(k, k, 1f);
                }
                _icon.transform.localPosition = new Vector3(0f, size.y * _iconOffsetY, -0.001f);
                _icon.sortingOrder = order + 1;
            }
        }
    }
}
