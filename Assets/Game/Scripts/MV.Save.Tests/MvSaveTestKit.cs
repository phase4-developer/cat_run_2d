using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MV.Save;

namespace MV.Save.Tests
{
    /// <summary>메모리 PlayerPrefs. 실제 PlayerPrefs·persistentDataPath를 쓰지 않는다.</summary>
    internal sealed class MemoryPrefs : IMvSavePrefs
    {
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal);
        public bool FailNextSet;

        public bool HasKey(string key) => Values.ContainsKey(key);
        public string GetString(string key) => Values.TryGetValue(key, out string value) ? value : string.Empty;
        public void SetString(string key, string value)
        {
            if (FailNextSet) { FailNextSet = false; throw new IOException("시험: 쓰기 실패 주입"); }
            Values[key] = value;
        }
        public void Save() { }
        public void DeleteKey(string key) => Values.Remove(key);
        public Dictionary<string, string> Copy() => new Dictionary<string, string>(Values, StringComparer.Ordinal);
    }

    /// <summary>합성 시험 키.</summary>
    internal sealed class TestSaveKey : IMvSaveKeyProvider
    {
        private readonly string _seed;
        public string KeyId { get; }
        public TestSaveKey(string keyId = "mv-test-1", string seed = "MoonVillage.Test.Key.A") { KeyId = keyId; _seed = seed; }
        public byte[] GetMasterKey()
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(_seed));
        }
    }

    internal sealed class TestClock
    {
        public long Now = MvSaveTestKit.Now;
    }

    internal static class MvSaveTestKit
    {
        /// <summary>2023-11-14T22:13:20Z.</summary>
        public const long Now = 1_700_000_000_000L;
        public const string Today = "2023-11-14";
        private static int _serial;

        public static MvSaveOptions Options(MemoryPrefs prefs, TestClock clock = null, IMvSaveKeyProvider key = null)
        {
            clock ??= new TestClock();
            return new MvSaveOptions
            {
                ProfileId = "mvt" + (++_serial),
                KeyNamespace = "mv.test.save",
                KeyProvider = key ?? new TestSaveKey(),
                Preferences = prefs,
                NowMs = () => clock.Now,
            };
        }

        public static string Slot(MvSaveOptions options, int index) => options.KeyNamespace + ":" + options.ProfileId + ":" + index;

        /// <summary>칸의 인증 태그 1바이트를 뒤집는다(무결성 실패 = 손상).</summary>
        public static void CorruptSlot(MemoryPrefs prefs, string key)
        {
            byte[] bytes = Convert.FromBase64String(prefs.Values[key]);
            bytes[bytes.Length - 1] ^= 0xFF;
            prefs.Values[key] = Convert.ToBase64String(bytes);
        }

        /// <summary>정본 DEF()(L844~845)를 JSON으로 옮긴 값.</summary>
        public const string ProtoDefJson =
            "{\"level\":1,\"area\":0,\"zone\":0,\"gold\":0,\"souls\":0,\"dia\":0,\"bars\":{},\"cp\":0," +
            "\"up\":{\"stair\":0,\"weapon\":0,\"income\":0},\"deck\":[0,1,0,0,0,0,0,0,0],\"god\":\"thunder\",\"escaped\":false,\"runs\":0," +
            "\"aup\":{\"rate\":0,\"range\":0,\"clone\":0,\"crit\":0,\"dbl\":0}," +
            "\"rb\":{\"lv\":0,\"tokens\":0,\"pending\":0,\"perks\":{\"power\":0,\"crit\":0,\"obst\":0,\"dcap\":0},\"cdUntil\":0}," +
            "\"ads\":{\"day\":\"\",\"dia\":3,\"skip\":4},\"zoneClear\":false}";
    }
}
