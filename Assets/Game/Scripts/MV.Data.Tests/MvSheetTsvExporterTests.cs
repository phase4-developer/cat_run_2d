using MV.Editor.Tables;
using NUnit.Framework;

namespace MV.Data.Tests
{
    /// <summary>시트 → TSV 내보내기(기획서 07 §7.1.1): 사람용 2줄과 `#` 열을 뺀다.</summary>
    public sealed class MvSheetTsvExporterTests
    {
        [Test]
        public void Convert_DropsHumanLinesAndCommentColumns()
        {
            const string sheet = "\uFEFF뜻\t설명\t메모\t단계\r\n" +
                                 "int\tstring\t\tint\r\n" +
                                 "id\tkey\t#비고\tlv\r\n" +
                                 "11001001\tstair\t사람용\t1\r\n" +
                                 "\t\t\t\r\n" +
                                 "11001002\tstair2\t\t2\r\n";
            Assert.That(MvSheetTsvExporter.Convert(sheet), Is.EqualTo("id\tkey\tlv\n11001001\tstair\t1\n11001002\tstair2\t2\n"));
        }

        [Test]
        public void Convert_WithoutHeaderLine_Throws() =>
            Assert.Throws<System.FormatException>(() => MvSheetTsvExporter.Convert("설명\nint\n"));
    }
}
