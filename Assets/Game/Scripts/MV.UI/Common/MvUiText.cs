using System.Collections.Generic;
using System.Globalization;
using jjevol;
using MV.Data;

namespace MV.UI
{
    /// <summary>
    /// 문구 찾기(05 §5.1.12, 07 §7.12). <c>UIRuntimeOptions.Localize</c>(7단계가 LocalizationManager.GetLoc으로 연결)로 언어팩 값을 쓰고,
    /// 키가 없거나 비었으면 <c>fallback</c>(한국어 대체)을 쓴다. 새 문구는 키를 시트에 넣고 대체 문구는 ko와 같게 둔다(MV-LOC).
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

        /// <summary>화면 문구 <c>ui</c> 컬렉션(키 = <c>ui.&lt;화면&gt;.&lt;항목&gt;</c>). 없으면 <c>#키</c>(07 §7.12 「문구가 없으면 #키」).</summary>
        public static string Ui(string key) => Get("ui", key, "#" + key);

        /// <summary><see cref="Ui(string)"/> + 자리 표시 <c>{0}</c>… (InvariantCulture, 07 §7.12).</summary>
        public static string Ui(string key, params object[] args) => Format(Ui(key), args);

        /// <summary>토스트 문구 <c>toastMessage</c> 컬렉션(키 = <c>toastMessage.&lt;항목&gt;</c>). 없으면 <c>#키</c>.</summary>
        public static string Toast(string key, params object[] args) => Format(Get("toastMessage", key, "#" + key), args);

        /// <summary>런 팝업·긴 안내 <c>msg</c> 컬렉션(키 = <c>msg.&lt;항목&gt;</c>). 없으면 <c>#키</c>.</summary>
        public static string Msg(string key, params object[] args) => Format(Get("msg", key, "#" + key), args);

        static string Format(string text, object[] args)
        {
            if (args == null || args.Length == 0) return text;
            try { return string.Format(CultureInfo.InvariantCulture, text, args); }
            catch (System.FormatException) { return text; }
        }

        /// <summary>재화 이름을 <c>01_wealth</c> 행 id로: 번역 <c>name:&lt;id&gt;</c> → 표 <c>name</c> 열 → id.</summary>
        public static string WealthNameById(MvTables tables, int id)
        {
            string raw = tables != null ? RawName(tables.Wealth.RawRows, id) : null;
            return Name(id, raw ?? id.ToString(CultureInfo.InvariantCulture));
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
