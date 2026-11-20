using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 창(정본 draw2D L3802~3803): 머리 빌보드(`World_Proj_SpearHead_Fire`/`_Bolt`, `Atlas_Fx_Common`)와 꼬리 장식 파티클(`VFX_Spear_Fire`/`_Bolt`).
    /// 꼬리 띠는 장식 파티클의 리본이 그린다(예전 코드 선 메쉬는 꺼 둔 채였으므로 지웠다).
    /// </summary>
    public sealed class SpearProjectile : ProjectileSlot
    {
        [SerializeField] SpriteRenderer _head;
        [Tooltip("번개 창 머리 색(흰 그림 World_Proj_SpearHead에 곱한다). 불 창은 그림 색 그대로")]
        [SerializeField] Color _boltTint = new Color(0.45f, 0.75f, 1f, 1f);

        public Color BoltTint => _boltTint;

        GameObject _fx;
        string _fxAddress;

        public override bool HasRefs => base.HasRefs && _head != null;

        /// <summary>머리 그림이 아직 없으면 숨기고 false. 장식 프리팹이 아직 없으면 장식 없이 창만 그린다.</summary>
        public bool Place(Sprite head, string fxAddress, GameObject fxPrefab, Vector3 pos) => Place(head, Color.white, fxAddress, fxPrefab, pos);

        /// <summary>머리 그림을 <paramref name="tint"/> 색으로 그린다(흰 그림에 곱함).</summary>
        public bool Place(Sprite head, Color tint, string fxAddress, GameObject fxPrefab, Vector3 pos)
        {
            if (head == null)
            {
                Hide();
                return false;
            }
            if (_head.sprite != head) _head.sprite = head;
            if (_head.color != tint)
            {
                _head.color = tint;
                if (Sortable != null) Sortable.SetBaseColor(_head, tint);   // 안개가 기본색으로 덮어쓰지 않게
            }
            ShowAt(pos);
            SetFx(fxAddress, fxPrefab);
            if (Fresh)
            {
                Fresh = false;
                RestartFx();
            }
            return true;
        }

        /// <summary>장식을 끼운다. 주소가 같으면 켜기만, 다르면 옛것을 지우고 새로 만든다.</summary>
        void SetFx(string address, GameObject prefab)
        {
            if (_fxAddress == address && _fx != null)
            {
                if (!_fx.activeSelf) { _fx.SetActive(true); RestartFx(); }
                return;
            }
            if (prefab == null) return;
            if (_fx != null) Destroy(_fx);
            _fx = Instantiate(prefab, transform, false);
            _fx.name = address;
            _fxAddress = address;
        }

        /// <summary>풀에서 다시 꺼낸 창: 옛 자리에서 이어지는 입자가 생기지 않게 처음부터 다시 재생한다.</summary>
        void RestartFx()
        {
            if (_fx == null) return;
            foreach (var ps in _fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Simulate(0f, false, true);
                ps.Play(false);
            }
        }
    }
}
