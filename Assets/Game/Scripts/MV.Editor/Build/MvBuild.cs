using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;
using jjevol;
using MV.Editor.Tables;

namespace MV.Editor.Build
{
    /// <summary>
    /// 중앙 실행기 build 모드가 <c>-executeMethod MV.Editor.Build.MvBuild.BuildAndroidDev</c>로 부르는 Android 개발 빌드.
    /// 스스로 <see cref="EditorApplication.Exit"/>로 끝난다(0 성공, 그 밖은 실패). 서명 keystore는 다루지 않는다.
    /// </summary>
    public static class MvBuild
    {
        /// <summary>프로젝트 폴더 기준 출력 APK. 실행기 <c>--build-output</c>에 같은 경로를 준다.</summary>
        public const string AndroidDevOutputPath = "Builds/Android/MoonVillage-dev.apk";

        /// <summary>GoogleMobileAds 설정의 Android 앱 ID가 비었을 때만 넣는 Google 공개 시험 앱 ID(잠정 결정 D-BUILD-ADMOBID).</summary>
        public const string AdMobAndroidTestAppId = "ca-app-pub-3940256099942544~3347511713";

        public const int ExitSuccess = 0;
        public const int ExitBuildFailed = 1;
        public const int ExitWrongTarget = 2;
        public const int ExitResolverMissing = 3;
        public const int ExitAddressablesFailed = 4;
        public const int ExitPlayerSettingsWrong = 5;
        public const int ExitTablesFailed = 6;

        /// <summary>빌드가 남기는 Addressables 임시 link.xml(Unity가 다음에 열 때 지워 실행기 후보 불일치를 낸다).</summary>
        public const string AddressablesTempLinkXml = "Assets/AddressableAssetsData/link.xml";

        const string LogPrefix = "[MvBuild] ";
        const string ResolverTypeName = "GooglePlayServices.PlayServicesResolver";
        const string AdMobSettingsTypeName = "GoogleMobileAds.Editor.GoogleMobileAdsSettings";
        const string AddressablesPrefKey = "Addressables.BuildAddressablesWithPlayerBuild";

        /// <summary>빌드에 넣을 씬: <see cref="EditorBuildSettings.scenes"/> 중 enabled, 순서 그대로.</summary>
        public static string[] EnabledScenePaths()
        {
            return EditorBuildSettings.scenes.Where(s => s.enabled && !string.IsNullOrEmpty(s.path)).Select(s => s.path).ToArray();
        }

        public static void BuildAndroidDev()
        {
            int code;
            try
            {
                code = RunAndroidDev();
            }
            catch (Exception e)
            {
                Debug.LogError(LogPrefix + "예외로 빌드 중단: " + e);
                code = ExitBuildFailed;
            }
            Debug.Log(LogPrefix + "종료 코드 " + code);
            EditorApplication.Exit(code);
        }

        static int RunAndroidDev()
        {
            // 1. 대상 확인 — 실행기는 -buildTarget Android로 띄운다.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError(LogPrefix + "활성 빌드 대상이 Android가 아니다: " + EditorUserBuildSettings.activeBuildTarget);
                return ExitWrongTarget;
            }

            // 2. EDM4U Android 의존성 동기 해석(PlayServicesResolver.ResolveSync(bool forceResolution), 1.2.186 공개 정적).
            var resolve = FindStaticMethod(ResolverTypeName, "ResolveSync", BindingFlags.Public, typeof(bool));
            if (resolve == null)
            {
                Debug.LogError(LogPrefix + ResolverTypeName + ".ResolveSync(bool)를 찾지 못했다(EDM4U 미로드).");
                return ExitResolverMissing;
            }
            Debug.Log(LogPrefix + "EDM4U Android 의존성 해석 시작");
            var resolved = resolve.Invoke(null, new object[] { true });
            if (!(resolved is bool ok && ok))
                // GoogleMobileAds AndroidBuildPreProcessor가 BuildPlayer 안에서 ResolveSync(true)를 다시 부르므로 실제 실패는 빌드 결과로 드러난다.
                Debug.LogWarning(LogPrefix + "EDM4U ResolveSync가 실패를 돌려줬다. 빌드 중 해석에 맡기고 계속한다.");

            // 3. AdMob Android 앱 ID — 비어 있으면 GoogleMobileAds ManifestProcessor가 빌드를 멈춘다.
            EnsureAdMobAndroidAppId();

            // 3b. 최신 Android(64비트 전용 기기)에서 돌도록 IL2CPP·ARM64인지 확인한다. 설정은 ProjectSettings가 정본이라 바꾸지 않는다.
            if (!IsDeviceReady(PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android), PlayerSettings.Android.targetArchitectures))
            {
                Debug.LogError(LogPrefix + "Android 설정이 IL2CPP·ARM64가 아니다: " + PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android) + ", " + PlayerSettings.Android.targetArchitectures);
                return ExitPlayerSettingsWrong;
            }

            // 3c. 표 산출물 — 플레이어는 Resources 표만 읽는다(에디터의 TSV 대체 경로는 플레이어에 없다). TSV에서 TableBuilder로 만든다(검증기 포함, 암호화 끔).
            try
            {
                var tables = TableBuilder.Build(Path.GetFullPath(MvTableAuthoring.TsvAssetPath), Path.GetFullPath(MvTableAuthoring.OutputAssetPath),
                    useEncrypt: false, validate: MvTableAuthoring.ValidateBuildInputs);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log(LogPrefix + $"표 산출물 {tables.State}, 파일 {tables.OutputFiles.Count}, 바뀜 없음 {tables.NoOp}");
            }
            catch (Exception e)
            {
                Debug.LogError(LogPrefix + "표 산출물 만들기 실패: " + e.Message);
                return ExitTablesFailed;
            }

            // 4. Addressables — 모든 그룹이 Local 경로라 APK 안에 들어간다(사용자 「원격 에셋은 안쓸거니 다 로컬로」). 플레이어 빌드가 자동으로 콘텐츠를 빌드하지 않을 때만 한 번 빌드한다. 설정 자산은 바꾸지 않는다.
            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (aaSettings == null)
            {
                Debug.LogError(LogPrefix + "Addressables 설정이 없다.");
                return ExitAddressablesFailed;
            }
            var option = aaSettings.BuildAddressablesWithPlayerBuild;
            bool auto = BuildsAddressablesWithPlayer(option, EditorPrefs.GetBool(AddressablesPrefKey, true));
            Debug.Log(LogPrefix + "Addressables 플레이어 빌드 시 자동 빌드: " + option + " → " + (auto ? "자동" : "수동"));
            if (!auto)
            {
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult aaResult);
                if (aaResult == null || !string.IsNullOrEmpty(aaResult.Error))
                {
                    Debug.LogError(LogPrefix + "Addressables 콘텐츠 빌드 실패: " + (aaResult == null ? "결과 없음" : aaResult.Error));
                    return ExitAddressablesFailed;
                }
            }

            // 5. 플레이어 빌드.
            var scenes = EnabledScenePaths();
            if (scenes.Length == 0)
            {
                Debug.LogError(LogPrefix + "빌드 씬이 없다(EditorBuildSettings).");
                return ExitBuildFailed;
            }
            var outputFull = Path.GetFullPath(AndroidDevOutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFull));
            EditorUserBuildSettings.buildAppBundle = false;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = AndroidDevOutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            });
            var summary = report.summary;
            Debug.Log(LogPrefix + $"빌드 결과 {summary.result}, 오류 {summary.totalErrors}, 크기 {summary.totalSize}, 출력 {AndroidDevOutputPath}");
            DeleteTempLinkXml();
            return summary.result == BuildResult.Succeeded ? ExitSuccess : ExitBuildFailed;
        }

        static void DeleteTempLinkXml()
        {
            foreach (var path in new[] { AddressablesTempLinkXml, AddressablesTempLinkXml + ".meta" })
                if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>64비트 전용 기기에서도 설치·실행되는 조합인지(IL2CPP, ARM64 포함).</summary>
        public static bool IsDeviceReady(ScriptingImplementation backend, AndroidArchitecture architectures)
        {
            return backend == ScriptingImplementation.IL2CPP && (architectures & AndroidArchitecture.ARM64) != 0;
        }

        /// <summary>Addressables 4.0.1 AddressablesPlayerBuildProcessor.ShouldBuildAddressablesForPlayerBuild와 같은 판정.</summary>
        static bool BuildsAddressablesWithPlayer(AddressableAssetSettings.PlayerBuildOption option, bool preferenceValue)
        {
            switch (option)
            {
                case AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer: return false;
                case AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer: return true;
                default: return preferenceValue;
            }
        }

        /// <summary>
        /// GoogleMobileAds 10.7.0의 internal <c>GoogleMobileAds.Editor.GoogleMobileAdsSettings</c>(어셈블리 GoogleMobileAds.Editor)를 리플렉션으로 연다.
        /// LoadInstance()는 자산이 없으면 Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset을 만든다.
        /// </summary>
        static void EnsureAdMobAndroidAppId()
        {
            var loadInstance = FindStaticMethod(AdMobSettingsTypeName, "LoadInstance", BindingFlags.NonPublic | BindingFlags.Public);
            if (loadInstance == null)
            {
                Debug.LogWarning(LogPrefix + AdMobSettingsTypeName + ".LoadInstance를 찾지 못했다. 앱 ID 확인을 건너뛴다.");
                return;
            }
            var instance = loadInstance.Invoke(null, null) as ScriptableObject;
            var prop = instance == null ? null : instance.GetType().GetProperty("GoogleMobileAdsAndroidAppId", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                Debug.LogWarning(LogPrefix + "GoogleMobileAdsAndroidAppId 속성을 찾지 못했다. 앱 ID 확인을 건너뛴다.");
                return;
            }
            var current = prop.GetValue(instance) as string;
            if (!string.IsNullOrWhiteSpace(current))
            {
                Debug.Log(LogPrefix + "AdMob Android 앱 ID가 이미 있다. 그대로 둔다.");
                return;
            }
            prop.SetValue(instance, AdMobAndroidTestAppId);
            EditorUtility.SetDirty(instance);
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "AdMob Android 앱 ID가 비어 Google 공개 시험 앱 ID를 넣었다(D-BUILD-ADMOBID).");
        }

        static MethodInfo FindStaticMethod(string typeName, string methodName, BindingFlags visibility, params Type[] parameterTypes)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try { type = assembly.GetType(typeName, false); }
                catch (Exception) { continue; }
                if (type == null) continue;
                var method = type.GetMethod(methodName, BindingFlags.Static | visibility, null, parameterTypes, null);
                if (method != null) return method;
            }
            return null;
        }
    }
}
