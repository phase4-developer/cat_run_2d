using System;
using System.Linq;
using MV.Editor.Build;
using NUnit.Framework;

namespace MV.Editor.Tests
{
    /// <summary>실행기 build 모드 계약(UNITY_JOB_CONTRACT 「빌드」) — MV-BUILD-ANDROID.</summary>
    public sealed class MvBuildTests
    {
        // 실행기가 BUILD_OUTPUT_REFUSED로 거부하는 프로젝트 폴더.
        static readonly string[] RunnerRefusedRoots = { "Library", "Temp", "Logs", "ProjectSettings", "UserSettings", "Packages" };

        [Test]
        public void AndroidDevOutput_IsApkUnderBuilds_NotRunnerRefusedFolder()
        {
            var path = MvBuild.AndroidDevOutputPath.Replace('\\', '/');
            Assert.That(path, Does.StartWith("Builds/"));
            Assert.That(path, Does.EndWith(".apk"));
            Assert.That(path, Does.Not.Contain(".."));
            var root = path.Split('/')[0];
            Assert.That(RunnerRefusedRoots, Has.None.EqualTo(root).IgnoreCase);
        }

        [Test]
        public void EnabledScenes_NotEmpty_BootFirst()
        {
            var scenes = MvBuild.EnabledScenePaths();
            Assert.That(scenes, Is.Not.Empty);
            Assert.That(scenes[0].EndsWith("/Boot.unity", StringComparison.Ordinal), Is.True, scenes[0]);
        }

        [Test]
        public void DeviceReady_NeedsIl2cppAndArm64()
        {
            Assert.That(MvBuild.IsDeviceReady(UnityEditor.ScriptingImplementation.IL2CPP, UnityEditor.AndroidArchitecture.ARM64), Is.True);
            Assert.That(MvBuild.IsDeviceReady(UnityEditor.ScriptingImplementation.IL2CPP, UnityEditor.AndroidArchitecture.ARMv7 | UnityEditor.AndroidArchitecture.ARM64), Is.True);
            Assert.That(MvBuild.IsDeviceReady(UnityEditor.ScriptingImplementation.Mono2x, UnityEditor.AndroidArchitecture.ARMv7), Is.False);
            Assert.That(MvBuild.IsDeviceReady(UnityEditor.ScriptingImplementation.IL2CPP, UnityEditor.AndroidArchitecture.ARMv7), Is.False);
        }

        [Test]
        public void ProjectAndroidSettings_AreDeviceReady()
        {
            var backend = UnityEditor.PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android);
            var arch = UnityEditor.PlayerSettings.Android.targetArchitectures;
            Assert.That(MvBuild.IsDeviceReady(backend, arch), Is.True, backend + ", " + arch);
        }
    

        [Test]
        public void TableOutputs_ExistForEveryTsv()
        {
            // 게임은 Resources 산출물만 읽는다(대체 경로 금지). TSV를 더하고 TableBuilder로 반영하지 않으면 여기서 드러난다.
            var tsv = System.IO.Directory.GetFiles(MV.Editor.Tables.MvTableAuthoring.TsvAssetPath, "*.txt").Select(System.IO.Path.GetFileName).OrderBy(n => n).ToArray();
            var outDir = MV.Editor.Tables.MvTableAuthoring.OutputAssetPath;
            Assert.That(tsv, Is.Not.Empty);
            var missing = tsv.Where(n => !System.IO.File.Exists(System.IO.Path.Combine(outDir, n))).ToArray();
            Assert.That(missing, Is.Empty, "Resources 표 산출물 없음: " + string.Join(", ", missing));
        }
    }
}
