using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MV.Data;

namespace MV.Sound.Tests
{
    /// <summary>저장소 TSV 정본으로 표를 읽는다(파일은 바꾸지 않는다). overrides: 표 이름 → TSV 전체 텍스트.</summary>
    internal static class SoundTestTables
    {
        static MvTables s_cached;

        public static MvTables Load()
        {
            if (s_cached != null) return s_cached;
            s_cached = Load(null);
            return s_cached;
        }

        public static MvTables Load(Dictionary<string, string> overrides)
        {
            var dir = new MvTsvDirectorySource(MvTableLoader.TsvDirectory);
            jjevol.ITableSource src = dir;
            if (overrides != null)
            {
                var mem = new MvMemoryTableSource(dir);
                foreach (var kv in overrides) mem.Set(kv.Key, kv.Value);
                src = mem;
            }
            var t = new MvTables();
            t.LoadAll(src);
            t.BuildIndexes();
            return t;
        }

        public static string ReadTsv(string table) =>
            File.ReadAllText(Path.Combine(MvTableLoader.TsvDirectory, table + ".txt"), new UTF8Encoding(false));

        /// <summary>key 행의 열 값을 바꾼 TSV 텍스트.</summary>
        public static string SetByKey(string tsv, string key, string column, string value)
        {
            var lines = tsv.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            var header = lines[0].Split('\t');
            int k = Array.IndexOf(header, "key"), c = Array.IndexOf(header, column);
            if (k < 0 || c < 0) throw new ArgumentException(column);
            bool found = false;
            for (int i = 1; i < lines.Length; i++)
            {
                var cells = lines[i].Split('\t');
                if (cells.Length <= k || cells[k] != key) continue;
                if (cells.Length <= c) Array.Resize(ref cells, header.Length);
                cells[c] = value;
                lines[i] = string.Join("\t", cells);
                found = true;
            }
            if (!found) throw new ArgumentException(key);
            return string.Join("\n", lines) + "\n";
        }
    }

    internal sealed class FakeClock : IMvSoundClock
    {
        public double Now { get; set; }
    }

    /// <summary>재생 요청 기록 대역.</summary>
    internal sealed class RecordingOut : IMvAudioOut
    {
        public readonly struct Sfx
        {
            public readonly string Address;
            public readonly float Volume, Pitch;
            public Sfx(string a, float v, float p) { Address = a; Volume = v; Pitch = p; }
            public override string ToString() => Address;
        }

        public readonly List<Sfx> Played = new List<Sfx>();
        /// <summary>음악 쪽 호출 순서: <c>music:주소:크로스페이드:keep</c>, <c>stop</c>, <c>on:true/false</c>.</summary>
        public readonly List<string> Music = new List<string>();

        public void PlaySfx(string address, float volume, float pitch) => Played.Add(new Sfx(address, volume, pitch));

        public void PlayMusic(string address, float volume, float crossfadeSeconds, bool keepPosition) =>
            Music.Add("music:" + address + ":" + crossfadeSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ":" + (keepPosition ? "keep" : "start"));

        public void StopMusic() => Music.Add("stop");

        public void SetMusicOn(bool on) => Music.Add(on ? "on:true" : "on:false");

        public List<string> Addresses()
        {
            var l = new List<string>();
            foreach (var p in Played) l.Add(p.Address);
            return l;
        }
    }
}
