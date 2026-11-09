using System;

namespace MV.Platform
{
    /// <summary>
    /// 가짜 광고 한 번(정본 <c>fakeAd</c> L2419: 팝업을 띄우고 1초마다 3→0, 0이면 닫고 완료).
    /// 화면은 <see cref="Changed"/>로 <see cref="SecondsLeft"/>를 그리고 <see cref="IsOpen"/>이 false면 팝업을 닫는다.
    /// </summary>
    public sealed class MvFakeAdSession
    {
        private readonly IMvAdTimer _timer;
        private readonly Action<MvAdResultCode> _finish;

        public MvAdKind Kind { get; }
        public string Placement { get; }
        public int SecondsLeft { get; private set; }
        public bool IsOpen { get; private set; }

        /// <summary>남은 초가 줄거나 닫힐 때.</summary>
        public event Action<MvFakeAdSession> Changed;

        internal MvFakeAdSession(MvAdKind kind, string placement, int seconds, IMvAdTimer timer, Action<MvAdResultCode> finish)
        {
            Kind = kind;
            Placement = placement;
            SecondsLeft = Math.Max(0, seconds);
            _timer = timer;
            _finish = finish;
            IsOpen = true;
        }

        internal void Begin()
        {
            if (SecondsLeft <= 0) { End(MvAdResultCode.Completed); return; }
            _timer.After(TimeSpan.FromSeconds(1), Tick);
        }

        /// <summary>사용자가 닫음 → Skipped. 이미 끝났으면 아무것도 하지 않는다.</summary>
        public void Close()
        {
            if (IsOpen) End(MvAdResultCode.Skipped);
        }

        private void Tick()
        {
            if (!IsOpen) return;
            SecondsLeft--;
            if (SecondsLeft <= 0) { End(MvAdResultCode.Completed); return; }
            Changed?.Invoke(this);
            _timer.After(TimeSpan.FromSeconds(1), Tick);
        }

        private void End(MvAdResultCode code)
        {
            SecondsLeft = 0;
            IsOpen = false;
            Changed?.Invoke(this);
            _finish(code);
        }
    }
}
