using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using R = MV.Save.MvJsonRead;

namespace MV.Save
{
    /// <summary>게임 스키마 이관 1고리(09 §9.5.3). 새 객체를 돌려주고 실패는 예외로 알린다.</summary>
    public interface IMvSaveMigration
    {
        string FromSchema { get; }
        string ToSchema { get; }
        JObject Apply(JObject payload);
    }

    public sealed class MvSaveMigrationException : Exception
    {
        public MvSaveMigrationException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// from → … → 현재 스키마까지 순서대로 적용한다. 빠진 고리·순환·고리 예외는 <see cref="MvSaveMigrationException"/>.
    /// Framework <c>RepositoryStorageAdapter</c>는 스키마가 다르면 열지 않으므로 이관은 Open 앞에서 돈다.
    /// 필드 추가만 하는 변경은 스키마를 올리지 않는다(관대한 읽기로 충분).
    /// </summary>
    public sealed class MvSaveMigrator
    {
        private readonly Dictionary<string, IMvSaveMigration> _byFrom = new Dictionary<string, IMvSaveMigration>(StringComparer.Ordinal);
        public string TargetSchema { get; }

        public MvSaveMigrator(IEnumerable<IMvSaveMigration> migrations, string targetSchema = MvSaveSchema.Current)
        {
            if (string.IsNullOrWhiteSpace(targetSchema)) throw new ArgumentException("목표 스키마가 필요합니다.", nameof(targetSchema));
            TargetSchema = targetSchema;
            foreach (IMvSaveMigration migration in migrations ?? Enumerable.Empty<IMvSaveMigration>())
            {
                if (migration == null || string.IsNullOrWhiteSpace(migration.FromSchema) || string.IsNullOrWhiteSpace(migration.ToSchema) || migration.FromSchema == migration.ToSchema)
                    throw new ArgumentException("이관 고리의 from·to가 잘못됐습니다.", nameof(migrations));
                if (_byFrom.ContainsKey(migration.FromSchema)) throw new ArgumentException("같은 from 스키마의 이관이 둘입니다: " + migration.FromSchema, nameof(migrations));
                _byFrom.Add(migration.FromSchema, migration);
            }
        }

        /// <summary>MoonVillage 기본 체인. v0(정본 프로토 저장) → v1.</summary>
        public static MvSaveMigrator CreateDefault(MvSaveConfig config, Func<long> nowMs)
            => new MvSaveMigrator(new IMvSaveMigration[] { new MvSaveMigrationV0ToV1(config, nowMs) });

        /// <summary>from 스키마에서 목표까지 고리가 이어지는지.</summary>
        public bool HasPath(string fromSchema)
        {
            string schema = fromSchema;
            for (int step = 0; step <= _byFrom.Count; step++)
            {
                if (schema == TargetSchema) return true;
                if (schema == null || !_byFrom.TryGetValue(schema, out IMvSaveMigration migration)) return false;
                schema = migration.ToSchema;
            }
            return false;
        }

        public JObject Run(JObject payload, string fromSchema)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            var current = (JObject)payload.DeepClone();
            string schema = fromSchema;
            int steps = 0;
            while (schema != TargetSchema)
            {
                if (schema == null || !_byFrom.TryGetValue(schema, out IMvSaveMigration migration))
                    throw new MvSaveMigrationException("이관 고리가 없습니다: " + (schema ?? "(스키마 없음)") + " → " + TargetSchema);
                if (++steps > _byFrom.Count) throw new MvSaveMigrationException("이관 체인이 순환합니다: " + schema);
                JObject next;
                try { next = migration.Apply((JObject)current.DeepClone()); }
                catch (Exception exception) { throw new MvSaveMigrationException("이관 실패: " + migration.FromSchema + " → " + migration.ToSchema, exception); }
                if (next == null) throw new MvSaveMigrationException("이관 결과가 없습니다: " + migration.FromSchema);
                next[MvSaveSchema.SchemaKey] = migration.ToSchema;
                current = next;
                schema = migration.ToSchema;
            }
            current[MvSaveSchema.SchemaKey] = TargetSchema;
            return current;
        }
    }

    /// <summary>v0(정본 <c>catescape_moon_v1</c> 평면 JSON) → v1 컨테이너 묶음. 변환은 <see cref="MvProtoSave"/>(D-67).</summary>
    public sealed class MvSaveMigrationV0ToV1 : IMvSaveMigration
    {
        private readonly MvSaveConfig _config;
        private readonly Func<long> _nowMs;
        public string FromSchema => MvSaveSchema.V0;
        public string ToSchema => MvSaveSchema.V1;

        public MvSaveMigrationV0ToV1(MvSaveConfig config, Func<long> nowMs)
        {
            _config = config ?? MvSaveConfig.CreateDefault();
            _nowMs = nowMs ?? MvSaveTime.UtcNowMs;
        }

        public JObject Apply(JObject payload)
        {
            var proto = (JObject)payload.DeepClone();
            proto.Remove(MvSaveSchema.SchemaKey);
            return MvProtoSave.ToPayload(proto, _config, _nowMs());
        }
    }

    /// <summary>
    /// 정본(웹 프로토) 저장 → v1 컨테이너 JSON 변환(D-67 개발 빌드 붙여넣기 도구의 변환 함수, 09 §9.9).
    /// 정본 <c>load()</c>(L846)와 같은 기본값 합치기를 먼저 한다: 값이 없거나 <c>up</c>이 없으면 <c>DEF()</c>.
    /// </summary>
    public static class MvProtoSave
    {
        public const string ImportSource = "proto-paste";
        // 정본 DEF().ads의 남은 횟수.
        private const int ProtoDefAdsDia = 3;
        private const int ProtoDefAdsSkip = 4;

        public static string ToPayloadJson(string protoJson, MvSaveConfig config, long nowMs)
            => MvSaveJson.ToJson(ToPayload(MvSaveJson.ParseObject(protoJson), config, nowMs));

        public static JObject ToPayload(JObject proto, MvSaveConfig config, long nowMs)
        {
            config ??= MvSaveConfig.CreateDefault();
            config.Validate();
            JObject s = proto != null && R.Truthy(proto["up"]) ? proto : new JObject();
            JObject rb = s["rb"] as JObject ?? new JObject();
            JObject ads = s["ads"] as JObject ?? new JObject();
            var payload = new JObject();

            // gold는 floor(관대한 읽기가 소수를 내림한다).
            payload[MvSaveKeys.Wealth] = MvSaveDefaults.WealthRows(new Dictionary<int, long>
            {
                [MvWealthIds.Gold] = R.Long(s["gold"], 0),
                [MvWealthIds.Souls] = R.Long(s["souls"], 0),
                [MvWealthIds.Dia] = R.Long(s["dia"], 0),
                [MvWealthIds.RbToken] = R.Long(rb["tokens"], 0),
            });

            payload[MvSaveKeys.Progress] = MvSaveNormalize.Progress(new JObject
            {
                ["level"] = Copy(s["level"]),
                ["area"] = Copy(s["area"]),
                ["cp"] = Copy(s["cp"]),
                ["bars"] = Copy(s["bars"]),
                ["runs"] = Copy(s["runs"]),
                ["zoneClear"] = Copy(s["zoneClear"]),
                ["escaped"] = Copy(s["escaped"]),
                ["bestArea"] = Copy(s["area"]),
            }, config);

            // 정본: deck은 길이 9 배열일 때만, 아니면 DEF().deck.
            JToken deck = s["deck"] is JArray protoDeck && protoDeck.Count == 9 ? protoDeck : null;
            payload[MvSaveKeys.Lobby] = MvSaveNormalize.Lobby(new JObject { ["up"] = Copy(s["up"]), ["deck"] = Copy(deck) }, config);
            payload[MvSaveKeys.AreaUp] = MvSaveNormalize.AreaUp(new JObject { ["lv"] = Copy(s["aup"]) });
            payload[MvSaveKeys.Rebirth] = MvSaveNormalize.Rebirth(new JObject
            {
                ["lv"] = Copy(rb["lv"]),
                ["pending"] = Copy(rb["pending"]),
                ["perks"] = Copy(rb["perks"]),
                ["cdUntil"] = Copy(rb["cdUntil"]),
            });
            payload[MvSaveKeys.God] = MvSaveNormalize.God(new JObject { ["sel"] = Copy(s["god"]) }, config);

            // rb.open → 부활 해금(sticky), 해금 연출은 이미 본 것으로 본다(09 §9.3.3).
            var opened = new JArray();
            if (R.Truthy(rb["open"])) opened.Add("rebirth");
            payload[MvSaveKeys.Unlock] = MvSaveNormalize.Unlock(new JObject { ["opened"] = opened, ["seenUnlock"] = opened.DeepClone() });

            // 정본 ads는 「남은 수」, Unity는 「사용 수」. 그 날짜가 오늘일 때만 used = max(0, 한도 − 남은 수), 아니면 버린다.
            string today = MvSaveTime.DayKey(nowMs, config.DayUtcOffsetHour);
            var daily = new JObject();
            if (R.Str(ads["day"], "") == today)
            {
                daily["day"] = today;
                daily["ads"] = new JObject
                {
                    ["dia"] = Math.Max(0, config.AdDailyLimit("dia") - R.Int(ads["dia"], ProtoDefAdsDia, int.MinValue)),
                    ["skip"] = Math.Max(0, config.AdDailyLimit("skip") - R.Int(ads["skip"], ProtoDefAdsSkip, int.MinValue)),
                };
            }
            payload[MvSaveKeys.Daily] = MvSaveNormalize.Daily(daily);

            foreach (string key in MvSaveKeys.All)
                if (!payload.ContainsKey(key)) payload[key] = MvSaveNormalize.For(key, config)(new JObject());

            payload[MvSaveKeys.Meta] = MvSaveNormalize.Meta(new JObject
            {
                ["createdAt"] = nowMs,
                ["lastSaveAt"] = nowMs,
                ["importSource"] = ImportSource,
                ["tableVersion"] = config.TableVersion,
            }, config);
            // zone은 버린다(다시 계산). ce_mute는 대상이 아니다.
            return payload;
        }

        private static JToken Copy(JToken token) => token == null ? JValue.CreateNull() : token.DeepClone();
    }
}
