using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 마법탄 프리팹 루트(`Local/Projectile/Prefab/projectile`, 노션 「신작 이펙트 리소스 관리」 「투사체 프리팹 구조」).
    /// proj_Image(SpriteRenderer) = 무기 등급 그림, 그 아래 자식 = 무기 등급 꼬리(`VFX_Tail_n`), VFX_Card = 대마법 주문카드 효과 자리(연결은 원장 대기).
    /// 그림·꼬리는 표 `103_item.projSprite`·`projTail`이 정하고 <see cref="ProjectileView"/>가 넣는다. 프리팹에 들어 있는 예시 꼬리는 처음 깨어날 때 지운다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileRig : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _image;
        [SerializeField] Transform _card;

        GameObject _tail;
        string _tailAddress;
        bool _cleared;

        public SpriteRenderer Image => _image;
        /// <summary>대마법 주문카드 효과를 붙일 자리(VFX_Card).</summary>
        public Transform Card => _card;
        /// <summary>지금 끼운 꼬리 주소(시험·QA).</summary>
        public string TailAddress => _tailAddress;

        void Awake() => ClearExampleTail();

        /// <summary>proj_Image 아래 자식(프리팹 예시 꼬리)을 지운다. 꼬리 자리는 등급 꼬리 하나만 쓴다.</summary>
        void ClearExampleTail()
        {
            if (_cleared || _image == null) return;
            _cleared = true;
            var t = _image.transform;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i).gameObject;
                if (child == _tail) continue;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        /// <summary>그림을 바꾼다. 같으면 건드리지 않는다.</summary>
        public void SetSprite(Sprite sprite)
        {
            if (_image != null && _image.sprite != sprite) _image.sprite = sprite;
        }

        /// <summary>그림만 돌린다(꼬리는 자취라 그림 아래에 있어도 모양이 돌지 않는다). 각도는 Unity 로컬 z(도).</summary>
        public void SetSpin(float degrees)
        {
            if (_image != null) _image.transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        /// <summary>등급 꼬리를 끼운다. 주소가 같으면 그대로, 다르면 옛 꼬리를 지우고 새로 만든다. <paramref name="prefab"/>이 null이면 꼬리를 뺀다.</summary>
        public void SetTail(string address, GameObject prefab)
        {
            ClearExampleTail();
            if (_tailAddress == address && (_tail != null || prefab == null)) return;
            if (_tail != null)
            {
                if (Application.isPlaying) Destroy(_tail); else DestroyImmediate(_tail);
                _tail = null;
            }
            _tailAddress = null;
            if (prefab == null || _image == null) return;
            _tail = Instantiate(prefab, _image.transform, false);
            _tail.name = address;
            _tailAddress = address;
        }

        /// <summary>풀에서 다시 꺼낼 때: 꼬리 자취를 지워 옛 위치에서 선이 이어지지 않게 한다.</summary>
        public void ResetTrails()
        {
            if (_tail == null) return;
            foreach (var tr in _tail.GetComponentsInChildren<TrailRenderer>(true)) tr.Clear();
            foreach (var ps in _tail.GetComponentsInChildren<ParticleSystem>(true)) ps.Clear(true);
        }
    }
}
