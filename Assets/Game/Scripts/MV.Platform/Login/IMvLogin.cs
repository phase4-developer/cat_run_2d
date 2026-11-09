using System;

namespace MV.Platform.Login
{
    /// <summary>
    /// 게임이 부르는 로그인 창구. 모양은 Framework platform.login <c>LoginManager</c>와 같다.
    /// Framework 대체: platform.login 고정본이 나오면 이 인터페이스 구현을 <c>LoginManager</c> 어댑터로 교체한다.
    /// 로그인 호출은 한 번에 하나이고, 겹치면 <see cref="MvLoginFailure.Busy"/>다.
    /// </summary>
    public interface IMvLogin
    {
        bool IsSignedIn { get; }
        MvLoginAccount Account { get; }
        bool IsBusy { get; }
        /// <summary>마지막으로 로그인한 방식이 저장돼 있으면 참(<see cref="RestoreSession"/>을 부를 수 있다).</summary>
        bool HasLoginHistory { get; }

        /// <summary>로그인·연동·로그아웃으로 계정이 바뀌면 온다. 로그아웃이면 null.</summary>
        event Action<MvLoginAccount> AccountChanged;

        /// <summary>방식을 지금 쓸 수 있는지. 공급자가 없으면 거짓(버튼을 숨긴다).</summary>
        bool IsAvailable(MvLoginMethod method);

        /// <summary><c>Guest</c>는 저장된 게스트 계정을 쓰거나 만든다. 다른 방식은 공급자 자격 증명으로 로그인한다.</summary>
        void SignIn(MvLoginMethod method, Action<MvLoginResult> callback);

        /// <summary>화면 없이 마지막 방식으로 다시 로그인한다. 저장된 것이 없으면 <see cref="MvLoginFailure.NoSavedLogin"/>.</summary>
        void RestoreSession(Action<MvLoginResult> callback);

        /// <summary>로그인한 계정에 방식을 연동한다. 공급자가 없으면 <see cref="MvLoginFailure.Unsupported"/>.</summary>
        void Link(MvLoginMethod method, Action<MvLoginResult> callback);

        /// <summary>로그아웃. 기본으로 마지막 방식도 잊는다. 기기 게스트 id는 남는다.</summary>
        void SignOut(bool keepLastMethod = false);

        /// <summary>공급자 갱신(주 스레드 매 프레임). 스텁에서는 할 일이 없을 수 있다.</summary>
        void Update();
    }
}
