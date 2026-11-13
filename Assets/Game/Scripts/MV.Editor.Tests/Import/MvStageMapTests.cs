using System.Collections.Generic;
using MV.Editor.Import;
using NUnit.Framework;

namespace MV.Editor.Tests.Import
{
    /// <summary>MV-VIEW-STAGE: 302_area.stageMap이 배경 그룹에 등록된 맵 프리팹 주소다(대체 경로 없음).</summary>
    public sealed class MvStageMapTests
    {
        [Test]
        public void AreaStageMaps_ReferenceBgPrefabs()
        {
            var tables = new MV.Data.MvTables();
            tables.LoadAll(new MV.Data.MvTsvDirectorySource(MV.Data.MvTableLoader.TsvDirectory));
            var bg = MvFolderArt.BgEntries();
            var failures = new List<string>();
            Assert.That(tables.Area.Datas, Is.Not.Empty, "302_area 행 없음");
            foreach (var row in tables.Area.Datas)
            {
                if (string.IsNullOrEmpty(row.stageMap)) failures.Add(row.id + ": stageMap 빈칸");
                else if (!bg.ContainsKey(row.stageMap)) failures.Add($"{row.id}: {row.stageMap}가 배경 프리팹에 없음");
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
    }
}
