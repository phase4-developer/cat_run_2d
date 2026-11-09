using MV.Platform.Net;
using NUnit.Framework;

namespace MV.Platform.Tests.Net
{
    /// <summary>MV-PLAT-NET: 서버 시간 = 기기 UTC + 오프셋(주입·서버 시각 맞춤).</summary>
    public sealed class ServerClockTests
    {
        [Test]
        public void NowMs_IsDeviceUtcPlusInjectedOffset()
        {
            long device = 1_000_000L;
            var clock = new MvLocalServerClock(() => device, offsetMs: 5_000L);

            Assert.That(clock.OffsetMs, Is.EqualTo(5_000L));
            Assert.That(clock.NowMs, Is.EqualTo(1_005_000L));

            device += 250L;
            Assert.That(clock.NowMs, Is.EqualTo(1_005_250L), "기기 시계가 흐르면 같은 오프셋으로 따라간다");

            clock.SetOffset(-60_000L);
            Assert.That(clock.NowMs, Is.EqualTo(940_250L));
        }

        [Test]
        public void SetServerTime_AlignsOffsetToServer()
        {
            long device = 2_000_000L;
            var clock = new MvLocalServerClock(() => device);
            Assert.That(clock.OffsetMs, Is.EqualTo(0L), "주입이 없으면 기기 시각 그대로");

            clock.SetServerTime(2_090_000L);

            Assert.That(clock.OffsetMs, Is.EqualTo(90_000L));
            Assert.That(clock.NowMs, Is.EqualTo(2_090_000L));
            device += 1_000L;
            Assert.That(clock.NowMs, Is.EqualTo(2_091_000L));
        }

        [Test]
        public void DefaultClock_FollowsDeviceUtc()
        {
            var clock = new MvLocalServerClock(offsetMs: 0);
            long before = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long now = clock.NowMs;
            long after = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Assert.That(now, Is.InRange(before, after));
        }

        [Test]
        public void LocalPing_StampsServerClockTime()
        {
            NetworkManager.ClearAll();
            try
            {
                var clock = new MvLocalServerClock(() => 10_000L, offsetMs: 3_000L);
                NetworkManager.SetLocalService(MvPingRequest.RegisterLocal(new MvLocalNetworkService(), clock));
                NetworkResult result = default;

                NetworkManager.Post(new MvPingRequest("t"), r => result = r);

                Assert.That(result.Success, Is.True, result.ToString());
                Assert.That(MvPingRequest.ReadServerTime(result), Is.EqualTo(13_000L), "로컬 백엔드는 서버 시계(기기+오프셋)로 시각을 찍는다");
            }
            finally
            {
                NetworkManager.ClearAll();
            }
        }
    }
}
