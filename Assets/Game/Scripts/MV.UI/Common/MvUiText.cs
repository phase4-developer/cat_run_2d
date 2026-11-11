using System.Collections.Generic;
using jjevol;
using MV.Data;

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

        /// <summary>
        /// 재화 이름(<c>01_wealth</c>, MV-SPEC-02 TermIdMap): 번역 <c>name:&lt;id&gt;</c> → 표 <c>name</c> 열 → <c>type</c>.
        /// <paramref name="type"/>은 표 행 키(예: <c>gold</c>·<c>dia</c>·<c>rescueCat</c>·<c>goldenFish</c>).
        /// </summary>
        public static string WealthName(MvTables tables, string type)
        {
            int id = tables != null ? tables.Wealth.GetId(type) : 0;
            if (id == 0) return type ?? "";
            return Name(id, RawName(tables.Wealth.RawRows, id) ?? type);
        }

        /// <summary>원문 행의 <c>name</c> 열(번역 없을 때의 표 이름). 없으면 null.</summary>
        public static string RawName(IReadOnlyList<TSVRow> rows, int id)
        {
            if (rows == null) return null;
            foreach (var r in rows)
            {
                if (r == null || r.GetInt("id") != id) continue;
                string s = r.GetString("name");
                return string.IsNullOrEmpty(s) ? null : s;
            }
            return null;
        }
    }
}
