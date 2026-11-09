using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using jjevol;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MV.Save
{
    /// <summary>저장 열기 설정. 비운 칸은 출시 기본값을 쓴다.</summary>
    public sealed class MvSaveOptions
    {
        public string ProfileId { get; set; } = MvSaveSchema.ProfileId;
        public string KeyNamespace { get; set; } = MvSaveSchema.KeyNamespace;
        /// <summary>null이면 <see cref="MvSaveKeySource.ForCurrentBuild"/>.</summary>
        public IMvSaveKeyProvider KeyProvider { get; set; }
        /// <summary>null이면 Unity PlayerPrefs.</summary>
        public IMvSavePrefs Preferences { get; set; }
        /// <summary>null이면 정본 기본값(<see cref="MvSaveConfig.CreateDefault"/>).</summary>
        public MvSaveConfig Config { get; set; }
        /// <summary>null이면 UTC 현재 시각(ms).</summary>
        public Func<long> NowMs { get; set; }
        /// <summary>null이면 <see cref="MvSaveMigrator.CreateDefault"/>.</summary>
        public IEnumerable<IMvSaveMigration> Migrations { get; set; }
    }

    /// <summary>열기 결과(09 §9.7 상태표).</summary>
    public enum MvSaveOpenStatus
    {
        /// <summary>정상 로드.</summary>
        Opened,
        /// <summary>첫 실행. 기본값으로 새로 만들었다.</summary>
        Created,
        /// <summary>한 칸이 깨져 남은 칸으로 열었다. 깨진 칸은 지웠다(원문은 결과에 있다).</summary>
        RecoveredBackup,
        /// <summary>두 칸 다 읽을 수 없음 또는 컨테이너 값 손상. 자동으로 새로 만들지 않는다.</summary>
        Corrupt,
        /// <summary>무결성·복호화·키 실패(키가 없거나 다름).</summary>
        ProtectionFailed,
        /// <summary>더 새 앱이 쓴 저장. 건드리지 않는다.</summary>
        UnsupportedVersion,
        /// <summary>Framework 봉투 이관 실패.</summary>
        MigrationFailed,
        /// <summary>게임 스키마 이관 실패(체인 빈틈·예외). 원문은 그대로 둔다.</summary>
        GameMigrationFailed,
        /// <summary>PlayerPrefs 접근 불가·쓰기 불확실.</summary>
        StorageUnavailable,
    }

    /// <summary>열기 결과에 따라 화면이 할 일. 문구 키는 09 §9.7(<c>toastMessage.saveRecovered</c> 등).</summary>
    public enum MvSaveNotice
    {
        None,
        /// <summary>「이전 저장을 불러왔습니다」 토스트 1회.</summary>
        ToastRecovered,
        /// <summary>복구 팝업: 다시 시도(<see cref="MvSaveStore.Open"/>) / 새로 시작(<see cref="MvSaveStore.OpenFresh"/>, 두 번 확인).</summary>
        RecoveryPopup,
        /// <summary>「앱을 업데이트해 주세요」.</summary>
        UpdatePopup,
        /// <summary>「저장소를 사용할 수 없습니다」 + 다시 시도.</summary>
        StorageRetryPopup,
    }

    public sealed class MvSaveOpenResult
    {
        public MvSaveOpenStatus Status { get; }
        /// <summary>열렸으면 저장소, 아니면 null.</summary>
        public MvSaveStore Store { get; }
        public string Error { get; }
        /// <summary>게임 스키마 이관을 거쳐 열었는지.</summary>
        public bool Migrated { get; }
        /// <summary>열 때 읽은 스키마(새로 만들었으면 null).</summary>
        public string LoadedSchema { get; }
        /// <summary>복구로 지운 손상 칸, 또는 새로 시작으로 지운 이전 칸의 원문(Base64). 개발 빌드 내보내기용.</summary>
        public IReadOnlyList<string> RemovedSlotValues { get; }
        public bool IsOpen => Store != null;

        public MvSaveNotice Notice
        {
            get
            {
                switch (Status)
                {
                    case MvSaveOpenStatus.Opened:
                    case MvSaveOpenStatus.Created: return MvSaveNotice.None;
                    case MvSaveOpenStatus.RecoveredBackup: return MvSaveNotice.ToastRecovered;
                    case MvSaveOpenStatus.UnsupportedVersion: return MvSaveNotice.UpdatePopup;
                    case MvSaveOpenStatus.StorageUnavailable: return MvSaveNotice.StorageRetryPopup;
                    default: return MvSaveNotice.RecoveryPopup;
                }
            }
        }

        internal MvSaveOpenResult(MvSaveOpenStatus status, MvSaveStore store, string error, bool migrated, string loadedSchema, IReadOnlyList<string> removed)
        {
            Status = status; Store = store; Error = error; Migrated = migrated; LoadedSchema = loadedSchema;
            RemovedSlotValues = removed ?? Array.AsReadOnly(Array.Empty<string>());
        }
    }

    /// <summary>
    /// MoonVillage 저장소(09). Framework <c>LocalSaveRepository</c>(PlayerPrefs 두 칸) · <c>DataRepository</c>(컨테이너) ·
    /// <c>RepositoryStorageAdapter</c>(<c>LocalAuthority</c>) 위에 컨테이너 묶음, 이관, 손상 처리, 진행 초기화를 올린다.
    /// 메인 스레드에서만 쓴다. 상태를 바꾸는 판정(정산·구매·보상)은 MV.Meta 몫이고 여기는 보관·직렬화·이관만 한다.
    /// </summary>
    public sealed class MvSaveStore : IDisposable
    {
        private readonly MvSaveOptions _options;
        private readonly MvSaveConfig _config;
        private readonly Func<long> _now;
        private RepositoryStorageAdapter _storage;

        public DataRepository Repository { get; }
        public RepositoryStorageAdapter Storage => _storage;

        public WealthContainer Wealth { get; }
        public MvProgressContainer Progress { get; }
        public MvLobbyContainer Lobby { get; }
        public MvAreaUpContainer AreaUp { get; }
        public MvRebirthContainer Rebirth { get; }
        public MvGodContainer God { get; }
        public MvUnlockContainer Unlock { get; }
        public MvDailyContainer Daily { get; }
        public MvPlayResContainer PlayRes { get; }
        public MvModeContainer Mode { get; }
        public MvQuestContainer Quest { get; }
        public MvResearchContainer Research { get; }
        public MvShopContainer Shop { get; }
        public MvPetContainer Pet { get; }
        public MvFriendContainer Friend { get; }
        public MvGearContainer Gear { get; }
        public MvPassContainer Pass { get; }
        public MvBlessingContainer Blessing { get; }
        public MvSettingsContainer Settings { get; }
        public MvMetaContainer Meta { get; }

        public bool IsDisposed => Repository.IsDisposed;
        public bool IsDirty => _storage.IsDirty;
        /// <summary>쓰기 실패·충돌 뒤라 더 쓸 수 없다. <see cref="Reopen"/>으로 다시 연다(09 §9.7 쓰기 실패).</summary>
        public bool RequiresReopen => _storage.Status != RepositoryStorageStatus.Ready;

        private MvSaveStore(MvSaveOptions options, MvSaveConfig config, Func<long> now)
        {
            _options = options; _config = config; _now = now;
            Repository = new DataRepository(options.ProfileId);
            try
            {
                Wealth = Add(new WealthContainer());
                Progress = Add(new MvProgressContainer(config));
                Lobby = Add(new MvLobbyContainer(config));
                AreaUp = Add(new MvAreaUpContainer());
                Rebirth = Add(new MvRebirthContainer());
                God = Add(new MvGodContainer(config));
                Unlock = Add(new MvUnlockContainer());
                Daily = Add(new MvDailyContainer());
                PlayRes = Add(new MvPlayResContainer());
                Mode = Add(new MvModeContainer());
                Quest = Add(new MvQuestContainer());
                Research = Add(new MvResearchContainer());
                Shop = Add(new MvShopContainer());
                Pet = Add(new MvPetContainer());
                Friend = Add(new MvFriendContainer());
                Gear = Add(new MvGearContainer());
                Pass = Add(new MvPassContainer());
                Blessing = Add(new MvBlessingContainer());
                Settings = Add(new MvSettingsContainer());
                Meta = Add(new MvMetaContainer(config));
            }
            catch { Repository.Dispose(); throw; }
        }

        private T Add<T>(T container) where T : DataContainer
        {
            Repository.Register(container, saveLocal: true);
            return container;
        }

        // ───────── 열기(09 §9.5.2) ─────────

        /// <summary>
        /// 저장을 연다: 저장소 → (한 칸 손상이면 남은 칸으로 복구) → 게임 스키마 이관 → 컨테이너 등록 → Open.
        /// 같은 프로필의 저장소가 이미 열려 있으면 예외(먼저 Dispose).
        /// </summary>
        public static MvSaveOpenResult Open(MvSaveOptions options = null) => Open(options, null);

        /// <summary>
        /// 복구 팝업 「새로 시작」(두 번 확인 뒤): 두 칸을 지우고 기본값으로 새로 만든다. 지운 원문은 결과에 남긴다.
        /// </summary>
        public static MvSaveOpenResult OpenFresh(MvSaveOptions options = null)
        {
            options ??= new MvSaveOptions();
            IMvSavePrefs prefs = options.Preferences ?? new MvPlayerPrefs();
            var slots = new LocalSaveRepository(options.ProfileId, new LocalSaveCodec(), options.KeyNamespace, prefs);
            var removed = new List<string>();
            foreach (string key in new[] { slots.Slot0Key, slots.Slot1Key })
            {
                if (!prefs.HasKey(key)) continue;
                removed.Add(prefs.GetString(key));
                prefs.DeleteKey(key);
            }
            prefs.Save();
            return Open(options, removed);
        }

        private static MvSaveOpenResult Open(MvSaveOptions options, IReadOnlyList<string> removedBefore)
        {
            options ??= new MvSaveOptions();
            MvSaveConfig config = options.Config ?? MvSaveConfig.CreateDefault();
            config.Validate();
            Func<long> now = options.NowMs ?? MvSaveTime.UtcNowMs;
            IMvSavePrefs prefs = options.Preferences ?? new MvPlayerPrefs();
            List<string> removed = removedBefore?.ToList() ?? new List<string>();

            LocalSaveCodec codec;
            try { codec = MvSaveKeySource.CreateCodec(options.KeyProvider ?? MvSaveKeySource.ForCurrentBuild()); }
            catch (Exception exception) when (exception is MvSaveKeyMissingException || exception is ArgumentException)
            { return Fail(MvSaveOpenStatus.ProtectionFailed, exception.Message, removed); }

            var files = new LocalSaveRepository(options.ProfileId, codec, options.KeyNamespace, prefs);
            LocalSaveLoadResult loaded = files.Load();
            bool recovered = false;

            if (loaded.Status == LocalSaveLoadStatus.Corrupt)
            {
                // 한 칸만 깨졌으면 남은 칸을 읽는다(LoadRecovery는 읽기 전용). 이어서 쓰려면 깨진 칸을 지워야 한다 —
                // 09 §9.7 RecoveredBackup 「계속」이 그 승인이다. 지운 원문은 결과에 남긴다.
                LocalSaveLoadResult salvage = files.LoadRecovery();
                if (salvage.Status != LocalSaveLoadStatus.RecoveredBackup) return Fail(MvSaveOpenStatus.Corrupt, loaded.Error, removed);
                List<string> damaged = DamagedSlots(prefs, codec, files);
                if (damaged.Count == 0) return Fail(MvSaveOpenStatus.Corrupt, loaded.Error, removed);
                foreach (string key in damaged)
                {
                    removed.Add(prefs.GetString(key));
                    prefs.DeleteKey(key);
                }
                prefs.Save();
                loaded = files.Load();
                if (!loaded.Success) return Fail(MvSaveOpenStatus.Corrupt, loaded.Error, removed);
                recovered = true;
            }

            switch (loaded.Status)
            {
                case LocalSaveLoadStatus.Missing:
                case LocalSaveLoadStatus.Loaded:
                case LocalSaveLoadStatus.RecoveredBackup:
                    break;
                case LocalSaveLoadStatus.UnsupportedVersion: return Fail(MvSaveOpenStatus.UnsupportedVersion, loaded.Error, removed);
                case LocalSaveLoadStatus.MigrationFailed: return Fail(MvSaveOpenStatus.MigrationFailed, loaded.Error, removed);
                case LocalSaveLoadStatus.ProtectionFailed: return Fail(MvSaveOpenStatus.ProtectionFailed, loaded.Error, removed);
                case LocalSaveLoadStatus.StorageUnavailable: return Fail(MvSaveOpenStatus.StorageUnavailable, loaded.Error, removed);
                default: return Fail(MvSaveOpenStatus.Corrupt, loaded.Error, removed);
            }

            // 게임 스키마 이관은 Open 앞이다(Adapter는 스키마가 다르면 열지 않는다).
            string loadedSchema = null;
            bool migrated = false;
            if (loaded.Success)
            {
                JObject payload = loaded.Document.Snapshot.Payload;
                loadedSchema = payload[MvSaveSchema.SchemaKey]?.Type == JTokenType.String ? payload.Value<string>(MvSaveSchema.SchemaKey) : null;
                if (loadedSchema != MvSaveSchema.Current)
                {
                    JObject next;
                    try
                    {
                        MvSaveMigrator migrator = options.Migrations != null ? new MvSaveMigrator(options.Migrations) : MvSaveMigrator.CreateDefault(config, now);
                        next = migrator.Run(payload, loadedSchema);
                    }
                    catch (Exception exception) when (exception is MvSaveMigrationException || exception is ArgumentException)
                    { return Fail(MvSaveOpenStatus.GameMigrationFailed, exception.Message, removed, loadedSchema); }
                    try { files.Save(new LocalSaveSnapshot(next), loaded.Document); }
                    catch (LocalSaveWriteException exception) { return Fail(MvSaveOpenStatus.StorageUnavailable, exception.Message, removed, loadedSchema); }
                    catch (Exception exception) when (exception is InvalidDataException || exception is LocalSaveConflictException || exception is IOException)
                    { return Fail(MvSaveOpenStatus.GameMigrationFailed, exception.Message, removed, loadedSchema); }
                    migrated = true;
                }
            }

            var store = new MvSaveStore(options, config, now);
            try
            {
                store._storage = RepositoryStorageAdapter.Open(store.Repository, files, MvSaveSchema.Current,
                    RepositoryStorageMode.LocalAuthority, () => MvSaveDefaults.Create(config, now()));
            }
            catch (Exception exception)
            {
                store.Repository.Dispose();
                MvSaveOpenStatus status = exception is LocalSaveWriteException || exception is LocalSaveProtectionException
                    ? MvSaveOpenStatus.StorageUnavailable : MvSaveOpenStatus.Corrupt;
                return Fail(status, exception.Message, removed, loadedSchema);
            }

            MvSaveOpenStatus opened = loaded.Status == LocalSaveLoadStatus.Missing ? MvSaveOpenStatus.Created
                : recovered ? MvSaveOpenStatus.RecoveredBackup : MvSaveOpenStatus.Opened;
            if (recovered) Debug.LogWarning("[MvSave] 손상된 저장 칸을 지우고 남은 칸으로 열었습니다.");
            return new MvSaveOpenResult(opened, store, null, migrated, loadedSchema, removed);
        }

        private static MvSaveOpenResult Fail(MvSaveOpenStatus status, string error, IReadOnlyList<string> removed, string schema = null)
        {
            Debug.LogWarning("[MvSave] 저장을 열지 못했습니다: " + status + " " + error);
            return new MvSaveOpenResult(status, null, error, false, schema, removed);
        }

        private static List<string> DamagedSlots(IMvSavePrefs prefs, LocalSaveCodec codec, LocalSaveRepository files)
        {
            var damaged = new List<string>();
            foreach (string key in new[] { files.Slot0Key, files.Slot1Key })
            {
                if (!prefs.HasKey(key)) continue;
                try { codec.Decode(Convert.FromBase64String(prefs.GetString(key)), files.ProfileId); }
                catch (Exception) { damaged.Add(key); }
            }
            return damaged;
        }

        /// <summary>쓰기 실패·충돌 뒤 다시 연다(진행 중이던 사건은 저장된 값 기준으로 다시 판단한다).</summary>
        public MvSaveOpenResult Reopen()
        {
            Dispose();
            return Open(_options);
        }

        public void Dispose()
        {
            if (!Repository.IsDisposed) Repository.Dispose();
        }

        // ───────── 저장(09 §9.4) ─────────

        /// <summary>한 사건의 변경을 한 번의 원자 저장으로(병합). 키는 <see cref="MvSaveTriggers"/>. <c>mvMeta.lastSaveAt</c>을 함께 올린다.</summary>
        public bool ApplyAndSave(JObject data, params string[] keys) => ApplyAndSave(data, false, keys);

        /// <summary>
        /// 저장 확인 → 메모리 반영 → 알림 순서(Framework). 하나라도 해석에 실패하면 아무것도 바뀌지 않는다.
        /// <c>LocalSaveWriteException</c>·<c>RepositoryRecoveryRequiredException</c>이면 같은 변경을 다시 시도하지 말고 <see cref="Reopen"/>.
        /// </summary>
        public bool ApplyAndSave(JObject data, bool replace, params string[] keys)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (keys == null || keys.Length == 0) throw new ArgumentException("저장할 컨테이너 키가 필요합니다.", nameof(keys));
            var payload = (JObject)data.DeepClone();
            string[] targets = keys.Contains(MvSaveKeys.Meta, StringComparer.Ordinal) ? keys.ToArray() : keys.Concat(new[] { MvSaveKeys.Meta }).ToArray();
            JObject meta = payload[MvSaveKeys.Meta] as JObject;
            if (meta == null) meta = replace ? MvSaveJson.ParseObject(Meta.ToJsonString()) : new JObject();
            else meta = (JObject)meta.DeepClone();
            meta["lastSaveAt"] = _now();
            payload[MvSaveKeys.Meta] = meta;
            return _storage.ApplyAndSave(payload, replace, targets);
        }

        /// <summary>문자열 JSON 판(개발 도구·시험).</summary>
        public bool ApplyAndSaveJson(string json, bool replace, params string[] keys) => ApplyAndSave(MvSaveJson.ParseObject(json), replace, keys);

        /// <summary>앱 일시정지·종료: 메모리만 바뀐 것이 있을 때만 저장한다(평소엔 사건마다 저장하므로 할 일 없음).</summary>
        public bool SaveIfDirty()
        {
            if (!_storage.IsDirty) return false;
            ApplyAndSave(new JObject(), MvSaveKeys.Meta);
            return true;
        }

        public long GetWealth(int wealthId)
        {
            string type = MvWealthIds.TypeOf(wealthId) ?? throw new ArgumentException("모르는 재화 id: " + wealthId, nameof(wealthId));
            return Wealth.Get(type);
        }

        /// <summary>등록한 모든 컨테이너 값(저장 묶음과 같은 모양, 스키마 칸 제외).</summary>
        public string ExportPayloadJson() => MvSaveJson.ToJson(Repository.ExportJson());

        // ───────── 개발 빌드 도구 ─────────

        /// <summary>진행 초기화(D-69, 개발 빌드 테스트 패널 전용). <c>mvSettings</c>만 남기고 기본값, <c>createdAt</c>은 새로.</summary>
        public void DevResetProgress()
        {
            EnsureDevelopmentBuild("진행 초기화(D-69)");
            JObject defaults = MvSaveDefaults.Create(_config, _now());
            var data = new JObject();
            foreach (string key in MvSaveKeys.ProgressResetKeys) data[key] = defaults[key];
            ApplyAndSave(data, true, MvSaveKeys.ProgressResetKeys.ToArray());
        }

        /// <summary>프로토 저장 붙여넣기(D-67, 개발 빌드 전용, 09 §9.9). 소리 설정은 건드리지 않는다.</summary>
        public void DevImportProtoSave(string protoJson)
        {
            EnsureDevelopmentBuild("프로토 저장 가져오기(D-67)");
            JObject converted = MvProtoSave.ToPayload(MvSaveJson.ParseObject(protoJson), _config, _now());
            var data = new JObject();
            foreach (string key in MvSaveKeys.ProtoImportKeys) data[key] = converted[key];
            ApplyAndSave(data, true, MvSaveKeys.ProtoImportKeys.ToArray());
        }

        private static void EnsureDevelopmentBuild(string what)
        {
            if (!Debug.isDebugBuild) throw new InvalidOperationException(what + "은 개발 빌드에서만 쓸 수 있습니다.");
        }

        /// <summary>시험 전용: 저장 칸에 원문 문서를 직접 쓴다(이관 시험의 이전 스키마 문서).</summary>
        internal static void WriteDocumentForTests(MvSaveOptions options, string payloadJson)
        {
            IMvSavePrefs prefs = options.Preferences ?? throw new ArgumentException("시험은 메모리 저장소를 넣어야 합니다.");
            var files = new LocalSaveRepository(options.ProfileId, MvSaveKeySource.CreateCodec(options.KeyProvider), options.KeyNamespace, prefs);
            LocalSaveLoadResult current = files.Load();
            files.Save(new LocalSaveSnapshot(MvSaveJson.ParseObject(payloadJson)), current.Success ? current.Document : null);
        }
    }
}
