using UnityEngine;

namespace MV.View
{
    /// <summary>회전 장난감 1개(정본 draw2D L3800, orb 카드): 플레이어 둘레를 도는 빌보드, 등급 그림만. 그림만 로컬 z로 돌리고 굽힌 반지름 비율로 크기를 맞춘다.</summary>
    public sealed class OrbBlade : ProjectileSlot
    {
        [SerializeField] Transform _spin;
        [SerializeField] SpriteRenderer _image;

        public override bool HasRefs => base.HasRefs && _spin != null && _image != null;

        /// <summary>그림이 아직 없으면 숨기고 false.</summary>
        public bool Place(Sprite sprite, Vector3 pos, float spinDeg, float scale)
        {
            if (sprite == null)
            {
                Hide();
                return false;
            }
            if (_image.sprite != sprite) _image.sprite = sprite;
            _spin.localRotation = Quaternion.Euler(0f, 0f, spinDeg);
            _spin.localScale = new Vector3(scale, scale, 1f);
            ShowAt(pos);
            return true;
        }
    }
}
