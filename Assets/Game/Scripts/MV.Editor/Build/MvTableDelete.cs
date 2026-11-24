using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using jjevol;
using MV.Editor.Tables;
using UnityEditor;
using UnityEngine;

namespace MV.Editor.Build
{
    /// <summary>
    /// 표 삭제 감싸기(MV-DATA-TABLEDEL, tables.authoring 0.1.0-pre.15 <see cref="TableDeletion"/>).
    /// 실행기 setup 잡은 실행 인자를 넘기지 못하므로, 지울 표를 인자 대신 저장소 상태로 정한다:
    /// 원본 TSV가 없는데 Resources 산출물이 남은 표 = 원본을 지워 삭제를 명시한 표.
    /// 표를 지울 때는 TSV(.meta)를 지워 Commit한 뒤 이 잡을 돌리면 산출물·.meta·빌드 기록이 함께 정리된다.
    /// 빌드 기록에 없는 산출물이 걸리면 Framework가 거절하고 아무것도 지우지 않는다(자동 삭제 금지 안전장치는 그대로).
    /// 중앙 실행기: <c>unity_job.py submit --project moonvillage --modes setup --setup-method MV.Editor.Build.MvTableDelete.DeleteRemovedTablesJob</c>.
    /// </summary>
    public static class MvTableDelete
    {
        const string LogPrefix = "[MvTableDelete] ";

        /// <summary>원본 TSV가 없는데 산출물(<c>&lt;표&gt;.txt</c>)이 남은 표 이름(정렬).</summary>
        public static List<string> RemovedTables(string tsvDir, string outputDir)
        {
            var sources = new HashSet<string>(
                Directory.Exists(tsvDir) ? Directory.GetFiles(tsvDir, "*.txt").Select(Path.GetFileNameWithoutExtension) : Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            return (Directory.Exists(outputDir) ? Directory.GetFiles(outputDir, "*.txt") : Array.Empty<string>())
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !sources.Contains(n))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// setup 잡 진입점. 지울 표가 없으면 0. 있으면 Framework 표 삭제의 종료 코드
        /// (0 완료, 1 거절·실패(바뀐 것 없음), 2 확인 필요, 3 복구 필요, 4 원본 일부 남음).
        /// </summary>
        public static void DeleteRemovedTablesJob()
        {
            int code;
            try { code = DeleteRemovedTables(); }
            catch (Exception e) { Debug.LogError(LogPrefix + "예외로 중단: " + e); code = (int)TableMaintenanceOutcome.Failed; }
            Debug.Log(LogPrefix + "종료 코드 " + code);
            EditorApplication.Exit(code);
        }

        public static int DeleteRemovedTables()
        {
            string input = Path.GetFullPath(MvTableAuthoring.TsvAssetPath), output = Path.GetFullPath(MvTableAuthoring.OutputAssetPath);
            var names = RemovedTables(input, output);
            if (names.Count == 0)
            {
                Debug.Log(LogPrefix + "원본 TSV를 지운 표가 없습니다(정리할 것 없음).");
                return (int)TableMaintenanceOutcome.Completed;
            }
            Debug.Log(LogPrefix + "원본을 지운 표: " + string.Join(", ", names));
            TableAuthoringOptions.UseEncryption = false; // 이 게임 표는 암호화하지 않는다(MvTableAuthoring·MvBuild와 같음)
            var plan = TableDeletion.Plan(input, output, names, MvTableAuthoring.ValidateBuildInputs);
            Debug.Log(LogPrefix + "삭제 계획\n" + plan);
            var result = TableDeletion.Apply(plan);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (result.Succeeded) Debug.Log(LogPrefix + result); else Debug.LogError(LogPrefix + result);
            return result.ExitCode;
        }
    }
}
