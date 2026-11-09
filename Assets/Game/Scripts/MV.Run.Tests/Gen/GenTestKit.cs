using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MV.Data;
using UnityEngine;

namespace MV.Run.Tests.Gen
{
    /// <summary>GEN 시험 공용: 표 로드, 저장소 덤프 경로, 작은 JSON 읽기.</summary>
    internal static class GenTestKit
    {
        static MvTables _tables;

        /// <summary>TSV 정본 표(파일은 바꾸지 않는다). 시험 사이에 공유한다(읽기 전용).</summary>
        public static MvTables Tables()
        {
            if (_tables != null) return _tables;
            var t = new MvTables();
            t.LoadAll(new MvTsvDirectorySource(MvTableLoader.TsvDirectory));
            t.BuildIndexes();
            _tables = t;
            return t;
        }

        /// <summary>저장소 루트 = Application.dataPath의 부모.</summary>
        public static string RepoRoot => Directory.GetParent(Application.dataPath).FullName;

        /// <summary>`QA/Dumps/gen/{name}`를 저장소 경로에서 직접 읽는다(복사하지 않는다).</summary>
        public static Dictionary<string, object> LoadDump(string name)
        {
            var path = Path.Combine(RepoRoot, "QA", "Dumps", "gen", name);
            if (!File.Exists(path)) throw new FileNotFoundException("대조 덤프가 없습니다", path);
            return (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(path, new UTF8Encoding(false)));
        }

        public static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;
        public static List<object> Arr(object o) => (List<object>)o;
        public static double Num(object o) => Convert.ToDouble(o, CultureInfo.InvariantCulture);
        public static int Int(object o) => (int)Num(o);

        public static string Show(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 시험 전용 최소 JSON 읽기(객체 → Dictionary, 배열 → List, 숫자 → double, true/false, null, 문자열).
    /// 숫자는 double.Parse(InvariantCulture)라 JS 최단 왕복 표기를 같은 double로 되돌린다.
    /// </summary>
    internal sealed class MiniJson
    {
        readonly string _s;
        int _i;

        MiniJson(string s) { _s = s; }

        public static object Parse(string text)
        {
            var p = new MiniJson(text);
            p.Ws();
            var v = p.Value();
            p.Ws();
            if (p._i != p._s.Length) throw p.Error("값 뒤에 남은 글자");
            return v;
        }

        FormatException Error(string what) => new FormatException($"JSON {what} (위치 {_i})");

        void Ws()
        {
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t') _i++;
                else break;
            }
        }

        object Value()
        {
            if (_i >= _s.Length) throw Error("끝");
            char c = _s[_i];
            switch (c)
            {
                case '{': return Object();
                case '[': return Array();
                case '"': return String();
                case 't': Expect("true"); return true;
                case 'f': Expect("false"); return false;
                case 'n': Expect("null"); return null;
                default: return Number();
            }
        }

        void Expect(string word)
        {
            if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error("알 수 없는 낱말");
            _i += word.Length;
        }

        Dictionary<string, object> Object()
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            _i++;
            Ws();
            if (_s[_i] == '}') { _i++; return d; }
            while (true)
            {
                Ws();
                var key = String();
                Ws();
                if (_s[_i++] != ':') throw Error("':' 없음");
                Ws();
                d[key] = Value();
                Ws();
                char c = _s[_i++];
                if (c == '}') return d;
                if (c != ',') throw Error("',' 없음");
            }
        }

        List<object> Array()
        {
            var list = new List<object>();
            _i++;
            Ws();
            if (_s[_i] == ']') { _i++; return list; }
            while (true)
            {
                Ws();
                list.Add(Value());
                Ws();
                char c = _s[_i++];
                if (c == ']') return list;
                if (c != ',') throw Error("',' 없음");
            }
        }

        string String()
        {
            if (_s[_i] != '"') throw Error("문자열 아님");
            _i++;
            StringBuilder sb = null;
            int start = _i;
            while (true)
            {
                char c = _s[_i];
                if (c == '"')
                {
                    string tail = _s.Substring(start, _i - start);
                    _i++;
                    return sb == null ? tail : sb.Append(tail).ToString();
                }
                if (c == '\\')
                {
                    sb = sb ?? new StringBuilder();
                    sb.Append(_s, start, _i - start);
                    char e = _s[_i + 1];
                    _i += 2;
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw Error("이스케이프");
                    }
                    start = _i;
                    continue;
                }
                _i++;
            }
        }

        double Number()
        {
            int start = _i;
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') _i++;
                else break;
            }
            if (_i == start) throw Error("숫자 아님");
            return double.Parse(_s.Substring(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
