using System.IO;
using MV.Editor.Build;
using MV.Editor.Tables;
using NUnit.Framework;

namespace MV.Editor.Tests.Build
{
    /// <summary>표 삭제 감싸기(MV-DATA-TABLEDEL): 지울 표 = 원본 TSV가 없는데 산출물이 남은 표. 자산은 지우지 않는다.</summary>
    public class MvTableDeleteTests
    {
        string _root;

        [SetUp] public void SetUp() { _root = Path.Combine(Path.GetTempPath(), "MvTableDeleteTests_" + System.Guid.NewGuid().ToString("N")); }
        [TearDown] public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        void Touch(string dir, string name) { Directory.CreateDirectory(Path.Combine(_root, dir)); File.WriteAllText(Path.Combine(_root, dir, name), "x"); }

        [Test]
        public void RemovedTables_AreOutputsWithoutSource()
        {
            Touch("tsv", "01_a.txt"); Touch("tsv", "02_b.txt");
            Touch("out", "01_a.txt"); Touch("out", "02_b.txt"); Touch("out", "09_gone.txt"); Touch("out", "09_gone.txt.meta");
            Assert.That(MvTableDelete.RemovedTables(Path.Combine(_root, "tsv"), Path.Combine(_root, "out")), Is.EqualTo(new[] { "09_gone" }));
        }

        [Test]
        public void RemovedTables_NoneWhenEveryOutputHasSource()
        {
            Touch("tsv", "01_a.txt"); Touch("out", "01_a.txt");
            Assert.That(MvTableDelete.RemovedTables(Path.Combine(_root, "tsv"), Path.Combine(_root, "out")), Is.Empty);
        }

        /// <summary>저장소 지금 상태: 지운 표가 남아 있지 않다(09_uiResource는 MV-DATA-09CLEAN에서 정리).</summary>
        [Test]
        public void Repository_HasNoRemovedTables()
        {
            Assert.That(MvTableDelete.RemovedTables(Path.GetFullPath(MvTableAuthoring.TsvAssetPath), Path.GetFullPath(MvTableAuthoring.OutputAssetPath)), Is.Empty);
        }
    }
}
