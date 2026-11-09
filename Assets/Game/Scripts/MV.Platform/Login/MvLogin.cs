using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlatformLibrary;

namespace MV.Platform.Login
{
    /// <summary>
    /// 게스트 로그인 스텁. platform.core 고정본 <see cref="LoginService"/>에 로컬 계정 공급자(<see cref="MvLocalAccountBackend"/>)를 붙여 쓴다.
    /// Google·Apple은 <see cref="ConfigureProvider"/>로 <see cref="IPlatformAuthProvider"/>를 넣는 자리만 있고, 넣지 않으면 <see cref="MvLoginFailure.Unsupported"/>다.
    /// Framework 대체: platform.login 고정본이 나오면 <c>LoginManager</c>(+ <c>.google</c>·<c>.apple</c>·<c>.firebase</c> 공급자) 어댑터로 교체.
    /// </summary>
    public sealed class MvLogin : IMvLogin, IDisposable
    {
        private readonly IMvLoginStore _store;
        private readonly PlatformServiceResolver _resolver;
        private readonly bool _ownsResolver;
        private readonly LoginService _service;
        private int _requestSeq;
        private bool _disposed;

        public MvLoginAccount Account { get; private set; }
        public bool IsSignedIn => Account != null;
        public bool IsBusy { get; private set; }
        public bool HasLoginHistory => !string.IsNullOrEmpty(_store.Get(MvLoginStoreKeys.LastMethod));

        public event Action<MvLoginAccount> AccountChanged;

        /// <param name="store">로그인 상태 저장소. 게임은 <see cref="MvPlayerPrefsLoginStore"/>, 시험은 <see cref="MvMemoryLoginStore"/>.</param>
        /// <param name="newGuestId">게스트 id 생성기. 기본은 GUID.</param>
        /// <param name="resolver">공급자 등록부. 없으면 만들어 소유한다.</param>
        /// <param name="backend">계정 공급자. 없으면 <see cref="MvLocalAccountBackend"/>. 서버 계정 공급자 교체 지점.</param>
        public MvLogin(IMvLoginStore store, Func<string> newGuestId = null, PlatformServiceResolver resolver = null, IBackendAuthProvider backend = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _ownsResolver = resolver == null;
            _resolver = resolver ?? new PlatformServiceResolver();
            // firebase 칸은 platform.core에서 「계정 공급자 사용」 스위치다. 지금은 로컬 계정 공급자를 쓴다.
            // Google·Apple은 켜 두고, 공급자를 넣지 않았으면 LoginService가 MissingDependency로 답한다.
            var options = new PlatformLoginOptions(google: true, apple: true, firebase: true, anonymous: true);
            _service = new LoginService(options, _resolver);
            _service.SetBackend(backend ?? new MvLocalAccountBackend(store, newGuestId));
        }

        /// <summary>
        /// 계정 공급자 연결 자리. Google·Apple 같은 로그인 방식의 자격 증명 공급자를 넣는다.
        /// Framework 대체: platform.login.google·.apple 고정본이 나오면 <c>LoginManager.AddProvider</c>로 옮긴다.
        /// </summary>
        public void ConfigureProvider(MvLoginMethod method, Func<IPlatformAuthProvider> factory, Func<LoginAvailability> availability = null)
        {
            if (method.IsGuest) throw new ArgumentException("Guest uses the account provider.", nameof(method));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _service.ConfigureProvider(method.ProviderId, availability ?? (() => LoginAvailability.Available), factory);
        }

        public bool IsAvailable(MvLoginMethod method) => Availability(method) == LoginAvailability.Available;

        public void SignIn(MvLoginMethod method, Action<MvLoginResult> callback)
        {
            if (!Begin(callback)) return;
            if (method.IsGuest)
            {
                RunBackend(() => _service.SignInAnonymousAsync(), method, true, callback);
                return;
            }
            RequestCredential(method, callback, credential => RunBackend(() => _service.AuthenticateAsync(credential), method, true, callback));
        }

        public void RestoreSession(Action<MvLoginResult> callback)
        {
            if (!Begin(callback)) return;
            string last = _store.Get(MvLoginStoreKeys.LastMethod);
            if (string.IsNullOrEmpty(last))
            {
                Finish(MvLoginResult.Fail(MvLoginFailure.NoSavedLogin, Account), callback);
                return;
            }
            var method = new MvLoginMethod(last);
            RunBackend(() => _service.RestoreAsync(method.ProviderId), method, true, callback);
        }

        public void Link(MvLoginMethod method, Action<MvLoginResult> callback)
        {
            if (!Begin(callback)) return;
            if (!IsSignedIn) { Finish(MvLoginResult.Fail(MvLoginFailure.NotSignedIn), callback); return; }
            if (method.IsGuest) { Finish(MvLoginResult.Fail(MvLoginFailure.Unsupported, Account, "guest_link"), callback); return; }
            RequestCredential(method, callback, credential => RunBackend(() => _service.LinkAsync(credential), method, false, callback));
        }

        public void SignOut(bool keepLastMethod = false)
        {
            if (_disposed) return;
            _service.Cancel();
            _service.Backend?.SignOut();
            if (!keepLastMethod) _store.Delete(MvLoginStoreKeys.LastMethod);
            SetAccount(null);
        }

        public void Update()
        {
            if (!_disposed) _service.Update();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _service.Dispose();
            if (_ownsResolver) _resolver.Dispose();
            Account = null;
            IsBusy = false;
        }

        private LoginAvailability Availability(MvLoginMethod method) =>
            method.IsGuest ? _service.GetAvailability(MvLoginMethod.AnonymousProvider) : _service.GetAvailability(method.ProviderId);

        private bool Begin(Action<MvLoginResult> callback)
        {
            if (_disposed) { callback?.Invoke(MvLoginResult.Fail(MvLoginFailure.NotInitialized, null, "disposed")); return false; }
            if (IsBusy) { callback?.Invoke(MvLoginResult.Fail(MvLoginFailure.Busy, Account)); return false; }
            IsBusy = true;
            return true;
        }

        private void Finish(MvLoginResult result, Action<MvLoginResult> callback)
        {
            IsBusy = false;
            callback?.Invoke(result);
        }

        private void RequestCredential(MvLoginMethod method, Action<MvLoginResult> callback, Action<PlatformAuthCredential> next)
        {
            LoginAvailability available = Availability(method);
            if (available != LoginAvailability.Available)
            {
                Finish(MvLoginResult.Fail(FromAvailability(available), Account, available.ToString()), callback);
                return;
            }
            var request = new PlatformAuthRequest("mv-login-" + (++_requestSeq));
            _service.RequestCredential(method.ProviderId, request, result =>
            {
                if (_disposed) return;
                if (!result.Success) { Finish(MvLoginResult.Fail(FromError(result.Error), Account, result.Error), callback); return; }
                next(result.Credential);
            });
        }

        // LoginService의 비동기 호출은 로컬 공급자에서 바로 끝난다. 서버 공급자면 await 뒤 주 스레드(Unity 동기화 문맥)로 돌아온다.
        private async void RunBackend(Func<Task<BackendAuthResult>> start, MvLoginMethod method, bool remember, Action<MvLoginResult> callback)
        {
            BackendAuthResult result;
            try { result = await start(); }
            catch (Exception e)
            {
                if (!_disposed) Finish(MvLoginResult.Fail(MvLoginFailure.ProviderError, Account, e.GetType().Name), callback);
                return;
            }
            if (_disposed) return;
            if (!result.Success || result.Session == null)
            {
                Finish(MvLoginResult.Fail(FromBackend(result), Account, result.Error), callback);
                return;
            }
            if (remember) _store.Set(MvLoginStoreKeys.LastMethod, method.Id);
            SetAccount(ToAccount(result.Session));
            Finish(MvLoginResult.Ok(Account), callback);
        }

        private void SetAccount(MvLoginAccount account)
        {
            if (Account == null && account == null) return;
            Account = account;
            AccountChanged?.Invoke(account);
        }

        private static MvLoginAccount ToAccount(BackendAuthSession session)
        {
            var methods = new List<MvLoginMethod>();
            foreach (string provider in session.Providers)
                if (provider != MvLoginMethod.AnonymousProvider) methods.Add(MvLoginMethod.FromProviderId(provider));
            return new MvLoginAccount(session.UserId, session.IsAnonymous, methods);
        }

        private static MvLoginFailure FromAvailability(LoginAvailability state)
        {
            switch (state)
            {
                case LoginAvailability.Available: return MvLoginFailure.None;
                case LoginAvailability.MissingConfiguration: return MvLoginFailure.NotConfigured;
                case LoginAvailability.Initializing: return MvLoginFailure.NotInitialized;
                case LoginAvailability.TemporarilyUnavailable: return MvLoginFailure.Busy;
                default: return MvLoginFailure.Unsupported; // Disabled, UnsupportedPlatform, MissingDependency
            }
        }

        private static MvLoginFailure FromError(string error)
        {
            switch (error)
            {
                case "busy": return MvLoginFailure.Busy;
                case "cancelled": return MvLoginFailure.Cancelled;
                case "unsupported": return MvLoginFailure.Unsupported;
            }
            return Enum.TryParse(error, out LoginAvailability state) && state != LoginAvailability.Available
                ? FromAvailability(state)
                : MvLoginFailure.ProviderError;
        }

        private static MvLoginFailure FromBackend(BackendAuthResult result)
        {
            switch (result.Status)
            {
                case BackendAuthStatus.Success: return MvLoginFailure.ProviderError; // 세션 없는 성공
                case BackendAuthStatus.NoCurrentUser: return MvLoginFailure.NoSavedLogin;
                case BackendAuthStatus.CredentialAlreadyInUse: return MvLoginFailure.AlreadyLinkedToOtherAccount;
                case BackendAuthStatus.SessionChanged: return MvLoginFailure.SessionChanged;
                case BackendAuthStatus.Cancelled: return MvLoginFailure.Cancelled;
                case BackendAuthStatus.Busy: return MvLoginFailure.Busy;
                default: return FromError(result.Error);
            }
        }
    }
}
