using System;
using Newtonsoft.Json.Linq;

namespace MV.Platform.Net
{
    /// <summary>
    /// 요청 1건의 결과. 서버 결과와 로컬 결과가 같은 모양이다.
    /// Framework 대체: platform.network 고정본이 나오면 <c>PlatformLibrary.Network.NetworkResult</c>로 교체.
    /// </summary>
    public readonly struct NetworkResult
    {
        public NetworkFailure Failure { get; }
        public bool Success => Failure == NetworkFailure.None;
        /// <summary>세션 무효·점검·취소. 화면은 실패 팝업 대신 중단 처리를 한다.</summary>
        public bool IsInterruption => Failure == NetworkFailure.SessionInvalid || Failure == NetworkFailure.Maintenance || Failure == NetworkFailure.Cancelled;
        /// <summary>실제로 처리한 경로(로그·시험용).</summary>
        public NetworkRoute Route { get; }
        /// <summary>서버·로컬 서비스가 보낸 게임 오류 문자열(<see cref="NetworkRejection.Code"/>). 없으면 빈 문자열.</summary>
        public string ServerErrorCode { get; }
        public string Message { get; }
        /// <summary>성공 데이터.</summary>
        public JObject Data { get; }
        /// <summary>응답 전체(조사용).</summary>
        public JObject Response { get; }
        /// <summary>HTTP 상태 등. 로그용이고 분기하지 않는다.</summary>
        public int ProviderCode { get; }
        public string Detail { get; }

        private NetworkResult(NetworkFailure failure, NetworkRoute route, string serverErrorCode, string message, JObject data, JObject response, int providerCode, string detail)
        {
            Failure = failure;
            Route = route;
            ServerErrorCode = serverErrorCode ?? string.Empty;
            Message = message ?? string.Empty;
            Data = data;
            Response = response;
            ProviderCode = providerCode;
            Detail = detail ?? string.Empty;
        }

        public static NetworkResult Succeeded(JObject data, JObject response = null, int providerCode = 0)
            => new NetworkResult(NetworkFailure.None, NetworkRoute.None, null, null, data ?? new JObject(), response, providerCode, null);

        public static NetworkResult Failed(NetworkFailure failure, string serverErrorCode = null, string message = null,
            JObject response = null, int providerCode = 0, string detail = null)
        {
            if (failure == NetworkFailure.None) throw new ArgumentException("성공은 Succeeded로 만듭니다.", nameof(failure));
            return new NetworkResult(failure, NetworkRoute.None, serverErrorCode, message, null, response, providerCode, detail);
        }

        internal NetworkResult WithRoute(NetworkRoute route)
            => new NetworkResult(Failure, route, ServerErrorCode, Message, Data, Response, ProviderCode, Detail);

        internal NetworkResult AsApplyFailed(Exception exception)
            => new NetworkResult(NetworkFailure.ApplyFailed, Route, ServerErrorCode, Message, Data, Response, ProviderCode, exception?.GetType().Name + ": " + exception?.Message);

        public override string ToString()
            => Success ? "Success(" + Route + ")" : Failure + "(" + Route + ") " + ServerErrorCode + " " + Detail;
    }
}
