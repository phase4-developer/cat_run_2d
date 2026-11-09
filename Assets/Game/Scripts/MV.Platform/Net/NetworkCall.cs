using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MV.Platform.Net
{
    /// <summary>
    /// 공급자·로컬 서비스에 넘기는 요청 1회 시도.
    /// Framework 대체: platform.network 고정본이 나오면 <c>PlatformLibrary.Network.NetworkCall</c>로 교체
    /// (고정본에는 환경 키·세션·응답 형식이 더 있다).
    /// </summary>
    public sealed class NetworkCall
    {
        public NetworkRequest Request { get; }
        /// <summary>기본값 + 요청 값. 서버로 보낼 파라미터와 같다.</summary>
        public Dictionary<string, object> Parameters { get; }
        public NetworkRoute Route { get; }

        public NetworkCall(NetworkRequest request, Dictionary<string, object> parameters, NetworkRoute route)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Parameters = parameters ?? new Dictionary<string, object>(StringComparer.Ordinal);
            Route = route;
        }
    }

    /// <summary>
    /// 서버 전송(HTTP, Firebase Functions). 지금은 구현이 없다 — 서버가 생기면 공급자를 <see cref="NetworkManager.SetServerProvider(INetworkProvider)"/>로 건다.
    /// Framework 대체: platform.network 고정본이 나오면 <c>PlatformLibrary.Network.INetworkProvider</c>로 교체하고 공급자 패키지(.http/.firebase)를 쓴다.
    /// </summary>
    public interface INetworkProvider
    {
        bool IsReady();
        /// <summary>1회 전송. 전송 오류는 <see cref="NetworkFailure"/>로 바꿔 돌려준다. 서버 시간은 응답의 <see cref="NetworkManager.ServerTimeKey"/>.</summary>
        Task<NetworkResult> SendAsync(NetworkCall call, CancellationToken cancellationToken);
    }

    /// <summary>
    /// <see cref="NetworkRoute.Local"/>로 가는 요청을 처리하는 프로젝트 로컬 백엔드. 서버와 같은 이름·파라미터를 받고 같은 응답 JSON을 만든다.
    /// 상태를 직접 바꾸지 않고 새 값을 응답 데이터로 돌려준다(반영은 <see cref="NetworkManager.SetResponseApplier"/>).
    /// Framework 대체: platform.network 고정본이 나오면 <c>PlatformLibrary.Network.INetworkLocalService</c>로 교체.
    /// </summary>
    public interface INetworkLocalService
    {
        bool CanHandle(NetworkRequest request);
        /// <summary>성공 데이터를 돌려준다. 규칙 위반은 <see cref="NetworkRejection"/>을 던진다.</summary>
        Task<JObject> HandleAsync(NetworkCall call, CancellationToken cancellationToken);
    }
}
