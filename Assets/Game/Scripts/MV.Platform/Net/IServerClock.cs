using System;

namespace MV.Platform.Net
{
    /// <summary>
    /// 서버 기준 현재 시각(UTC Unix ms). 일일 초기화·쿨타임처럼 서버가 판정할 시간은 기기 시계 대신 이것을 읽는다.
    /// <see cref="NowMs"/>는 MV.Meta <c>IMvClock.NowMs</c>와 같은 단위·뜻이라 게임 쪽 어댑터 한 줄로 넘긴다
    /// (MV.Core에 공용 시계 인터페이스가 생기면 그것을 상속한다).
    /// Framework 대체: platform.network 고정본에는 시계가 없고 <c>NetworkManager.ServerTimeReceived</c>만 있다. 서버가 생기면
    /// <see cref="MvLocalServerClock.SetServerTime"/>을 그 이벤트에 잇는다.
    /// </summary>
    public interface IServerClock
    {
        long NowMs { get; }
        /// <summary>서버 시각 − 기기 시각(ms). 서버가 없으면 주입한 값(기본 0).</summary>
        long OffsetMs { get; }
    }

    /// <summary>
    /// 로컬 서버 시계: 기기 UTC + 오프셋. 서버가 없으면 오프셋은 주입값이고(개발 도구·시험),
    /// 서버가 생기면 응답의 서버 시간으로 오프셋을 맞춘다. 기기 시계 조작은 막지 않는다(서버 없음).
    /// </summary>
    public sealed class MvLocalServerClock : IServerClock
    {
        private readonly Func<long> _deviceUtcMs;

        /// <param name="deviceUtcMs">기기 UTC Unix ms. <c>null</c>이면 <see cref="DateTimeOffset.UtcNow"/>. 시험은 고정 값을 넣는다.</param>
        /// <param name="offsetMs">처음 오프셋.</param>
        public MvLocalServerClock(Func<long> deviceUtcMs = null, long offsetMs = 0)
        {
            _deviceUtcMs = deviceUtcMs ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            OffsetMs = offsetMs;
        }

        public long DeviceNowMs => _deviceUtcMs();
        public long NowMs => _deviceUtcMs() + OffsetMs;
        public long OffsetMs { get; private set; }

        /// <summary>오프셋을 직접 넣는다(개발 도구의 시간 이동).</summary>
        public void SetOffset(long offsetMs) => OffsetMs = offsetMs;

        /// <summary>서버가 알린 현재 시각으로 오프셋을 맞춘다. <see cref="NetworkManager.ServerTimeReceived"/>에 잇는다.</summary>
        public void SetServerTime(long serverUtcMs) => OffsetMs = serverUtcMs - _deviceUtcMs();
    }
}
