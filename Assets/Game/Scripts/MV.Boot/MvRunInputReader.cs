using System;
using MV.Run;
using MV.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MV.Boot
{
    /// <summary>
    /// 런 입력(04 §4.1.2). 포인터는 EventSystem으로 받은 <see cref="RunDragArea"/>(런 HUD 드래그 입력면) 값만 읽고,
    /// 키는 Input System 액션으로 읽는다. 장치 상태를 직접 폴링하지 않는다 — 팝업 백판 같은 UI가 가리면 포인터 입력이 오지 않는다.
    /// 드래그: 누른 순간 화면 x(sx)와 그때의 목표 tx(px)를 기억하고 <c>tx = px + (x − sx) / (W / DRAG_SCREEN_M)</c>.
    /// W = 9:16 게임 영역 폭(px). clamp는 MV.Run이 한다. 키: ←/→ 축(실시간 dt는 MV.Run이 곱한다).
    /// </summary>
    public sealed class MvRunInputReader : IDisposable
    {
        readonly double _dragScreenM;
        readonly InputAction _move;
        readonly InputAction _start;
        int _seenPress;
        double _px;

        public MvRunInputReader(double dragScreenM)
        {
            _dragScreenM = dragScreenM > 0 ? dragScreenM : 7.2;
            _move = new InputAction("RunMove", InputActionType.Value);
            _move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            _start = new InputAction("RunStart", InputActionType.Button);
            _start.AddBinding("<Keyboard>/space");
            _start.AddBinding("<Keyboard>/enter");
            _start.AddBinding("<Keyboard>/numpadEnter");
            _move.Enable();
            _start.Enable();
        }

        /// <summary>새 런: 지금 누르고 있는 포인터를 새 누름으로 다시 본다(로비 첫 터치가 새 런의 tx=0부터 드래그).</summary>
        public void Reset() => _seenPress = -1;

        /// <summary>9:16 게임 영역 폭(px). 화면이 더 넓으면 높이 기준.</summary>
        public static double GameAreaWidth(float screenW, float screenH) => Mathf.Max(1f, Mathf.Min(screenW, screenH * 9f / 16f));

        public RunInput Read(RunState s)
        {
            var input = RunInput.None;
            var area = RunDragArea.Active;
            if (area && area.Pressed && s != null)
            {
                if (area.PressSeq != _seenPress) { _seenPress = area.PressSeq; _px = s.P.Tx; }
                double w = GameAreaWidth(Screen.width, Screen.height);
                input.DragTargetX = _px + (area.X - area.StartX) / (w / _dragScreenM);
            }
            input.KeyAxis = Mathf.RoundToInt(_move.ReadValue<float>());
            return input;
        }

        /// <summary>로비 출발 키(Space/Enter, 04 §4.1.2 「런 시작 키」)를 이번 프레임에 눌렀는가.</summary>
        public bool StartPressedThisFrame() => _start.WasPressedThisFrame();

        public void Dispose()
        {
            _move.Dispose();
            _start.Dispose();
        }
    }
}
