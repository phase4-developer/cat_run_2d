using System;
using System.Collections.Generic;
using System.Linq;
using jjevol;
using MV.Data;
using MV.Save;
using UnityEngine;

namespace MV.Boot
{
    /// <summary>
    /// 부팅 5·6단계(10 §10.7, 09 §9.5.2): 표 값으로 저장 설정·참조 목록을 만들어 <see cref="MvSave.Init"/>을 부르고,
    /// 열린 저장소에 재화 서비스(<see cref="ResourceService"/>)를 묶는다.
    /// 저장 API는 동기다. 저장이 열리지 않아도 시작은 멈추지 않는다 — 화면(MV-UI)이 <see cref="PendingNotice"/>를 읽어 팝업을 띄운다.
    /// </summary>
    public static class MvBootSave
    {
        /// <summary>07 <c>07_consumeType</c> 1 = 재화(Wealth).</summary>
        public static readonly ConsumeTypeId WealthTypeId = new ConsumeTypeId(1);

        /// <summary>마지막 Boot 저장 열기 결과.</summary>
        public static MvSaveOpenResult LastOpen { get; private set; }

        /// <summary>
        /// 화면이 아직 처리하지 않은 저장 안내(토스트·팝업). UI가 없는 지금은 로그 + 이 값만 남긴다.
        /// MV-UI가 로비를 열 때 읽고 <see cref="ConsumeNotice"/>로 비운다.
        /// </summary>
        public static MvSaveNotice PendingNotice { get; private set; }

        /// <summary>열린 저장소의 재화 서비스(Framework). 저장이 닫혀 있으면 null. 저장을 다시 열면 새로 묶는다.</summary>
        public static ResourceService Resources { get; private set; }

        static WealthTable _wealthDefinitions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LastOpen = null;
            PendingNotice = MvSaveNotice.None;
            Resources = null;
            _wealthDefinitions = null;
        }

        public static MvSaveNotice ConsumeNotice()
        {
            var notice = PendingNotice;
            PendingNotice = MvSaveNotice.None;
            return notice;
        }

        /// <summary>6단계. 표(4단계)가 먼저 읽혀 있어야 한다. 재화 id가 표와 다르면 예외로 시작을 멈춘다.</summary>
        public static MvSaveOpenResult Open(MvTables tables)
        {
            if (tables == null) throw new InvalidOperationException("저장을 열기 전에 표(MvTables.Current)가 필요합니다.");

            var wealthRows = tables.Wealth.Datas.Select(row => new KeyValuePair<int, string>(row.id, row.type));
            var mismatches = MvWealthIds.Mismatches(wealthRows);
            if (mismatches.Count > 0)
                throw new InvalidOperationException("MvWealthIds가 01_wealth와 다릅니다: " + string.Join("; ", mismatches));

            _wealthDefinitions = tables.Wealth;
            MvSave.CurrentChanged -= BindResources;
            MvSave.CurrentChanged += BindResources;

            var result = MvSave.Init(new MvSaveOptions
            {
                Config = CreateConfig(tables),
                References = CreateReferences(tables),
            });
            LastOpen = result;
            PendingNotice = result.Notice;
            Report(result);
            return result;
        }

        static void Report(MvSaveOpenResult result)
        {
            switch (result.Notice)
            {
                case MvSaveNotice.None:
                    Debug.Log($"[MvBootSave] 저장 {result.Status}" + (result.Migrated ? $" (이관 {result.LoadedSchema})" : ""));
                    break;
                case MvSaveNotice.ToastRecovered:
                    Debug.LogWarning("[MvBootSave] 이전 저장을 불러왔습니다(toastMessage.saveRecovered).");
                    break;
                default:
                    // 팝업 대기: 저장 없이 로비까지 간다. 진행을 바꾸는 화면은 MvSave.Current가 null이면 막아야 한다.
                    Debug.LogWarning($"[MvBootSave] 저장을 열지 못했습니다: {result.Status} → {result.Notice}. {result.Error}");
                    break;
            }
        }

        /// <summary>5단계: 재화 서비스는 저장소(WealthContainer)와 표(WealthTable)가 둘 다 있어야 만든다.</summary>
        static void BindResources(MvSaveStore store)
        {
            if (Resources != null)
            {
                Resources.Dispose();
                Resources = null;
            }
            if (store == null || store.IsDisposed || _wealthDefinitions == null) return;
            Resources = new ResourceService(store.Repository, _wealthDefinitions, WealthTypeId);
        }

        /// <summary>저장 기본값이 읽는 설정(09 §9.5.4)을 표에서 만든다.</summary>
        public static MvSaveConfig CreateConfig(MvTables tables)
        {
            var config = tables.Config.Value ?? throw new InvalidOperationException("00_config 행이 없습니다.");
            var limits = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in tables.AdPlacement.Sorted)
                if (!string.IsNullOrEmpty(row.key)) limits[row.key] = row.dailyLimit;

            return new MvSaveConfig
            {
                StartDeck = PadStartDeck(config.startDeck, tables.WeaponTierMax()),
                StartGodKey = config.startGodKey,
                CpMax = tables.RunConfig.I(RunConfigKeys.CP_MAX),
                FloorCount = tables.FloorCount(), // 303 floor 최댓값(옛 FLOOR_COUNT)
                TableVersion = config.dataVersion,
                DayUtcOffsetHour = config.adDayUtcOffsetHour,
                AdDailyLimits = limits,
            };
        }

        /// <summary>
        /// 시작 덱 = <c>00_config.startDeck</c>, 길이는 103 active 상한 등급 + 1까지 0으로 채운다(MV-SPEC-02 Q-08).
        /// 등급 수가 표 설정만으로 바뀌어도 새 저장이 참조 정리(덱 길이)에 걸리지 않게 한다.
        /// </summary>
        static int[] PadStartDeck(IReadOnlyList<int> start, int tierMax)
        {
            var deck = new int[Math.Max(start.Count, tierMax + 1)];
            for (int i = 0; i < start.Count; i++) deck[i] = start[i];
            return deck;
        }

        /// <summary>참조 정리(09 §9.6) 대상 표 값.</summary>
        public static MvSaveReferenceCatalog CreateReferences(MvTables tables)
        {
            var barLimits = new Dictionary<int, int[]>();
            foreach (var area in tables.Area.Sorted)
            {
                var floors = tables.AreaFloor.GetGroup(area.floorGroup);
                int floorCount = floors.Count == 0 ? 0 : floors.Max(f => f.floor);
                var cells = new int[Math.Max(0, floorCount)];
                foreach (var floor in floors)
                    if (floor.floor >= 1) cells[floor.floor - 1] = floor.rows * 3;
                barLimits[area.areaNo - 1] = cells;
            }

            return new MvSaveReferenceCatalog
            {
                GodKeys = KeySet(tables.God.Sorted.Select(row => row.key)),
                MaxWeaponTier = tables.Item.Sorted.Count == 0 ? -1 : tables.WeaponTierMax(), // 103 active 상한(MV-SPEC-02 Q-08)
                MaxAreaIndex = tables.Area.Sorted.Count == 0 ? -1 : tables.Area.Sorted.Max(row => row.areaNo) - 1,
                BarCellLimits = barLimits,
                LobbyUpMax = tables.LobbyUpgrade.Sorted.ToDictionary(row => row.key, row => tables.LevelMax(row), StringComparer.Ordinal), // 계단·지팡이는 11·12 합계
                AreaUpMax = tables.AreaUpgrade.Sorted.ToDictionary(row => row.key, row => row.max, StringComparer.Ordinal),
                PerkMax = tables.RebirthPerk.Sorted.ToDictionary(row => row.key, row => row.max, StringComparer.Ordinal),
                AdPlacementKeys = KeySet(tables.AdPlacement.Sorted.Select(row => row.key)),
                // 400_content 표가 MvTables에 아직 없다. 들어오면 그 key로 채운다(그 전까지 modePlays는 정리하지 않는다).
                ModeContentKeys = null,
                ContentOpenKeys = KeySet(tables.ContentOpen.Sorted.Select(row => row.key)),
            };
        }

        static HashSet<string> KeySet(IEnumerable<string> keys) =>
            new HashSet<string>(keys.Where(key => !string.IsNullOrEmpty(key)), StringComparer.Ordinal);
    }
}
