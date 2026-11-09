using MV.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MV.Boot
{
    /// <summary>
    /// 런 입력(04 §4.1.2)을 Input System에서 읽는다. 터치·마우스는 같은 경로(<see cref="Pointer.current"/>)다.
    /// 드래그: 누른 순간 화면 x(sx)와 그때의 목표 tx(px)를 기억하고 <c>tx = px + (x − sx) / (W / DRAG_SCREEN_M)</c>.
    /// W = 9:16 게임 영역 폭(px). clamp는 MV.Run이 한다. 키: ←/→ 축(실시간 dt는 MV.Run이 곱한다).
    /// Framework 대체: Framework에 입력 패키지가 없어 게임 코드로 둔다(계약 MV-PLAY-INT).
    /// </summary>
    public sealed class MvRunInputReader
    {
        readonly double _dragScreenM;
        bool _dragging;
        float _sx;
        double _px;

        public MvRunInputReader(double dragScreenM)
        {
            _dragScreenM = dragScreenM > 0 ? dragScreenM : 7.2;
        }

        /// <summary>새 런: 드래그 기억을 버린다. 로비를 누른 그 손가락이 아직 눌려 있으면 다음 읽기에서 새 런의 tx(0)부터 드래그한다.</summary>
        public void Reset() => _dragging = false;

        /// <summary>9:16 게임 영역 폭(px). 화면이 더 넓으면 높이 기준.</summary>
        public static double GameAreaWidth(float screenW, float screenH) => Mathf.Max(1f, Mathf.Min(screenW, screenH * 9f / 16f));

        public RunInput Read(RunState s)
        {
            var input = RunInput.None;
            var pointer = Pointer.current;
            if (pointer != null && s != null)
            {
                bool pressed = pointer.press.isPressed;
                float x = pointer.position.ReadValue().x;
                if (pressed && !_dragging) { _dragging = true; _sx = x; _px = s.P.Tx; }
                else if (!pressed) _dragging = false;
                if (_dragging)
                {
                    double w = GameAreaWidth(Screen.width, Screen.height);
                    input.DragTargetX = _px + (x - _sx) / (w / _dragScreenM);
                }
            }
            var kb = Keyboard.current;
            if (kb != null)
                input.KeyAxis = (kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0);
            return input;
        }

        /// <summary>로비 출발 키(Space/Enter, 04 §4.1.2 「런 시작 키」). Enter는 UI Submit으로도 들어오지만 출발은 한 번만 된다.</summary>
        public static bool StartKeyPressed()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
        }
    }
}
