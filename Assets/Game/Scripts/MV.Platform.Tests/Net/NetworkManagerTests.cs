using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MV.Platform.Net;
using NUnit.Framework;

namespace MV.Platform.Tests.Net
{
    /// <summary>MV-PLAT-NET: 요청·응답·거절 모양, 로컬 백엔드 왕복, 반영→콜백 순서, 경로·공급자 교체 지점.</summary>
    public sealed class NetworkManagerTests
    {
        private const long DeviceMs = 1_700_000_000_000L;

        private MvLocalServerClock _clock;
        private MvLocalNetworkService _local;

        [SetUp]
        public void SetUp()
        {
            NetworkManager.ClearAll();
            _clock = new MvLocalServerClock(() => DeviceMs);
            _local = MvPingRequest.RegisterLocal(new MvLocalNetworkService(), _clock);
        }

        [TearDown]
        public void TearDown() => NetworkManager.ClearAll();

        [Test]
        public void LocalRoute_RoundTrip_ReturnsSameShapeResponse()
        {
            Assert.That(NetworkManager.DefaultRoute, Is.EqualTo(NetworkRoute.Local), "서버가 없으므로 기본 경로는 Local");
            NetworkManager.SetLocalService(_local);
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
            Assert.That(request.IsPosting, Is.False);
            Assert.That(NetworkManager.IsWaitingForResponse, Is.False);
        }

        [Test]
        public void PostAsync_LocalRoute_CompletesWithoutServer()
        {
            NetworkManager.SetLocalService(_local);

            Task<NetworkResult> task = NetworkManager.PostAsync(new MvPingRequest("async"));

            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.Result.Success, Is.True, task.Result.ToString());
            Assert.That(MvPingRequest.ReadEcho(task.Result), Is.EqualTo("async"));
        }

        [Test]
        public void Rejection_PassesErrorCode_AndSkipsApplier()
        {
            NetworkManager.SetLocalService(_local);
            int applied = 0;
            NetworkManager.SetResponseApplier((request, result) => applied++);
            var results = new List<NetworkResult>();

            NetworkManager.Post(new MvPingRequest(""), results.Add);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Failure, Is.EqualTo(NetworkFailure.Rejected));
            Assert.That(results[0].ServerErrorCode, Is.EqualTo(MvNetErrors.BadParams));
            Assert.That(results[0].IsInterruption, Is.False);
            Assert.That(applied, Is.EqualTo(0), "거절된 응답은 반영하지 않는다");
        }

        [Test]
        public void Success_AppliesResponseBeforeCallback_ThenRaisesCompleted()
        {
            NetworkManager.SetLocalService(_local);
            var order = new List<string>();
            NetworkManager.SetResponseApplier((request, result) => order.Add("apply:" + MvPingRequest.ReadEcho(result)));
            NetworkManager.RequestCompleted += request => order.Add("completed:" + request.Name);

            NetworkManager.Post(new MvPingRequest("x"), result => order.Add("callback:" + result.Success));

            Assert.That(order, Is.EqualTo(new[] { "apply:x", "callback:True", "completed:" + MvPingRequest.RequestName }));
        }

        [Test]
        public void ApplierThrows_EndsAsApplyFailed()
        {
            NetworkManager.SetLocalService(_local);
            NetworkManager.SetResponseApplier((request, result) => throw new InvalidOperationException("반영 실패"));
            var results = new List<NetworkResult>();

            NetworkManager.Post(new MvPingRequest("x"), results.Add);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Failure, Is.EqualTo(NetworkFailure.ApplyFailed));
        }

        [Test]
        public void NoLocalService_EndsUnsupported_WithoutThrowing()
        {
            var results = new List<NetworkResult>();

            NetworkManager.Post(new MvPingRequest("x"), results.Add);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Failure, Is.EqualTo(NetworkFailure.Unsupported));
        }

        [Test]
        public void UnhandledRequest_EndsUnsupported()
        {
            NetworkManager.SetLocalService(_local);
            var results = new List<NetworkResult>();

            NetworkManager.Post(new OtherRequest(), results.Add);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Failure, Is.EqualTo(NetworkFailure.Unsupported));
        }

        [Test]
        public void RequestInstance_IsSentOnce()
        {
            NetworkManager.SetLocalService(_local);
            var request = new MvPingRequest("x");
            NetworkManager.Post(request);

            Assert.Throws<InvalidOperationException>(() => NetworkManager.Post(request));
            Assert.Throws<InvalidOperationException>(() => request.Add("value", "y"));
        }

        [Test]
        public void ServerRoute_UsesProvider_RouteSwitchCancelsAndDropsLateResponse()
        {
            NetworkManager.SetLocalService(_local);
            var provider = new PendingProvider();
            NetworkManager.SetServerProvider(provider);
            NetworkManager.DefaultRoute = NetworkRoute.Server;
            var results = new List<NetworkResult>();

            NetworkManager.Post(new MvPingRequest("srv"), results.Add);

            Assert.That(provider.Calls.Count, Is.EqualTo(1), "같은 요청이 서버 공급자로 간다");
            Assert.That(provider.Calls[0].Request.Name, Is.EqualTo(MvPingRequest.RequestName));
            Assert.That(provider.Calls[0].Parameters[MvPingRequest.ValueKey], Is.EqualTo("srv"));
            Assert.That(provider.Calls[0].Route, Is.EqualTo(NetworkRoute.Server));
            Assert.That(results.Count, Is.EqualTo(0));
            Assert.That(NetworkManager.IsWaitingForResponse, Is.True);

            NetworkManager.DefaultRoute = NetworkRoute.Local;

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Failure, Is.EqualTo(NetworkFailure.Cancelled));
            Assert.That(results[0].Route, Is.EqualTo(NetworkRoute.Server));
            Assert.That(results[0].IsInterruption, Is.True);
            Assert.That(provider.LastToken.IsCancellationRequested, Is.True);

            provider.Pending.TrySetCanceled();
            Assert.That(results.Count, Is.EqualTo(1), "늦은 응답은 버린다");
        }

        [Test]
        public void ServerRoute_WithoutProvider_EndsUnsupported()
        {
            NetworkManager.DefaultRoute = NetworkRoute.Server;
            var results = new List<NetworkResult>();

            NetworkManager.Post(new MvPingRequest("x"), results.Add);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Failure, Is.EqualTo(NetworkFailure.Unsupported));
            Assert.That(results[0].Route, Is.EqualTo(NetworkRoute.Server));
        }

        [Test]
        public void RequestRoute_OverridesDefaultRoute()
        {
            NetworkManager.SetLocalService(_local);
            NetworkManager.DefaultRoute = NetworkRoute.Server;
            var request = new MvPingRequest("local") { Route = NetworkRoute.Local };
            var results = new List<NetworkResult>();

            Assert.That(NetworkManager.ResolveRoute(request), Is.EqualTo(NetworkRoute.Local));
            NetworkManager.Post(request, results.Add);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Success, Is.True, results[0].ToString());
            Assert.That(results[0].Route, Is.EqualTo(NetworkRoute.Local));
        }

        [Test]
        public void DefaultRouteNone_FallsBackToLocal()
        {
            NetworkManager.DefaultRoute = NetworkRoute.Server;
            NetworkManager.DefaultRoute = NetworkRoute.None;

            Assert.That(NetworkManager.DefaultRoute, Is.EqualTo(NetworkRoute.Local));
        }

        [Test]
        public void PostAsync_CallerCancellation_EndsCancelled()
        {
            var provider = new PendingProvider();
            NetworkManager.SetServerProvider(provider);
            NetworkManager.DefaultRoute = NetworkRoute.Server;
            using (var cancel = new CancellationTokenSource())
            {
                Task<NetworkResult> task = NetworkManager.PostAsync(new MvPingRequest("x"), cancel.Token);
                Assert.That(task.IsCompleted, Is.False);

                cancel.Cancel();

                Assert.That(task.IsCompleted, Is.True);
                Assert.That(task.Result.Failure, Is.EqualTo(NetworkFailure.Cancelled));
                Assert.That(NetworkManager.IsWaitingForResponse, Is.False);
            }
        }

        private sealed class OtherRequest : NetworkRequest
        {
            public override string Name => "test.other";
        }

        private sealed class PendingProvider : INetworkProvider
        {
            public readonly List<NetworkCall> Calls = new List<NetworkCall>();
            public TaskCompletionSource<NetworkResult> Pending;
            public CancellationToken LastToken;

            public bool IsReady() => true;

            public Task<NetworkResult> SendAsync(NetworkCall call, CancellationToken cancellationToken)
            {
                Calls.Add(call);
                LastToken = cancellationToken;
                Pending = new TaskCompletionSource<NetworkResult>();
                return Pending.Task;
            }
        }
    }
}
