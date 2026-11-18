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
        /// <summary>그림을 카메라 쪽으로 세우는 빌보드(그림·장식의 부모).</summary>
        [SerializeField] protected Transform _billboard;

        /// <summary>풀에서 막 꺼냈다(꼬리·장식 입자를 처음부터 다시).</summary>
        public bool Fresh { get; set; }
        /// <summary>관리자 프레임 표시(이번 프레임에 쓰였는지).</summary>
        public int Stamp { get; set; }
        public bool Visible => gameObject.activeSelf;

        /// <summary>프리팹 참조가 모두 있는지(관리자가 생성 때 확인한다).</summary>
        public virtual bool HasRefs => _sortable != null && _billboard != null;

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
