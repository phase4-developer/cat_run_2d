using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using jjevol;
using MV.Data;
using UnityEditor;
using UnityEngine;

namespace MV.Editor.Tables
{
    /// <summary>
    /// `07_consumeType.txt` → C# `MV.Data.ConsumeType` enum. Framework 표 제작 도구의 ConsumeTypes 코드 생성(com.jjevol.tables.authoring
    /// `ConsumeTypesGenerationSettings`)을 그대로 부른다(07 §7.8.12 codeName 열).
    /// <list type="bullet">
    /// <item>정의 입력 이름은 Framework 기본 `ConsumeTypes`다. `07_consumeType`은 부팅이 읽는 런타임 표로도 빌드되므로
    /// 같은 이름에 Authoring 전용 역할을 주면 Framework가 막는다(ConsumeRoleConflict). 내용은 같은 TSV 원문이다.</item>
    /// <item>출력 `Core/Generated/ConsumeType.Generated.cs`·`ConsumeTypeExtensions.Generated.cs`(MV.Data asmdef, Economy 참조).
    /// 생성기는 None을 넣지 않으므로 표의 typeId 0 `None` 행이 enum 기본값이 된다.</item>
    /// <item>첫 생성은 손으로 쓴 `Core/ConsumeType.cs` enum이 컴파일되어 있는 동안 막히고(ConsumeTypeCollision), 그 enum을 지우면
    /// 컴파일 오류로 막힌다(ProjectCompileErrors). 생성 파일을 손으로 만들어 두는 것도 안 된다(관리 기록 없는 파일은 덮어쓰지 않음,
    /// GeneratedUnmanagedConflict). 이관 경로는 Framework 요청 대기(TaskBoard MV-DATA). 그때까지 「계획 보기」로 표·생성 결과만 확인한다.</item>
    /// <item>적용은 `ProjectSettings/JJEvolTableAuthoring/` 관리 기록을 쓴다(통합 Writer 영역, 총괄이 실행).</item>
    /// </list>
    /// </summary>
    public static class MvConsumeTypeCodegen
    {
        public const string DefinitionName = "ConsumeTypes";
        public const string CodeAssetPath = "Assets/Game/Scripts/MV.Data/Core/Generated";
        public const string CodeNamespace = "MV.Data";
        public const string EnumName = "ConsumeType";

        public static string SourcePath => Path.GetFullPath(Path.Combine(MvTableAuthoring.TsvAssetPath, ConsumeTypeTable.Name + ".txt"));

        public static ConsumeTypesGenerationSettings Settings() =>
            new ConsumeTypesGenerationSettings(Path.GetFullPath(CodeAssetPath), CodeNamespace, EnumName, DefinitionName);

        /// <summary>07 원문을 게임 검증기로 먼저 확인하고 코드 생성 계획을 만든다(출력 쓰기 없음).</summary>
        public static TableBuildPlan Plan()
        {
            var text = File.ReadAllText(SourcePath, new UTF8Encoding(false)).Replace("\r\n", "\n");
            var report = MvTableAuthoring.Validate(new[] { new KeyValuePair<string, string>(ConsumeTypeTable.Name, text) });
            if (report.HasError) throw new InvalidDataException(report.Summary(10));

            var input = TableInput.FromText(DefinitionName, text, TableDialect.PlainTsv, ConsumeTypeTable.Name + ".txt");
            var request = new TableBuildRequest(new[] { input }, Path.GetFullPath(MvTableAuthoring.OutputAssetPath), useEncrypt: false,
                validationVersion: MvTableAuthoring.ValidationVersion, consumeTypes: Settings());
            return TableBuilder.Plan(request);
        }

        static string Describe(TableBuildPlan plan)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[MvConsumeTypeCodegen] {(plan.IsValid ? "적용 가능" : "막힘")} — {SourcePath} → {CodeAssetPath}");
            foreach (var d in plan.Diagnostics) sb.AppendLine("  " + d);
            if (plan.ConsumeTypes != null)
            {
                foreach (var line in plan.ConsumeTypes.Differences) sb.AppendLine("  변경: " + line);
                foreach (var change in plan.ConsumeTypes.Changes) sb.AppendLine($"  파일: {change.TableName} {change.Action}");
            }
            return sb.ToString();
        }

        [MenuItem("MV/Tables/ConsumeType 코드 생성 - 계획 보기")]
        static void PlanMenu()
        {
            try
            {
                var plan = Plan();
                if (plan.IsValid) Debug.Log(Describe(plan));
                else Debug.LogError(Describe(plan));
            }
            catch (Exception e)
            {
                Debug.LogError("[MvConsumeTypeCodegen] 계획 실패: " + e.Message);
            }
        }

        [MenuItem("MV/Tables/ConsumeType 코드 생성 - 적용")]
        static void ApplyMenu()
        {
            TableBuildPlan plan;
            try { plan = Plan(); }
            catch (Exception e)
            {
                Debug.LogError("[MvConsumeTypeCodegen] 계획 실패: " + e.Message);
                return;
            }
            var text = Describe(plan);
            if (!plan.IsValid)
            {
                Debug.LogError(text);
                return;
            }
            if (!EditorUtility.DisplayDialog("ConsumeType 코드 생성", text, "적용", "취소")) return;

            var prepared = TableBuilder.Prepare(plan);
            if (prepared.IsNoOp)
            {
                Debug.Log("[MvConsumeTypeCodegen] 바뀐 것이 없습니다.");
                return;
            }
            var result = prepared.Apply();
            Debug.Log($"[MvConsumeTypeCodegen] 적용: {result.State} ({string.Join(", ", result.OutputFiles.Select(f => Path.GetFileName(f)))}). 컴파일 확인 뒤 Ready가 됩니다.");
        }
    }
}
