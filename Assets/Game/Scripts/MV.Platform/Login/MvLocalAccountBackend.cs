using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PlatformLibrary;

namespace MV.Platform.Login
{
    /// <summary>
    /// 서버 저장 전 단계의 기기 로컬 계정 공급자. 기기마다 게스트 계정 하나이고, 연동한 공급자는 그 계정에 붙는다.
    /// 자격 증명 토큰은 확인하지 않는다(서버가 없다).
    /// Framework 대체: platform.login.firebase 고정본과 Firebase Auth SDK가 들어오면
    /// <c>FirebaseAccountProvider</c>(platform.login의 <c>IAccountProvider</c>) 또는 같은 <see cref="IBackendAuthProvider"/> 구현으로 교체.
    /// </summary>
    public sealed class MvLocalAccountBackend : IBackendAuthProvider
    {
        private readonly IMvLoginStore _store;
        private readonly Func<string> _newGuestId;
        private bool _disposed;

        public MvLocalAccountBackend(IMvLoginStore store, Func<string> newGuestId = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _newGuestId = newGuestId ?? (() => Guid.NewGuid().ToString("N"));
        }

        public LoginAvailability Availability => _disposed ? LoginAvailability.TemporarilyUnavailable : LoginAvailability.Available;
        public string Environment => "local";
        public BackendAuthSession CurrentSession { get; private set; }

        public Task<BackendAuthResult> SignInAnonymousAsync() => Done(Open(EnsureGuestId()));

        public Task<BackendAuthResult> RestoreAsync(string provider)
        {
            string id = _store.Get(MvLoginStoreKeys.GuestId);
            if (string.IsNullOrEmpty(id)) return Done(BackendAuthStatus.NoCurrentUser, "no_local_account");
            if (provider != MvLoginMethod.AnonymousProvider && !LoadLinked().Contains(provider))
                return Done(BackendAuthStatus.NoCurrentUser, "provider_not_linked");
            return Done(Open(id));
        }

        /// <summary>로컬 스텁: 기기 계정(없으면 만든다)에 공급자를 붙이고 그 계정으로 로그인한다.</summary>
        public Task<BackendAuthResult> SignInAsync(PlatformAuthCredential credential)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));
            string id = EnsureGuestId();
            AddLinked(credential.Provider);
            return Done(Open(id));
        }

        public Task<BackendAuthResult> LinkAsync(PlatformAuthCredential credential)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));
            if (CurrentSession == null) return Done(BackendAuthStatus.NoCurrentUser, "not_signed_in");
            AddLinked(credential.Provider);
            return Done(Open(CurrentSession.UserId), linkAttempted: true, providerLinked: true);
        }

        public Task<BackendAuthResult> ContinueAsync(string continuation) => Done(BackendAuthStatus.Failed, "unsupported");

        public Task<BackendAuthResult> RefreshTokenAsync(bool forceRefresh) =>
            CurrentSession == null ? Done(BackendAuthStatus.NoCurrentUser, "not_signed_in") : Done(CurrentSession);

        public Task<BackendAuthResult> UnlinkAsync(string provider)
        {
            if (CurrentSession == null) return Done(BackendAuthStatus.NoCurrentUser, "not_signed_in");
            var linked = LoadLinked();
            if (linked.Remove(provider)) SaveLinked(linked);
            return Done(Open(CurrentSession.UserId));
        }

        public void DiscardContinuation(string continuation) { }
        public void Cancel() { }

        /// <summary>현재 세션만 닫는다. 기기 게스트 id와 연동 기록은 남긴다.</summary>
        public void SignOut() => CurrentSession = null;

        public void Dispose()
        {
            _disposed = true;
            CurrentSession = null;
        }

        private string EnsureGuestId()
        {
            string id = _store.Get(MvLoginStoreKeys.GuestId);
            if (!string.IsNullOrEmpty(id)) return id;
            id = _newGuestId();
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("The guest ID generator returned an empty ID.");
            _store.Set(MvLoginStoreKeys.GuestId, id);
            return id;
        }

        private BackendAuthSession Open(string userId)
        {
            var linked = LoadLinked();
            CurrentSession = new BackendAuthSession(userId, string.Empty, null, linked.Count == 0, linked);
            return CurrentSession;
        }

        private List<string> LoadLinked()
        {
            string raw = _store.Get(MvLoginStoreKeys.LinkedProviders);
            var list = new List<string>();
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (string part in raw.Split(','))
                if (part.Length > 0 && !list.Contains(part)) list.Add(part);
            return list;
        }

        private void AddLinked(string provider)
        {
            var linked = LoadLinked();
            if (linked.Contains(provider)) return;
            linked.Add(provider);
            SaveLinked(linked);
        }

        private void SaveLinked(List<string> linked)
        {
            if (linked.Count == 0) _store.Delete(MvLoginStoreKeys.LinkedProviders);
            else _store.Set(MvLoginStoreKeys.LinkedProviders, string.Join(",", linked));
        }

        private static Task<BackendAuthResult> Done(BackendAuthSession session, bool linkAttempted = false, bool providerLinked = false) =>
            Task.FromResult(new BackendAuthResult(BackendAuthStatus.Success, session, phase: "local", linkAttempted: linkAttempted, providerLinked: providerLinked));

        private static Task<BackendAuthResult> Done(BackendAuthStatus status, string error) =>
            Task.FromResult(new BackendAuthResult(status, error: error, phase: "local"));
    }
}
