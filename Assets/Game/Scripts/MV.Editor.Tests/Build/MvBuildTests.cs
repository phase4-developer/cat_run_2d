using System;
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
    }
}
