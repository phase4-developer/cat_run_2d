using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 투사체 프리팹 한 개의 공통 뿌리(<see cref="ProjectileView"/>의 풀 항목). 정렬·자리·켜고 끄기만 한다.
    /// 종류별 그림은 파생 모듈(<see cref="ToyProjectile"/>·<see cref="SpearProjectile"/>·<see cref="OrbBlade"/>)이 가진다.
    /// 정렬: 순위 Unit, 탐침 = 위치(오프셋 0), 안개 알파 없음(02 §2.3.1 7·8행, §2.4.1 대상 아님). 편향은 관리자가 표 값(sort.proj·sort.blade)으로 넣는다.
    /// </summary>
    [RequireComponent(typeof(DepthSortable))]
    public abstract class ProjectileSlot : MonoBehaviour
    {
        [SerializeField] DepthSortable _sortable;
        /// <summary>정렬·안개(그림 색을 바꿀 때 기본색으로 등록한다).</summary>
        protected DepthSortable Sortable => _sortable;
        /// <summary>그림을 카메라 쪽으로 세우는 빌보드(그림·장식의 부모).</summary>
        [SerializeField] protected Transform _billboard;
        /// <summary>
        /// 투사체 판정 형상(구). 프리팹 뿌리 바로 아래 `Hitbox`의 SphereCollider 반지름·중심을 디자이너가 편집한다(MV-HIT-RADIUS).
        /// 판정은 이 Collider를 물리에 올리지 않고 반지름·중심만 읽어 <see cref="RunHitWorld"/>가 SphereCast로 이동 구간을 훑는다.
        /// 풀 인스턴스에서는 꺼 둔다(다른 판정 질의에 걸리지 않게). 회전 장난감은 판정이 규칙 범위라 비워 둔다.
        /// </summary>
        [SerializeField] SphereCollider _hitbox;

        /// <summary>풀에서 막 꺼냈다(꼬리·장식 입자를 처음부터 다시).</summary>
        public bool Fresh { get; set; }
        /// <summary>관리자 프레임 표시(이번 프레임에 쓰였는지).</summary>
        public int Stamp { get; set; }
        public bool Visible => gameObject.activeSelf;

        /// <summary>프리팹 참조가 모두 있는지(관리자가 생성 때 확인한다).</summary>
        public virtual bool HasRefs => _sortable != null && _billboard != null;

        void Awake()
        {
            if (_hitbox != null) _hitbox.enabled = false;
        }

        /// <summary>
        /// 프리팹(원본)의 판정 구: 반지름 = Collider 반지름 × 가장 큰 축 배율, 중심 = 뿌리 위치 기준 Collider 중심(배율·회전 반영).
        /// 뿌리를 단위 배율 부모 아래 회전 없이 놓는다는 전제(<see cref="ProjectileView"/> 풀). Hitbox가 없으면 false.
        /// </summary>
        public bool TryHitShape(out ProjHitShape shape)
        {
            shape = default;
            if (_hitbox == null) return false;
            var t = _hitbox.transform;
            var ls = t.lossyScale;
            float scale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
            shape = new ProjHitShape(_hitbox.radius * scale, t.TransformPoint(_hitbox.center) - transform.position);
            return true;
        }

        /// <summary>정렬 편향(표 값).</summary>
        public void SetBias(float bias)
        {
            _sortable.Bias = bias;
            _sortable.Refresh();
        }

        protected void ShowAt(Vector3 pos)
        {
            transform.position = pos;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
