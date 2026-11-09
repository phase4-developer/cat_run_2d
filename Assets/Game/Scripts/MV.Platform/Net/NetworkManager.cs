using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MV.Platform.Net
{
    /// <summary>
    /// 서버 판정 기능(재화 변경·구매·보상·우편·서버 시간)의 요청 관리자. 게임 코드는 이 클래스와 요청 클래스만 안다.
    /// 처리 순서: 경로 결정(<see cref="ResolveRoute"/>) → 서버 공급자 또는 로컬 서비스 → 서버 시간 알림 → 응답 반영 함수 → 콜백 → <see cref="RequestCompleted"/>.
    /// 지금은 서버가 없어 <see cref="DefaultRoute"/>가 <see cref="NetworkRoute.Local"/>이다. 서버 전환은
    /// <see cref="SetServerProvider(INetworkProvider)"/>와 <c>DefaultRoute = NetworkRoute.Server</c>만으로 하고 요청 클래스·호출 코드는 고치지 않는다.
    /// Framework 대체: platform.network 고정본이 나오면 <c>PlatformLibrary.Network.NetworkManager</c>로 교체(같은 이름의 호출만 둔 부분집합).
    /// 고정본에만 있는 것: 대기열(<c>PostQueue</c>), 환경·세션·점검, 자동 재시도·시간 제한, 기본 실패 처리기, 로딩 표시, 요청 캐시.
    /// </summary>
    public static class NetworkManager
    {
        /// <summary>응답 JSON에서 서버 시간(UTC Unix ms)을 담는 키(고정본 기본 형식과 같음).</summary>
        public const string ServerTimeKey = "timeStamp";
        /// <summary>응답 JSON에서 성공 데이터를 담는 키(고정본 기본 형식과 같음).</summary>
        public const string DataKey = "data";

        private sealed class Entry
        {
            public NetworkRequest Request;
            public Action<NetworkResult> Callback;
            public NetworkRoute Route;
            public CancellationTokenSource Cancel;
            public CancellationTokenRegistration CallerCancel;
            public bool Done;
        }

        private static readonly List<Entry> Active = new List<Entry>();
        private static readonly Dictionary<string, INetworkProvider> Providers = new Dictionary<string, INetworkProvider>(StringComparer.Ordinal);
        private static INetworkLocalService _local;
        private static Action<NetworkRequest, NetworkResult> _applier;
        private static NetworkRoute _defaultRoute = NetworkRoute.Local;

        /// <summary>서버 응답에 서버 시간이 있을 때마다(UTC Unix ms). <see cref="MvLocalServerClock.SetServerTime"/>에 잇는다.</summary>
        public static event Action<long> ServerTimeReceived;
        /// <summary>요청이 끝나고 콜백까지 불린 뒤.</summary>
        public static event Action<NetworkRequest> RequestCompleted;

        public static bool IsWaitingForResponse => Active.Count > 0;

        // ---- 연결 ----------------------------------------------------------------------------------------

        /// <summary>기본 대상 서버 공급자. <c>null</c>이면 뺀다. 교체 지점: 서버 공급자 패키지 연결 뒤 여기에 건다.</summary>
        public static void SetServerProvider(INetworkProvider provider) => SetServerProvider(NetworkTarget.Default, provider);

        public static void SetServerProvider(string target, INetworkProvider provider)
        {
            if (string.IsNullOrEmpty(target)) throw new ArgumentException("공급자 대상 키가 필요합니다.", nameof(target));
            if (provider == null) Providers.Remove(target);
            else Providers[target] = provider;
        }

        /// <summary>로컬 백엔드. 게임이 구현한다(<see cref="MvLocalNetworkService"/>).</summary>
        public static void SetLocalService(INetworkLocalService service) => _local = service;

        /// <summary>성공 응답을 콜백 전에 저장소로 반영하는 함수. 예외를 던지면 결과가 <see cref="NetworkFailure.ApplyFailed"/>가 된다.</summary>
        public static void SetResponseApplier(Action<NetworkRequest, NetworkResult> applier) => _applier = applier;

        /// <summary>진행 중 요청을 <see cref="NetworkFailure.Cancelled"/>로 끝내고 연결·구독·경로를 처음 상태로 돌린다.</summary>
        public static void ClearAll()
        {
            CancelAll();
            Providers.Clear();
            _local = null;
            _applier = null;
            _defaultRoute = NetworkRoute.Local;
            ServerTimeReceived = null;
            RequestCompleted = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => ClearAll();

        // ---- 경로 ----------------------------------------------------------------------------------------

        /// <summary>
        /// <see cref="NetworkRequest.Route"/>가 <see cref="NetworkRoute.None"/>인 요청의 경로. 기본은 <see cref="NetworkRoute.Local"/>(서버 없음).
        /// <c>None</c>을 넣으면 <c>Local</c>이다. 바꾸면 진행 중 요청이 <see cref="NetworkFailure.Cancelled"/>로 끝난다.
        /// </summary>
        public static NetworkRoute DefaultRoute
        {
            get => _defaultRoute;
            set
            {
                if (value == NetworkRoute.None) value = NetworkRoute.Local;
                if (value == _defaultRoute) return;
                _defaultRoute = value;
                CancelAll();
            }
        }

        public static NetworkRoute ResolveRoute(NetworkRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return request.Route == NetworkRoute.None ? _defaultRoute : request.Route;
        }

        // ---- 보내기 --------------------------------------------------------------------------------------

        /// <summary>요청을 보낸다. 콜백은 요청마다 한 번 온다(실패·취소 포함). 로컬 서비스가 바로 끝나면 이 호출 안에서 콜백까지 끝난다.</summary>
        public static void Post(NetworkRequest request, Action<NetworkResult> callback = null)
            => Start(request, callback, CancellationToken.None);

        public static Task<NetworkResult> PostAsync(NetworkRequest request, CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<NetworkResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Start(request, result => completion.TrySetResult(result), cancellationToken);
            return completion.Task;
        }

        /// <summary>진행 중 요청을 모두 <see cref="NetworkFailure.Cancelled"/>로 끝낸다. 늦게 온 응답은 버린다.</summary>
        public static void CancelAll()
        {
            foreach (Entry entry in Active.ToList()) Cancel(entry);
        }

        private static void Start(NetworkRequest request, Action<NetworkResult> callback, CancellationToken external)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.MarkPosting();
            var entry = new Entry
            {
                Request = request,
                Callback = callback,
                Route = ResolveRoute(request),
                Cancel = new CancellationTokenSource()
            };
            Active.Add(entry);
            if (external.CanBeCanceled) entry.CallerCancel = external.Register(() => Cancel(entry));
            _ = RunAsync(entry);
        }

        private static async Task RunAsync(Entry entry)
        {
            if (entry.Done) return;
            CancellationToken token = entry.Cancel.Token;
            NetworkResult result;
            try
            {
                result = entry.Route == NetworkRoute.Server
                    ? await RunServerAsync(entry.Request, token)
                    : await RunLocalAsync(entry.Request, token);
            }
            catch (NetworkRejection rejection)
            {
                result = NetworkResult.Failed(NetworkFailure.Rejected, rejection.Code, rejection.Message,
                    rejection.Details == null ? null : new JObject { ["details"] = rejection.Details });
            }
            catch (OperationCanceledException)
            {
                result = NetworkResult.Failed(NetworkFailure.Cancelled, detail: "취소됨.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Network] " + entry.Request.Name + " 실패: " + exception.Message);
                result = NetworkResult.Failed(NetworkFailure.ProviderError, detail: exception.GetType().Name + ": " + exception.Message);
            }

            if (entry.Done) return;   // 경로 전환·취소로 이미 끝남. 늦은 응답은 버린다.
            Finish(entry, result.WithRoute(entry.Route));
        }

        private static async Task<NetworkResult> RunServerAsync(NetworkRequest request, CancellationToken token)
        {
            if (!Providers.TryGetValue(request.Target ?? NetworkTarget.Default, out INetworkProvider provider))
                return NetworkResult.Failed(NetworkFailure.Unsupported, detail: "서버 공급자 없음: " + request.Target);
            if (!provider.IsReady()) return NetworkResult.Failed(NetworkFailure.NotInitialized, detail: "서버 공급자가 준비되지 않음.");
            var call = new NetworkCall(request, request.BuildParameters(), NetworkRoute.Server);
            return await provider.SendAsync(call, token);
        }

        private static async Task<NetworkResult> RunLocalAsync(NetworkRequest request, CancellationToken token)
        {
            INetworkLocalService local = _local;
            if (local == null) return NetworkResult.Failed(NetworkFailure.Unsupported, detail: "로컬 서비스 없음.");
            if (!local.CanHandle(request)) return NetworkResult.Failed(NetworkFailure.Unsupported, detail: "로컬 서비스가 처리하지 않는 요청: " + request.Name);
            var call = new NetworkCall(request, request.BuildParameters(), NetworkRoute.Local);
            JObject data = await local.HandleAsync(call, token) ?? new JObject();
            return NetworkResult.Succeeded(data, new JObject { [DataKey] = data });
        }

        private static void Finish(Entry entry, NetworkResult result)
        {
            if (result.Response != null && TryReadTime(result.Response, out long time)) Raise(ServerTimeReceived, time);
            if (result.Success && _applier != null)
            {
                try { _applier(entry.Request, result); }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Network] " + entry.Request.Name + " 응답 반영 실패: " + exception.Message);
                    result = result.AsApplyFailed(exception);
                }
            }
            Complete(entry, result);
        }

        private static void Cancel(Entry entry)
        {
            if (entry.Done) return;
            Complete(entry, NetworkResult.Failed(NetworkFailure.Cancelled, detail: "취소됨.").WithRoute(entry.Route));
            entry.Cancel.Cancel();
        }

        private static void Complete(Entry entry, NetworkResult result)
        {
            if (entry.Done) return;
            entry.Done = true;
            entry.CallerCancel.Dispose();
            Active.Remove(entry);
            entry.Request.MarkCompleted(result);
            try { entry.Callback?.Invoke(result); }
            catch (Exception exception) { Debug.LogException(exception); }
            Raise(RequestCompleted, entry.Request);
        }

        private static bool TryReadTime(JObject response, out long time)
        {
            time = 0;
            JToken token = response[ServerTimeKey];
            if (token == null) return false;
            if (token.Type == JTokenType.Integer) { time = token.Value<long>(); return true; }
            if (token.Type == JTokenType.Float) { time = (long)token.Value<double>(); return true; }
            return false;
        }

        private static void Raise<T>(Action<T> handler, T value)
        {
            if (handler == null) return;
            try { handler(value); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
