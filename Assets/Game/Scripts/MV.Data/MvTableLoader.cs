using System;
using System.IO;
using System.Threading.Tasks;
using jjevol;
using jjevol.Data;
using UnityEngine;

namespace MV.Data
{
    /// <summary>
    /// 부팅 4단계의 표 부분(기획서 10 §10.7, 07 §7.1.8): 원본 지정 → RegisterAll → TableRepository.LoadAllAsync →
    /// 게임 검증기 → BuildIndexes → <see cref="MvTables.Current"/>. 검증 오류면 <see cref="MvTableValidationException"/>로 시작을 멈춘다.
    /// </summary>
    public static class MvTableLoader
    {
        /// <summary>TSV 정본 폴더(에디터 전용 대체 원본·작성 검증).</summary>
        public static string TsvDirectory => Path.Combine(Application.dataPath, "Game", "GameData", "Table", "TSV");

        /// <summary>마지막 로드의 검증 결과(경고 포함). 개발 화면·시험이 읽는다.</summary>
        public static MvValidationReport LastReport { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LastReport = null;
            MvTables.Current = null;
        }

        /// <summary>
        /// 출시 경로는 TableBuilder 산출물(Resources, 암호화 끔). 에디터에서 산출물이 아직 없으면 TSV 폴더를 읽는다
        /// (표 제작 창으로 반영하기 전 개발 대체, 07 §7.1.9).
        /// </summary>
        public static ITableSource DefaultSource()
        {
            var packaged = new PackagedTableSource(useEncrypt: false);
#if UNITY_EDITOR
            if (!packaged.HasTable(ConfigData.Table) && Directory.Exists(TsvDirectory))
            {
                Debug.LogWarning("[MvTableLoader] Resources 표 산출물이 없어 TSV 폴더를 읽습니다(에디터 전용). 표 제작 창으로 반영하세요.");
                return new MvTsvDirectorySource(TsvDirectory);
            }
#endif
            return packaged;
        }

        public static async Task<MvTables> LoadAsync(ITableSource source = null, MvValidationOptions options = null, Action<float> onProgress = null)
        {
            GameTableSource.Configure(source ?? DefaultSource());
            var tables = new MvTables();
            if (!TableRepository.loaded)
            {
                tables.RegisterAll();
                await TableRepository.LoadAllAsync(onProgress);
            }
            else
            {
                // 저장소가 이미 한 번 돌았다(같은 세션의 재부팅). 저장소의 1회 규칙을 건드리지 않고 직접 읽는다.
                foreach (var t in tables.All) await t.Table.LoadDatasAsync();
                onProgress?.Invoke(1f);
            }

            var report = MvTableValidator.ValidateRuntime(tables, options);
            LastReport = report;
            if (report.WarningCount > 0) Debug.LogWarning("[MvTableLoader] " + report.Summary());
            if (report.HasError) throw new MvTableValidationException(report);

            tables.BuildIndexes();
            MvTables.Current = tables;
            return tables;
        }
    }
}
