using System;

namespace MV.UI
{
    /// <summary>
    /// 결과 받기 뒤 닫기 순서(MV-UI3-RESULTCLOSE): 팝업 닫기 전환이 끝난 뒤에만 다음(게임 루프 → 로비)을 한 번 부른다.
    /// <list type="bullet">
    /// <item>열림 전환 중 받기(미리 터치): Framework <c>UIPopup.ClosePopup</c>은 전환 중이면 아무것도 하지 않는다. 전에는 그 직후 로비로 가서
    /// 결과 팝업이 남았다. 여기서는 열림 전환이 끝나길 기다렸다가 닫는다(<see cref="Tick"/>).</item>
    /// <item>닫기를 걸어도 열린 채면(Framework가 거절) 강제로 닫는다. 결과 팝업이 남지 않게 한다.</item>
    /// <item>닫힘 완료(onHide)나 파괴 중 먼저 온 쪽에서 다음을 한 번만 부른다.</item>
    /// </list>
    /// Framework 팝업 형식을 모르는 순수 로직이라 EditMode 시험이 가짜 <see cref="IPort"/>로 돌린다.
    /// </summary>
    public sealed class ResultCloseSequence
    {
        /// <summary>닫을 팝업(실제는 <see cref="ResultPopup"/>).</summary>
        public interface IPort
        {
            bool IsOpen { get; }
            bool IsTransitioning { get; }
            /// <summary>닫기 전환 시작(Framework <c>ClosePopup(true)</c>).</summary>
            void Close();
            /// <summary>전환 없이 바로 닫기(Framework <c>ForceClosePopup(true)</c>).</summary>
            void ForceClose();
        }

        readonly IPort _port;
        Action _then;
        bool _closeIssued;

        public ResultCloseSequence(IPort port) { _port = port ?? throw new ArgumentNullException(nameof(port)); }

        /// <summary>받기가 끝나 닫기를 시작했다(다음은 아직).</summary>
        public bool Started { get; private set; }
        /// <summary>다음(<c>then</c>)을 불렀다.</summary>
        public bool Done { get; private set; }
        /// <summary>열림 전환이 끝나길 기다리는 중.</summary>
        public bool WaitingShow => Started && !Done && !_closeIssued;

        /// <summary>닫기 시작. 두 번째부터는 무시한다(중복 입력).</summary>
        public bool Begin(Action then)
        {
            if (Started) return false;
            Started = true;
            _then = then;
            TryClose();
            return true;
        }

        /// <summary>매 프레임: 열림 전환이 끝났으면 닫기를 건다.</summary>
        public void Tick()
        {
            if (WaitingShow) TryClose();
        }

        void TryClose()
        {
            if (!_port.IsOpen)
            {
                // 이미 닫히는 중(전환 진행) → 닫힘 완료를 기다린다. 전환도 없으면 이미 닫혔다.
                _closeIssued = true;
                if (!_port.IsTransitioning) Closed();
                return;
            }
            if (_port.IsTransitioning) return; // 열림 전환 중: ClosePopup이 무시되므로 기다린다
            _closeIssued = true;
            _port.Close();
            if (_port.IsOpen) _port.ForceClose(); // 닫기가 거절되면 팝업이 남지 않게 강제로 닫는다
        }

        /// <summary>닫힘 완료(onHide) 또는 팝업 파괴. 시작된 뒤 처음 한 번만 다음을 부른다.</summary>
        public void Closed()
        {
            if (!Started || Done) return;
            Done = true;
            var then = _then;
            _then = null;
            then?.Invoke();
        }
    }
}
