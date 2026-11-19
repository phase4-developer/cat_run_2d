using UnityEngine;
using UnityEngine.EventSystems;

namespace MV.UI
{
    /// <summary>
    /// 런 드래그 입력면(04 §4.1.2). <c>UIHud_Run</c> 맨 아래 전체 화면 투명 그래픽(Raycast Target)에 붙는다.
    /// 포인터는 EventSystem(InputSystemUIInputModule) 이벤트로만 받으므로, 위에 놓인 UI(HUD 버튼·팝업 백판)가 가리면 입력이 오지 않는다.
    /// 화면 px → m 변환은 이 값을 읽는 쪽(MV.Boot 런 입력)이 한다. 한 번에 포인터 하나만 따른다.
    /// </summary>
    public class RunDragArea : MonoBehaviour, IPointerDownHandler, IInitializePotentialDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        /// <summary>지금 켜진 런 드래그 입력면(런 HUD가 보일 때). 로비 첫 터치 넘겨받기와 런 입력이 읽는다.</summary>
        public static RunDragArea Active { get; private set; }

        int _pointerId;

        /// <summary>누르고 있는 중인가.</summary>
        public bool Pressed { get; private set; }
        /// <summary>누른 순간 화면 x(px).</summary>
        public float StartX { get; private set; }
        /// <summary>지금 화면 x(px).</summary>
        public float X { get; private set; }
        /// <summary>새로 누를 때마다 1 늘어난다(읽는 쪽이 누른 순간을 알도록).</summary>
        public int PressSeq { get; private set; }

        void OnEnable() => Active = this;

        void OnDisable()
        {
            if (Active == this) Active = null;
            Pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData) => Begin(eventData);

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnDrag(PointerEventData eventData)
        {
            if (Pressed && eventData.pointerId == _pointerId) X = eventData.position.x;
        }

        public void OnEndDrag(PointerEventData eventData) => End(eventData);

        public void OnPointerUp(PointerEventData eventData) => End(eventData);

        /// <summary>
        /// 로비 첫 터치(04 §4.1.2 「같은 터치로 런 시작 + 드래그 시작」): 로비에서 누른 포인터의 누름·드래그 대상을 이 입력면으로 옮긴다.
        /// UI 모듈이 누름 대상을 정한 뒤(IInitializePotentialDragHandler)에 불러야 덮어쓰이지 않는다.
        /// </summary>
        public void TakeOver(PointerEventData eventData)
        {
            eventData.pointerPress = gameObject;
            eventData.rawPointerPress = gameObject;
            eventData.pointerDrag = gameObject;
            eventData.useDragThreshold = false;
            Begin(eventData);
        }

        void Begin(PointerEventData eventData)
        {
            if (Pressed) return;
            Pressed = true;
            _pointerId = eventData.pointerId;
            StartX = X = eventData.position.x;
            PressSeq++;
        }

        void End(PointerEventData eventData)
        {
            if (Pressed && eventData.pointerId == _pointerId) Pressed = false;
        }
    }
}
