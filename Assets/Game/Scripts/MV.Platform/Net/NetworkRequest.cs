using System;
using System.Collections;
using System.Collections.Generic;

namespace MV.Platform.Net
{
    /// <summary>
    /// 게임 요청의 부모. 서버 함수 하나마다 하위 클래스 하나를 두고 <see cref="Name"/>·<see cref="Add"/>로 이름·파라미터를 정한다.
    /// 서버와 로컬 백엔드는 같은 이름·파라미터를 받는다. 인스턴스는 한 번만 보낸다. 코루틴에서 <c>yield return request</c>로 기다린다.
    /// Framework 대체: platform.network 고정본이 나오면 <c>PlatformLibrary.Network.NetworkRequest</c>로 교체.
    /// 하위 클래스는 부모만 바꾸면 그대로 쓴다. 고정본의 <c>RequiresSession</c>·<c>BlockedDuringMaintenance</c>·<c>Retry</c>·<c>Timeout</c>·
    /// <c>ShowLoading</c>·<c>UseDefaultFailureHandling</c>은 서버가 생길 때 쓰므로 이 스텁에는 없다.
    /// </summary>
    public abstract class NetworkRequest : IEnumerator
    {
        private readonly Dictionary<string, object> _params = new Dictionary<string, object>(StringComparer.Ordinal);

        /// <summary>서버 함수 이름 또는 HTTP 경로. 로컬 백엔드도 이 이름으로 처리기를 고른다.</summary>
        public abstract string Name { get; }
        /// <summary>서버 공급자 키. <see cref="NetworkManager.SetServerProvider(string, INetworkProvider)"/>.</summary>
        public virtual string Target => NetworkTarget.Default;
        /// <summary><see cref="NetworkRoute.None"/>이면 <see cref="NetworkManager.DefaultRoute"/>를 따르고, 아니면 이 요청만 그 경로로 보낸다.</summary>
        public NetworkRoute Route { get; set; }
        /// <summary>요청 값. 보낼 때 <see cref="CreateDefaultParams"/> 기본값 위에 덮어쓴다.</summary>
        public IReadOnlyDictionary<string, object> Params => _params;
        /// <summary>이 응답이 바꾸는 저장소 키(반영 함수가 읽는다). 통신이 저장소를 참조하지 않도록 문자열로 둔다.</summary>
        public string[] DataKeys { get; protected set; } = Array.Empty<string>();
        /// <summary>성공 데이터를 병합하지 않고 교체한다.</summary>
        public bool ReplaceData { get; protected set; }

        public NetworkResult Result { get; private set; }
        public bool Completed { get; private set; }
        public bool IsPosting { get; private set; }
        public DateTime PostedAtUtc { get; private set; }

        public NetworkRequest Add(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("파라미터 키가 필요합니다.", nameof(key));
            if (IsPosting || Completed) throw new InvalidOperationException("보낸 뒤에는 파라미터를 바꿀 수 없습니다.");
            _params[key] = value;
            return this;
        }

        /// <summary>프로젝트 부모 요청의 공통 값(버전 등). 보낼 때 계산한다.</summary>
        protected virtual Dictionary<string, object> CreateDefaultParams() => new Dictionary<string, object>(StringComparer.Ordinal);

        internal Dictionary<string, object> BuildParameters()
        {
            Dictionary<string, object> merged = CreateDefaultParams() ?? new Dictionary<string, object>(StringComparer.Ordinal);
            var result = new Dictionary<string, object>(merged, StringComparer.Ordinal);
            foreach (var pair in _params) result[pair.Key] = pair.Value;
            return result;
        }

        internal void MarkPosting()
        {
            if (IsPosting || Completed) throw new InvalidOperationException("요청 인스턴스는 한 번만 보냅니다: " + Name);
            IsPosting = true;
            PostedAtUtc = DateTime.UtcNow;
        }

        internal void MarkCompleted(NetworkResult result)
        {
            Result = result;
            IsPosting = false;
            Completed = true;
        }

        object IEnumerator.Current => null;
        bool IEnumerator.MoveNext() => !Completed;
        void IEnumerator.Reset() { }

        public override string ToString() => GetType().Name + "(" + Name + ")";
    }
}
