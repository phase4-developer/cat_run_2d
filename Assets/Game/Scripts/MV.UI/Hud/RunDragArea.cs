using UnityEngine;
using UnityEngine.EventSystems;

namespace MV.UI
{
    /// <summary>
    /// 인게임 전용 런 드래그 입력(04 §4.1.2). <c>UIHud_Run</c> 루트에 붙고, 맨 아래 전체 화면 투명 그래픽(<c>RunDragCatch</c>, Raycast Target)이 빈 곳의 누름을 받는다.
    /// uGUI 포인터 이벤트는 부모로 올라오므로, 버튼이 아닌 HUD 그림 위에서 누른 것도 이 루트가 받는다. 버튼은 자기 누름·클릭을 그대로 받는다.
    /// 런 HUD는 런 시작에 켜지고 끝나면 숨으므로 런 밖에서는 입력을 받지 않는다(꺼지면 누름도 지운다).
    /// 포인터는 EventSystem(InputSystemUIInputModule) 이벤트로만 받으므로 HUD 위 레이어(팝업 백판)가 가리면 오지 않는다.
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

        /// <summary>이 입력면이 받은 누름만 바로 드래그한다. 버튼에서 시작한 누름은 문턱을 두어 클릭이 살게 한다.</summary>
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (Pressed && eventData.pointerId == _pointerId) eventData.useDragThreshold = false;
        }

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
