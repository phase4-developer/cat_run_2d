using Newtonsoft.Json.Linq;

namespace MV.Platform.Net
{
    /// <summary>
    /// 예시·점검 요청 1개: 보낸 값을 그대로 돌려주고 서버 시각을 붙인다. 요청 틀이 로컬·서버 양쪽에서 같은 모양으로 도는지 보는 용도다.
    /// 서버 함수 이름 <see cref="RequestName"/>, 파라미터 <c>value</c>(문자열, 필수), 응답 데이터 <c>{"echo": value, "serverTime": ms}</c>.
    /// 실제 기능 요청(정산·구매 등)은 MV-META 쪽이 같은 방식으로 옮긴다.
    /// </summary>
    public sealed class MvPingRequest : NetworkRequest
    {
        public const string RequestName = "net.ping";
        public const string ValueKey = "value";
        public const string EchoKey = "echo";
        public const string ServerTimeKey = "serverTime";

        public override string Name => RequestName;

        public MvPingRequest(string value)
        {
            Add(ValueKey, value);
        }

        /// <summary>로컬 처리기를 등록한다. 서버 시각은 <paramref name="clock"/>.</summary>
        public static MvLocalNetworkService RegisterLocal(MvLocalNetworkService service, IServerClock clock)
        {
            return service.Register(RequestName, call => new JObject
            {
                [EchoKey] = MvLocalNetworkService.RequireString(call, ValueKey),
                [ServerTimeKey] = clock.NowMs
            });
        }

        /// <summary>성공 응답의 되돌린 값. 실패면 <c>null</c>.</summary>
        public static string ReadEcho(NetworkResult result) => result.Success ? (string)result.Data?[EchoKey] : null;

        /// <summary>성공 응답의 서버 시각(UTC Unix ms). 실패면 0.</summary>
        public static long ReadServerTime(NetworkResult result) => result.Success ? (long?)result.Data?[ServerTimeKey] ?? 0 : 0;
    }
}
