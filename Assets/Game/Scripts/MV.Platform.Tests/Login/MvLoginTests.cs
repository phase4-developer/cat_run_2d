using System;
using MV.Platform.Login;
using NUnit.Framework;
using PlatformLibrary;

namespace MV.Platform.Tests.Login
{
    /// <summary>게스트 로그인 스텁(MV-PLAT-LOGIN): 첫 실행 생성, 재실행 같은 id, 공급자 없음 연결, 로그아웃, 공급자 교체 지점.</summary>
    public sealed class MvLoginTests
    {
        private MvMemoryLoginStore _store;
        private int _generated;

        [SetUp]
        public void SetUp()
        {
            _store = new MvMemoryLoginStore();
            _generated = 0;
        }

        private MvLogin NewLogin() => new MvLogin(_store, () => "guest-" + (++_generated));

        private static MvLoginResult Call(Action<Action<MvLoginResult>> action)
        {
            MvLoginResult got = null;
            action(r => got = r);
            Assert.That(got, Is.Not.Null, "로컬 스텁은 같은 호출 안에서 답한다");
            return got;
        }

        [Test]
        public void FirstRun_GuestSignIn_CreatesAndStoresGuestId()
        {
            using (var login = NewLogin())
            {
                Assert.That(login.HasLoginHistory, Is.False);
                Assert.That(login.IsAvailable(MvLoginMethod.Guest), Is.True);
                MvLoginAccount changed = null;
                login.AccountChanged += a => changed = a;

                MvLoginResult r = Call(cb => login.SignIn(MvLoginMethod.Guest, cb));

                Assert.That(r.Success, Is.True, r.Detail);
                Assert.That(r.Account.UserId, Is.EqualTo("guest-1"));
                Assert.That(r.Account.IsGuest, Is.True);
                Assert.That(r.Account.LinkedMethods, Is.Empty);
                Assert.That(login.IsSignedIn, Is.True);
                Assert.That(login.IsBusy, Is.False);
                Assert.That(changed, Is.SameAs(login.Account));
                Assert.That(_store.Get(MvLoginStoreKeys.GuestId), Is.EqualTo("guest-1"));
                Assert.That(_store.Get(MvLoginStoreKeys.LastMethod), Is.EqualTo(MvLoginMethod.Guest.Id));
                Assert.That(login.HasLoginHistory, Is.True);
            }
        }

        [Test]
        public void Restart_RestoreAndGuestSignIn_KeepSameGuestId()
        {
            using (var first = NewLogin())
                Assert.That(Call(cb => first.SignIn(MvLoginMethod.Guest, cb)).Account.UserId, Is.EqualTo("guest-1"));

            using (var second = NewLogin())
            {
                Assert.That(second.IsSignedIn, Is.False, "재실행 직후는 로그인 전");
                Assert.That(second.HasLoginHistory, Is.True);
                MvLoginResult restored = Call(second.RestoreSession);
                Assert.That(restored.Success, Is.True, restored.Detail);
                Assert.That(restored.Account.UserId, Is.EqualTo("guest-1"));
                Assert.That(restored.Account.IsGuest, Is.True);
            }

            using (var third = NewLogin())
                Assert.That(Call(cb => third.SignIn(MvLoginMethod.Guest, cb)).Account.UserId, Is.EqualTo("guest-1"));

            Assert.That(_generated, Is.EqualTo(1), "게스트 id는 첫 실행에 한 번만 만든다");
        }

        [Test]
        public void NoProvider_LinkAndSignIn_AreUnsupported()
        {
            using (var login = NewLogin())
            {
                Assert.That(Call(cb => login.Link(MvLoginMethod.Google, cb)).Failure, Is.EqualTo(MvLoginFailure.NotSignedIn));
                MvLoginAccount guest = Call(cb => login.SignIn(MvLoginMethod.Guest, cb)).Account;

                foreach (MvLoginMethod method in new[] { MvLoginMethod.Google, MvLoginMethod.Apple })
                {
                    Assert.That(login.IsAvailable(method), Is.False, method.Id);
                    MvLoginResult link = Call(cb => login.Link(method, cb));
                    Assert.That(link.Failure, Is.EqualTo(MvLoginFailure.Unsupported), method.Id);
                    Assert.That(link.Account, Is.SameAs(guest), "실패해도 현재 계정 그대로");
                    Assert.That(Call(cb => login.SignIn(method, cb)).Failure, Is.EqualTo(MvLoginFailure.Unsupported), method.Id);
                }

                Assert.That(login.Account, Is.SameAs(guest));
                Assert.That(login.Account.LinkedMethods, Is.Empty);
                Assert.That(login.IsBusy, Is.False);
                Assert.That(_store.Get(MvLoginStoreKeys.LinkedProviders), Is.Null);
            }
        }

        [Test]
        public void SignOut_ClearsSession_ForgetsLastMethod_KeepsDeviceGuestId()
        {
            using (var login = NewLogin())
            {
                Call(cb => login.SignIn(MvLoginMethod.Guest, cb));
                int changes = 0;
                MvLoginAccount last = login.Account;
                login.AccountChanged += a => { changes++; last = a; };

                login.SignOut();

                Assert.That(login.IsSignedIn, Is.False);
                Assert.That(login.Account, Is.Null);
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(last, Is.Null);
                Assert.That(login.HasLoginHistory, Is.False);
                Assert.That(Call(login.RestoreSession).Failure, Is.EqualTo(MvLoginFailure.NoSavedLogin));
                Assert.That(Call(cb => login.Link(MvLoginMethod.Google, cb)).Failure, Is.EqualTo(MvLoginFailure.NotSignedIn));

                MvLoginResult again = Call(cb => login.SignIn(MvLoginMethod.Guest, cb));
                Assert.That(again.Account.UserId, Is.EqualTo("guest-1"), "기기 게스트 id는 로그아웃해도 남는다");
            }
        }

        [Test]
        public void SignOut_KeepLastMethod_AllowsRestore()
        {
            using (var login = NewLogin())
            {
                Call(cb => login.SignIn(MvLoginMethod.Guest, cb));
                login.SignOut(keepLastMethod: true);

                Assert.That(login.IsSignedIn, Is.False);
                Assert.That(login.HasLoginHistory, Is.True);
                Assert.That(Call(login.RestoreSession).Account.UserId, Is.EqualTo("guest-1"));
            }
        }

        [Test]
        public void ConfiguredProvider_LinkAddsMethod_AndRestoreUsesIt()
        {
            using (var login = NewLogin())
            {
                login.ConfigureProvider(MvLoginMethod.Google, () => new FakeAuthProvider("google"));
                Assert.That(login.IsAvailable(MvLoginMethod.Google), Is.True);
                Assert.That(login.IsAvailable(MvLoginMethod.Apple), Is.False);

                Call(cb => login.SignIn(MvLoginMethod.Guest, cb));
                MvLoginResult link = Call(cb => login.Link(MvLoginMethod.Google, cb));

                Assert.That(link.Success, Is.True, link.Detail);
                Assert.That(link.Account.UserId, Is.EqualTo("guest-1"), "연동해도 같은 계정");
                Assert.That(link.Account.IsGuest, Is.False);
                Assert.That(link.Account.HasMethod(MvLoginMethod.Google), Is.True);
                Assert.That(_store.Get(MvLoginStoreKeys.LastMethod), Is.EqualTo(MvLoginMethod.Guest.Id), "연동은 마지막 방식을 바꾸지 않는다");
            }

            using (var next = NewLogin())
            {
                next.ConfigureProvider(MvLoginMethod.Google, () => new FakeAuthProvider("google"));
                MvLoginResult signIn = Call(cb => next.SignIn(MvLoginMethod.Google, cb));
                Assert.That(signIn.Success, Is.True, signIn.Detail);
                Assert.That(signIn.Account.UserId, Is.EqualTo("guest-1"));
                Assert.That(_store.Get(MvLoginStoreKeys.LastMethod), Is.EqualTo(MvLoginMethod.Google.Id));
            }

            using (var restarted = NewLogin())
            {
                MvLoginResult restored = Call(restarted.RestoreSession);
                Assert.That(restored.Success, Is.True, restored.Detail);
                Assert.That(restored.Account.HasMethod(MvLoginMethod.Google), Is.True);
            }
        }

        private sealed class FakeAuthProvider : IPlatformAuthProvider
        {
            public FakeAuthProvider(string provider) => Provider = provider;
            public string Provider { get; }
            public bool IsSupported => true;
            public event Action CredentialsRevoked { add { } remove { } }

            public void SignIn(PlatformAuthRequest request, Action<PlatformAuthResult> callback, Action<PlatformAuthProgress> progress = null) =>
                callback(new PlatformAuthResult(new PlatformAuthCredential(Provider, "subject-1", "token-1")));

            public void Cancel() { }
            public void Update() { }
        }
    }
}
