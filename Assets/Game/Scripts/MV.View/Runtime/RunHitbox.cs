using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 오브젝트 프리팹 안의 투사체 피격 형상 표시. 이 GameObject(와 자식)의 Collider가 투사체 판정의 정본이다(<see cref="RunHitWorld"/>).
    /// 크기·위치·회전은 프리팹에서 Collider로 편집한다. 그림과 일부러 다르게(여유·축소) 잡아도 된다. 코드·표는 이 크기를 덮어쓰지 않는다.
    /// 뷰가 오브젝트에 연결될 때 <see cref="RunObjectView"/>가 이 Collider를 <see cref="RunHitWorld"/>에 등록하고, 회수될 때 해제한다(이 컴포넌트가 스스로 등록하지 않는다).
    /// 그림 자식(Billboard 아래)이 아니라 프리팹 뿌리 바로 아래에 둔다. 투사체는 지면 위 <see cref="ProjLayout.ProjY"/> 높이로 날아가므로 그 높이를 덮어야 맞는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunHitbox : MonoBehaviour
    {
    }
}
