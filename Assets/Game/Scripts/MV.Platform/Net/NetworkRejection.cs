using System;
using Newtonsoft.Json.Linq;

namespace MV.Platform.Net
{
    /// <summary>
    /// 로컬 서비스가 게임 오류(규칙 위반)를 돌려줄 때 던진다. 요청은 <see cref="NetworkFailure.Rejected"/>로 끝나고
    /// <see cref="Code"/>가 <see cref="NetworkResult.ServerErrorCode"/>로 간다. 서버도 같은 코드 문자열을 쓴다.
    /// Framework 대체: platform.network 고정본이 나오면 <c>PlatformLibrary.Network.NetworkRejection</c>으로 교체.
    /// </summary>
    public sealed class NetworkRejection : Exception
    {
        public string Code { get; }
        public JObject Details { get; }

        public NetworkRejection(string code, string message = null, JObject details = null)
            : base(string.IsNullOrEmpty(message) ? code : message)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("게임 오류 코드가 필요합니다.", nameof(code));
            Code = code;
            Details = details;
        }
    }
}
