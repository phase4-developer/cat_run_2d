using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MV.UI
{
    /// <summary>로비 출발 영역. 포인터 누름과 Submit(선택돼 있을 때 Enter)을 출발로 본다.</summary>
    public class LobbyStartArea : MonoBehaviour, IPointerDownHandler, ISubmitHandler
    {
        public event Action Pressed;

        void OnEnable()
        {
            // 선택된 UI가 없을 때 Enter로 출발(05 §5.4.1). 다른 선택이 있으면 건드리지 않는다.
            var es = EventSystem.current;
            if (es && !es.currentSelectedGameObject) es.SetSelectedGameObject(gameObject);
        }

        public void OnPointerDown(PointerEventData eventData) => Pressed?.Invoke();

        public void OnSubmit(BaseEventData eventData) => Pressed?.Invoke();

        /// <summary>시험·입력 어댑터용.</summary>
        public void Press() => Pressed?.Invoke();
    }
}
