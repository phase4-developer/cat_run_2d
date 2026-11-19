using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MV.UI
{
    /// <summary>
    /// 로비 출발 영역. 포인터 누름을 EventSystem 이벤트로 받아 출발로 본다(위에 팝업 백판이 있으면 오지 않는다).
    /// 04 §4.1.2 「로비 첫 터치: 같은 터치로 런 시작 + 드래그 시작」: 출발로 런 HUD가 켜지면 같은 포인터를 <see cref="RunDragArea"/>로 넘긴다.
    /// UI 모듈은 누름 이벤트 뒤에 누름·드래그 대상을 다시 정하므로 출발과 넘기기는 <see cref="IInitializePotentialDragHandler"/>에서 한다
    /// (그래서 드래그 대상이 되도록 <see cref="IDragHandler"/>를 구현한다). 출발 키(Space/Enter)는 MV.Boot 런 입력의 Input System 액션이 맡는다.
    /// </summary>
    public class LobbyStartArea : MonoBehaviour, IDragHandler, IInitializePotentialDragHandler
    {
        public event Action Pressed;

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            Pressed?.Invoke();
            if (RunDragArea.Active) RunDragArea.Active.TakeOver(eventData);
        }

        public void OnDrag(PointerEventData eventData) { }

        /// <summary>시험·키 입력용.</summary>
        public void Press() => Pressed?.Invoke();
    }
}
