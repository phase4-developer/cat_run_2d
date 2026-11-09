using MV.Data;

namespace MV.View.Tests
{
    /// <summary>시험용 표(저장소 TSV를 직접 읽는다. 사용자 저장·PlayerPrefs 안 씀).</summary>
    internal static class ViewTestTables
    {
        static MvTables _cached;

        public static MvTables Load()
        {
            if (_cached != null) return _cached;
            var t = new MvTables();
            t.LoadAll(new MvTsvDirectorySource(MvTableLoader.TsvDirectory));
            t.BuildIndexes();
            _cached = t;
            return t;
        }

        public static ViewConfig Config() => ViewConfig.From(Load().RunConfig);

        public static ThemeData Theme(string key) => Load().Theme.ByKey(key);
    }
}
