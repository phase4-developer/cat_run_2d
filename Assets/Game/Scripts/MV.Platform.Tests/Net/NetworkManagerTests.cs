using System.Collections.Generic;
using MV.Platform.Net;
using NUnit.Framework;
using PlatformLibrary.Network;

namespace MV.Platform.Tests.Net
{
    /// <summary>
    /// MV-PLAT-NET: 게임 몫(로컬 서비스 등록, 핑 요청 왕복·거절)만 본다. 요청 관리자 자체 동작(반영 순서, 경로 전환 취소,
    /// 요청 1회 전송, 호출자 취소 등)은 platform.network 고정본의 패키지 시험이 맡는다.
    /// </summary>
    public sealed class NetworkManagerTests
    {
        private const long DeviceMs = 1_700_000_000_000L;

        private MvLocalNetworkService _local;

        [SetUp]
        public void SetUp()
        {
            NetworkManager.ClearAll();
            // 게임 시작 코드와 같은 연결: 서버가 없으므로 로컬 서비스 + 기본 경로 Local(고정본 기본은 Server).
            _local = MvPingRequest.RegisterLocal(new MvLocalNetworkService(), new MvLocalServerClock(() => DeviceMs));
            NetworkManager.SetLocalService(_local);
            NetworkManager.DefaultRoute = NetworkRoute.Local;
        }

        [TearDown]
        public void TearDown() => NetworkManager.ClearAll();

        [Test]
        public void LocalRoute_RoundTrip_ReturnsSameShapeResponse()
        {
            var request = new MvPingRequest("hello");
            var results = new List<NetworkResult>();

            NetworkManager.Post(request, results.Add);

            Assert.That(results.Count, Is.EqualTo(1), "로컬은 오프라인에서 바로 끝나고 콜백은 한 번");
            NetworkResult result = results[0];
            Assert.That(result.Success, Is.True, result.ToString());
            Assert.That(result.Route, Is.EqualTo(NetworkRoute.Local));
            Assert.That(MvPingRequest.ReadEcho(result), Is.EqualTo("hello"));
            Assert.That(MvPingRequest.ReadServerTime(result), Is.EqualTo(DeviceMs));
            Assert.That(request.Completed, Is.True);
        }

        [Test]
        public void LocalPing_EmptyValue_RejectedWithBadParams()
        {
            var results = new List<NetworkResult>();

            NetworkManager.Post(new MvPingRequest(""), results.Add);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Failure, Is.EqualTo(NetworkFailure.Rejected));
            Assert.That(results[0].ServerErrorCode, Is.EqualTo(MvNetErrors.BadParams));
            Assert.That(results[0].IsInterruption, Is.False);
        }

        [Test]
        public void UnhandledRequest_EndsUnsupported()
        {
            var results = new List<NetworkResult>();

            NetworkManager.Post(new OtherRequest(), results.Add);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Failure, Is.EqualTo(NetworkFailure.Unsupported));
        }

        [Test]
        public void Register_SameNameReplaces_UnregisterStopsHandling()
        {
            MvPingRequest.RegisterLocal(_local, new MvLocalServerClock(() => DeviceMs, offsetMs: 500L));
            NetworkResult replaced = default;

            NetworkManager.Post(new MvPingRequest("r"), r => replaced = r);

            Assert.That(MvPingRequest.ReadServerTime(replaced), Is.EqualTo(DeviceMs + 500L), "같은 이름은 나중 처리기로 바뀐다");

            Assert.That(_local.Unregister(MvPingRequest.RequestName), Is.True);
            Assert.That(_local.CanHandle(new MvPingRequest("u")), Is.False);
            NetworkResult removed = default;
            NetworkManager.Post(new MvPingRequest("u"), r => removed = r);
            Assert.That(removed.Failure, Is.EqualTo(NetworkFailure.Unsupported));
        }

        private sealed class OtherRequest : NetworkRequest
        {
            public override string Name => "test.other";
        }
    }
}
