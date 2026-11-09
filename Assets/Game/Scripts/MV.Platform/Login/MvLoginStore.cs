using System.Collections.Generic;
using UnityEngine;

namespace MV.Platform.Login
{
    /// <summary>로그인 상태(기기 게스트 id·마지막 방식·연동 방식)를 보존하는 주입 저장소.</summary>
    public interface IMvLoginStore
    {
        /// <summary>없으면 null.</summary>
        string Get(string key);
        void Set(string key, string value);
        void Delete(string key);
    }

    public static class MvLoginStoreKeys
    {
        public const string GuestId = "mv.login.guestId";
        public const string LastMethod = "mv.login.lastMethod";
        /// <summary>연동한 공급자 ID를 ','로 이은 값.</summary>
        public const string LinkedProviders = "mv.login.linked";
    }

    /// <summary>메모리 저장소. 시험과 저장을 남기지 않을 때 쓴다.</summary>
    public sealed class MvMemoryLoginStore : IMvLoginStore
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public string Get(string key) => _values.TryGetValue(key, out string value) ? value : null;
        public void Set(string key, string value) => _values[key] = value;
        public void Delete(string key) => _values.Remove(key);
    }

    /// <summary>기기 저장소(PlayerPrefs). 게임 실행용 기본값이다. 시험에서는 쓰지 않는다.</summary>
    public sealed class MvPlayerPrefsLoginStore : IMvLoginStore
    {
        public string Get(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;

        public void Set(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
