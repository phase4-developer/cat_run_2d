namespace MV.Platform.Net
{
    /// <summary>
    /// 요청을 누가 처리하는지. <see cref="None"/>이면 <see cref="NetworkManager.DefaultRoute"/>를 따른다.
    /// Framework 대체: platform.network 고정본이 나오면 <c>PlatformLibrary.Network.NetworkRoute</c>로 교체.
    /// </summary>
    public enum NetworkRoute
    {
        None,
        Server,
        Local
    }

    /// <summary>
    /// 서버 공급자 키. 자기 대상이 없는 요청은 <see cref="Default"/>를 쓴다.
    /// Framework 대체: platform.network 고정본이 나오면 <c>PlatformLibrary.Network.NetworkTarget</c>로 교체.
    /// </summary>
    public static class NetworkTarget
    {
        public const string Default = "default";
    }
}
