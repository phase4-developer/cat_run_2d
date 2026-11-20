using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 공용 버튼 눌림 연출(MV-UI-BTNANIM, 노션 「Button Animation (공통)」). 누르면 Animator 트리거 In, 떼면 Out.
    /// 디자이너는 버튼 오브젝트에 이 부품 하나만 추가한다. <see cref="RequireComponent"/>로 Animator가 함께 붙고,
    /// 이 스크립트의 Default References에 공용 Controller를 넣어 두면 추가할 때 그 Controller가 Animator에 들어간다.
    /// 트리거 이름은 연출 값이라 프리팹 직렬화 필드로 둔다. 버튼이 꺼져 있으면(interactable 아님) 재생하지 않는다.
    /// Controller가 없으면 연출 없이 클릭은 그대로 두고 경로를 경고로 한 번 남긴다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class ButtonClickAnim : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("추가할 때 Animator에 넣을 공용 Controller. 스크립트 Default References로 기본값을 정한다.")]
        [SerializeField] RuntimeAnimatorController controller;
        [SerializeField] string inTrigger = "In";
        [SerializeField] string outTrigger = "Out";

        Animator _animator;
        Selectable _selectable;
        bool _pressed;
        bool _warned;

        /// <summary>In을 재생하고 아직 Out을 재생하지 않은 상태.</summary>
        public bool IsPressed => _pressed;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _selectable = GetComponent<Selectable>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            if (_pressed) return;
            if (_selectable == null) _selectable = GetComponent<Selectable>();
            if (_selectable != null && !_selectable.IsInteractable()) return;
            if (!Ready()) return;
            _animator.ResetTrigger(outTrigger);
            _animator.SetTrigger(inTrigger);
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            if (!_pressed) return;
            _pressed = false;
            if (!Ready()) return;
            _animator.ResetTrigger(inTrigger);
            _animator.SetTrigger(outTrigger);
        }

        void OnDisable() => _pressed = false;

        bool Ready()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_animator != null && _animator.runtimeAnimatorController != null) return true;
            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning($"[ButtonClickAnim] Animator Controller가 없습니다: {PathOf(transform)}", this);
            }
            return false;
        }

        static string PathOf(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        // 에디터에서 부품을 추가할 때(또는 Reset) 공용 Controller를 비어 있는 Animator에 넣는다.
        void Reset()
        {
            var animator = GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController == null && controller != null)
                animator.runtimeAnimatorController = controller;
        }

        [ContextMenu("공용 Controller 적용")]
        void ApplyController()
        {
            var animator = GetComponent<Animator>();
            if (animator == null || controller == null) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(animator, "공용 Controller 적용");
#endif
            animator.runtimeAnimatorController = controller;
        }
    }
}
