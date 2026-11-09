namespace MV.Platform.Net
{
    /// <summary>
    /// 요청이 성공하지 못한 이유. 게임은 이 값으로 갈라지고 공급자 숫자 코드로는 갈라지지 않는다.
    /// Framework 대체: platform.network 고정본이 나오면 <c>PlatformLibrary.Network.NetworkFailure</c>로 교체(값 이름·순서 같음).
    /// 이 스텁이 만드는 값은 <see cref="None"/>, <see cref="Unsupported"/>, <see cref="NotInitialized"/>, <see cref="Cancelled"/>,
    /// <see cref="Rejected"/>, <see cref="ApplyFailed"/>, <see cref="ProviderError"/>뿐이다. 나머지는 서버 공급자가 돌려준다.
    /// </summary>
    public enum NetworkFailure
    {
        None,
        /// <summary>서버 공급자·로컬 서비스가 없거나 로컬 서비스가 이 요청을 처리하지 않는다.</summary>
        Unsupported,
        /// <summary>공급자는 있지만 준비되지 않았다.</summary>
        NotInitialized,
        NoConnection,
        Timeout,
        /// <summary>경로 전환, <see cref="NetworkManager.CancelAll"/>, 부른 쪽 취소.</summary>
        Cancelled,
        Unauthorized,
        SessionNotReady,
        SessionInvalid,
        Maintenance,
        /// <summary>서버·로컬 서비스가 게임 오류를 돌려줬다. <see cref="NetworkResult.ServerErrorCode"/>를 본다.</summary>
        Rejected,
        ServerError,
        InvalidResponse,
        /// <summary>응답 반영 함수가 예외를 던졌다. 반영되지 않은 데이터로 진행하지 않는다.</summary>
        ApplyFailed,
        ProviderError
    }
}
