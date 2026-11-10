using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PlatformLibrary.Network;

namespace MV.Platform.Net
{
    /// <summary>
    /// 로컬 백엔드(서버 없음). 요청 이름마다 처리기를 하나 등록하고, 처리기는 서버 함수와 같은 파라미터를 읽어
    /// 서버와 같은 응답 데이터 JSON을 돌려준다. 규칙 위반은 <see cref="NetworkRejection"/>(오류 코드)을 던진다.
    /// 처리기는 상태를 바꾸지 않는다 — 반영은 <see cref="NetworkManager.AddResponseApplier"/>로 등록한 반영기가 한다.
    /// 실제 요청 종류(정산·구매 등)는 각 기능 모듈이 자기 처리기를 <see cref="Register"/>로 더한다(MV.Platform은 게임 모듈을 참조하지 않는다).
    /// platform.network(0.1.0-pre.2)의 <see cref="INetworkLocalService"/> 구현이다. 고정본의 기본 경로는 Server라
    /// 서버가 없는 동안 게임 시작 코드가 <c>NetworkManager.DefaultRoute = NetworkRoute.Local</c>을 함께 건다.
    /// </summary>
    public sealed class MvLocalNetworkService : INetworkLocalService
    {
        private readonly Dictionary<string, Func<NetworkCall, JObject>> _handlers = new Dictionary<string, Func<NetworkCall, JObject>>(StringComparer.Ordinal);

        /// <summary>요청 이름 하나의 처리기. 같은 이름을 다시 넣으면 바꾼다.</summary>
        public MvLocalNetworkService Register(string requestName, Func<NetworkCall, JObject> handler)
        {
            if (string.IsNullOrWhiteSpace(requestName)) throw new ArgumentException("요청 이름이 필요합니다.", nameof(requestName));
            _handlers[requestName] = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        public bool Unregister(string requestName) => requestName != null && _handlers.Remove(requestName);

        public bool CanHandle(NetworkRequest request) => request != null && request.Name != null && _handlers.ContainsKey(request.Name);

        public Task<JObject> HandleAsync(NetworkCall call, CancellationToken cancellationToken)
        {
            if (call == null) throw new ArgumentNullException(nameof(call));
            cancellationToken.ThrowIfCancellationRequested();
            if (!_handlers.TryGetValue(call.Request.Name, out Func<NetworkCall, JObject> handler))
                throw new NetworkRejection(MvNetErrors.UnknownRequest, "로컬 처리기 없음: " + call.Request.Name);
            return Task.FromResult(handler(call) ?? new JObject());
        }

        /// <summary>파라미터 문자열 읽기. 없거나 비면 <see cref="MvNetErrors.BadParams"/>로 거절한다.</summary>
        public static string RequireString(NetworkCall call, string key)
        {
            if (call.Parameters.TryGetValue(key, out object value) && value is string text && text.Length > 0) return text;
            throw new NetworkRejection(MvNetErrors.BadParams, "파라미터 없음: " + key);
        }
    }

    /// <summary>공통 거절 코드(<see cref="NetworkRejection.Code"/>). 기능별 코드는 그 기능 모듈이 정한다(예: MV.Meta <c>MvMetaErrors</c>).</summary>
    public static class MvNetErrors
    {
        public const string UnknownRequest = "net.unknownRequest";
        public const string BadParams = "net.badParams";
    }
}
