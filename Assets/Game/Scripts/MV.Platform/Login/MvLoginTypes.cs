using System;
using System.Collections.Generic;

namespace MV.Platform.Login
{
    /// <summary>
    /// 로그인 방식. 문자열 ID 값이라 새 방식은 <c>new MvLoginMethod("steam")</c>처럼 더한다.
    /// Framework 대체: platform.login 고정본이 나오면 <c>PlatformLibrary.Login.LoginMethod</c>로 교체(같은 ID 문자열).
    /// </summary>
    public readonly struct MvLoginMethod : IEquatable<MvLoginMethod>
    {
        /// <summary>게스트. platform.core <c>LoginService</c>의 "anonymous" 계정으로 간다.</summary>
        public static readonly MvLoginMethod Guest = new MvLoginMethod("guest");
        public static readonly MvLoginMethod Google = new MvLoginMethod("google");
        public static readonly MvLoginMethod Apple = new MvLoginMethod("apple");

        /// <summary>platform.core <c>LoginService</c>가 게스트에 쓰는 공급자 ID.</summary>
        public const string AnonymousProvider = "anonymous";

        public string Id { get; }
        public bool IsGuest => Id == Guest.Id;

        public MvLoginMethod(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A login method ID is required.", nameof(id));
            Id = id;
        }

        /// <summary>platform.core <c>LoginService</c>·<c>IBackendAuthProvider</c>에 넘기는 공급자 ID.</summary>
        public string ProviderId => IsGuest ? AnonymousProvider : Id;

        public static MvLoginMethod FromProviderId(string provider) =>
            provider == AnonymousProvider ? Guest : new MvLoginMethod(provider);

        public bool Equals(MvLoginMethod other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MvLoginMethod other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : StringComparer.Ordinal.GetHashCode(Id);
        public override string ToString() => Id ?? string.Empty;
        public static bool operator ==(MvLoginMethod a, MvLoginMethod b) => a.Equals(b);
        public static bool operator !=(MvLoginMethod a, MvLoginMethod b) => !a.Equals(b);
    }

    /// <summary>실패 이유. 이름은 Framework <c>LoginResult.Failure</c>와 같게 둔다(교체 때 그대로 대응).</summary>
    public enum MvLoginFailure
    {
        None,
        NotConfigured,
        Unsupported,
        NotInitialized,
        Busy,
        Cancelled,
        NoSavedLogin,
        CredentialRejected,
        NotSignedIn,
        AlreadyLinkedToOtherAccount,
        SessionChanged,
        ProviderError,
    }

    /// <summary>로그인한 게임 계정.</summary>
    public sealed class MvLoginAccount
    {
        public string UserId { get; }
        /// <summary>연동한 계정 공급자(Google·Apple 등)가 없으면 참.</summary>
        public bool IsGuest { get; }
        /// <summary>게스트를 뺀 연동 방식.</summary>
        public IReadOnlyList<MvLoginMethod> LinkedMethods { get; }

        public MvLoginAccount(string userId, bool isGuest, IReadOnlyList<MvLoginMethod> linkedMethods)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("A user ID is required.", nameof(userId));
            UserId = userId;
            IsGuest = isGuest;
            LinkedMethods = linkedMethods ?? Array.Empty<MvLoginMethod>();
        }

        public bool HasMethod(MvLoginMethod method)
        {
            for (int i = 0; i < LinkedMethods.Count; i++)
                if (LinkedMethods[i] == method) return true;
            return false;
        }
    }

    public sealed class MvLoginResult
    {
        public bool Success => Failure == MvLoginFailure.None;
        public MvLoginFailure Failure { get; }
        /// <summary>성공이면 로그인한 계정. 실패면 호출 전 계정(없으면 null).</summary>
        public MvLoginAccount Account { get; }
        /// <summary>진단용 원인 문자열(공급자 오류 코드 등).</summary>
        public string Detail { get; }

        private MvLoginResult(MvLoginFailure failure, MvLoginAccount account, string detail)
        {
            Failure = failure;
            Account = account;
            Detail = detail ?? string.Empty;
        }

        public static MvLoginResult Ok(MvLoginAccount account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            return new MvLoginResult(MvLoginFailure.None, account, null);
        }

        public static MvLoginResult Fail(MvLoginFailure failure, MvLoginAccount current = null, string detail = null)
        {
            if (failure == MvLoginFailure.None) throw new ArgumentException("A failure is required.", nameof(failure));
            return new MvLoginResult(failure, current, detail);
        }
    }
}
