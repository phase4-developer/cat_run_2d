using System;
using System.Collections.Generic;

namespace MV.Data
{
    /// <summary>열 타입(기획서 07 §7.1.2). 검증기가 원문 문자열을 이 타입으로 다시 읽는다.</summary>
    public enum MvColKind
    {
        Int,
        Long,
        Float,
        Bool01,
        Str,
        /// <summary>표 안 유일, 영문·숫자·`.`·`_`.</summary>
        Key,
        Enum,
        Color,
        ColorList,
        IntList,
        FloatList,
        StrList,
        /// <summary>다른 표 id(0·빈칸 = 없음).</summary>
        Fk,
        /// <summary>다른 표 id 목록(`/`).</summary>
        FkList,
        /// <summary>다른 표의 묶음 번호(그 표 schema.GroupColumn).</summary>
        FkGroup,
        /// <summary>다른 표 key.</summary>
        FkKey,
        /// <summary>`03_sound.key`.</summary>
        Sound,
        /// <summary>`04_effect.key`.</summary>
        Effect,
        /// <summary>리소스 이름·주소(07 §7.10). 카탈로그가 있을 때만 확인.</summary>
        Res,
        /// <summary>`300_runConfig` 키 이름(`hitstop.*`, `shake.*` 앞부분).</summary>
        RunConfigKey,
    }

    public sealed class MvColumn
    {
        public string Name { get; internal set; }
        public MvColKind Kind { get; internal set; }
        public bool Required { get; internal set; }
        public Type EnumType { get; internal set; }
        /// <summary>Fk 계열 대상 표 파일 이름.</summary>
        public string Target { get; internal set; }
        /// <summary>Res 열의 기본 아틀라스(07 §7.10.3). `@theme` = 모든 테마 아틀라스.</summary>
        public string Atlas { get; internal set; }
        /// <summary>RunConfigKey 열이 `.amp`/`.time`을 붙여 찾는 앞부분인지.</summary>
        public bool Prefix { get; internal set; }
    }

    /// <summary>consumeData 열 묶음 정의(07 §7.1.6~7.1.7).</summary>
    public sealed class MvConsumeColumns
    {
        public string Prefix { get; internal set; }
        /// <summary>`{p}ValuePerZone`·`{p}ValueRule` 확장 열.</summary>
        public bool RunExtension { get; internal set; }
        /// <summary>`{p}Value` 대신 `{p}Value_min`·`{p}Value_max`(801·802).</summary>
        public bool Range { get; internal set; }
    }

    /// <summary>표 1개의 열 정의. 검증기와 시험이 쓴다.</summary>
    public sealed class MvSchema
    {
        readonly List<MvColumn> _columns = new List<MvColumn>();
        readonly List<MvConsumeColumns> _consumes = new List<MvConsumeColumns>();

        public string Table { get; }
        /// <summary>표 번호 문자열(`105`). 작은 정의 표도 앞자리를 맞춘다(D-80 권장안).</summary>
        public string Number { get; }
        public string IdColumn { get; private set; } = "id";
        /// <summary>id 접두 규칙을 쓰지 않는 표(`00_config` 1행, `07_consumeType`).</summary>
        public bool FreeIds { get; private set; }
        public bool SingleRow { get; private set; }
        public string GroupColumn { get; private set; }
        /// <summary>600번대 consumeType을 보상 열에 쓸 수 있는 런 표(07 §7.13.2).</summary>
        public bool AllowRunConsume { get; private set; }

        public IReadOnlyList<MvColumn> Columns => _columns;
        public IReadOnlyList<MvConsumeColumns> Consumes => _consumes;

        public MvSchema(string table)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            int underscore = table.IndexOf('_');
            Number = underscore > 0 ? table.Substring(0, underscore) : table;
        }

        public MvColumn Find(string name)
        {
            foreach (var column in _columns)
                if (column.Name == name) return column;
            return null;
        }

        /// <summary>id 접두(`105` → `105…`, `01` → `1…`).</summary>
        public string IdPrefix => int.TryParse(Number, out int n) ? n.ToString() : Number;

        // ---- builder ----

        MvSchema Add(string name, MvColKind kind, bool required, Type enumType = null, string target = null, string atlas = null, bool prefix = false)
        {
            if (Find(name) != null) throw new ArgumentException($"{Table}: duplicate column {name}");
            _columns.Add(new MvColumn { Name = name, Kind = kind, Required = required, EnumType = enumType, Target = target, Atlas = atlas, Prefix = prefix });
            return this;
        }

        public MvSchema Id(string column = "id")
        {
            IdColumn = column;
            return Add(column, MvColKind.Int, true);
        }

        public MvSchema FreeIdRule() { FreeIds = true; return this; }
        public MvSchema OneRow() { SingleRow = true; return this; }
        /// <summary>key 유일 범위가 group 안(110_card 슬롯 키, 07 §7.7.3).</summary>
        public bool KeyPerGroup { get; private set; }
        public MvSchema KeyUniqueInGroup() { KeyPerGroup = true; return this; }
        public MvSchema RunConsume() { AllowRunConsume = true; return this; }
        public MvSchema Group(string column = "group") { GroupColumn = column; return Add(column, MvColKind.Int, true); }

        public MvSchema Int(string name, bool required = false) => Add(name, MvColKind.Int, required);
        public MvSchema Long(string name, bool required = false) => Add(name, MvColKind.Long, required);
        public MvSchema Float(string name, bool required = false) => Add(name, MvColKind.Float, required);
        public MvSchema Bool(string name, bool required = false) => Add(name, MvColKind.Bool01, required);
        public MvSchema Str(string name, bool required = false) => Add(name, MvColKind.Str, required);
        public MvSchema Key(string name = "key", bool required = true) => Add(name, MvColKind.Key, required);
        public MvSchema Enum<T>(string name, bool required = false) where T : struct, System.Enum => Add(name, MvColKind.Enum, required, typeof(T));
        public MvSchema Color(string name, bool required = false) => Add(name, MvColKind.Color, required);
        public MvSchema Colors(string name, bool required = false) => Add(name, MvColKind.ColorList, required);
        public MvSchema Ints(string name, bool required = false) => Add(name, MvColKind.IntList, required);
        public MvSchema Floats(string name, bool required = false) => Add(name, MvColKind.FloatList, required);
        public MvSchema Strs(string name, bool required = false) => Add(name, MvColKind.StrList, required);
        public MvSchema Fk(string name, string table, bool required = false) => Add(name, MvColKind.Fk, required, target: table);
        public MvSchema FkList(string name, string table, bool required = false) => Add(name, MvColKind.FkList, required, target: table);
        public MvSchema FkGroup(string name, string table, bool required = false) => Add(name, MvColKind.FkGroup, required, target: table);
        public MvSchema FkKey(string name, string table, bool required = false) => Add(name, MvColKind.FkKey, required, target: table);
        public MvSchema Sound(string name) => Add(name, MvColKind.Sound, false);
        public MvSchema Effect(string name) => Add(name, MvColKind.Effect, false);
        public MvSchema Res(string name, string atlas, bool required = false) => Add(name, MvColKind.Res, required, atlas: atlas);
        public MvSchema ConfigKey(string name, bool prefix = false) => Add(name, MvColKind.RunConfigKey, false, prefix: prefix);

        public MvSchema Consume(string prefix, bool runExtension = false, bool range = false)
        {
            _consumes.Add(new MvConsumeColumns { Prefix = prefix, RunExtension = runExtension, Range = range });
            return this;
        }
    }
}
