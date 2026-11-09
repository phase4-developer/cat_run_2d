using System;
using System.Security.Cryptography;
using System.Text;
using jjevol;
using UnityEngine;

namespace MV.Save
{
    /// <summary>저장 암호화 키 공급(D-68). 같은 KeyId에는 항상 같은 32바이트 키를 준다.</summary>
    public interface IMvSaveKeyProvider
    {
        string KeyId { get; }
        byte[] GetMasterKey();
    }

    /// <summary>
    /// 개발 빌드 키. 공개 고정값이라 비밀이 아니다 — 개발 저장을 누구나 열어 볼 수 있다.
    /// Framework 대체: D-68은 「개발은 암호화 끔」이지만 고정본 com.jjevol.save 0.1.0-pre.3은 평문 저장을 지원하지 않는다(키 필수).
    /// 그래서 개발은 공개 키로 「끈 것과 같은」 상태를 만든다. Framework가 평문 모드를 주면 여기를 바꾼다.
    /// </summary>
    public sealed class MvDevSaveKeyProvider : IMvSaveKeyProvider
    {
        public const string Id = "mv-dev-1";
        public string KeyId => Id;

        public byte[] GetMasterKey()
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes("MoonVillage.DevSave.PublicKey.v1"));
        }
    }

    public sealed class MvSaveKeyMissingException : Exception
    {
        public MvSaveKeyMissingException(string message) : base(message) { }
    }

    /// <summary>빌드 종류별 키 선택. 출시 키 보관 위치는 사용자 결정 대기(D-68).</summary>
    public static class MvSaveKeySource
    {
        /// <summary>
        /// D-68 교체 지점: 출시 빌드 키 공급자. 키 보관 위치(저장소 밖 설정 파일 등)가 정해지면 Boot가 저장 열기 전에 넣는다.
        /// 정해지기 전에는 null이고, 출시 빌드는 저장을 열지 못한다(ProtectionFailed).
        /// </summary>
        public static IMvSaveKeyProvider ReleaseProvider { get; set; }

        public static IMvSaveKeyProvider ForCurrentBuild()
        {
            if (Debug.isDebugBuild) return new MvDevSaveKeyProvider();
            return ReleaseProvider ?? throw new MvSaveKeyMissingException("출시 빌드 저장 키 공급자가 없습니다(D-68 결정 대기).");
        }

        internal static LocalSaveCodec CreateCodec(IMvSaveKeyProvider provider)
        {
            if (provider == null) throw new MvSaveKeyMissingException("저장 키 공급자가 없습니다.");
            byte[] key = provider.GetMasterKey();
            try { return LocalSaveCodec.CreateEncrypted(new LocalSaveEncryption(provider.KeyId, key)); }
            finally { if (key != null) Array.Clear(key, 0, key.Length); }
        }
    }

    /// <summary>
    /// PlayerPrefs 접근 자리. Framework <c>ILocalSavePreferences</c>에 손상 칸 정리용 삭제를 더했다.
    /// 시험은 메모리 구현을 넣는다(실제 PlayerPrefs를 쓰지 않는다).
    /// </summary>
    public interface IMvSavePrefs : ILocalSavePreferences
    {
        void DeleteKey(string key);
    }

    public sealed class MvPlayerPrefs : IMvSavePrefs
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public string GetString(string key) => PlayerPrefs.GetString(key);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Save() => PlayerPrefs.Save();
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
    }
}
