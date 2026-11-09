using jjevol;

namespace MV.UI
{
    /// <summary>
    /// 문구 찾기(05 §5.1.12). 지금은 한국어 직접 문자열을 쓰고 키 자리를 남긴다.
    /// 교체 지점: <c>UIRuntimeOptions.Localize</c>가 LocalizationManager.GetLoc로 연결되면 키 값이 그대로 쓰인다.
    /// </summary>
    public static class MvUiText
    {
        /// <summary>번역이 연결돼 있고 키와 다른 값을 돌려주면 그 값, 아니면 <paramref name="fallback"/>.</summary>
        public static string Get(string collection, string key, string fallback)
        {
            if (string.IsNullOrEmpty(key) || UIRuntimeOptions.Localize == null) return fallback;
            string v = UIRuntimeOptions.GetLocalizedString(collection, key);
            return string.IsNullOrEmpty(v) || v == key ? fallback : v;
        }

        /// <summary>표 행 이름 <c>name:&lt;행 id&gt;</c>.</summary>
        public static string Name(int rowId, string fallback) => Get("name", rowId.ToString(), fallback);
    }
}
