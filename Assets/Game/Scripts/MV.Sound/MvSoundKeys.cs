using System.Globalization;

namespace MV.Sound
{
    /// <summary>
    /// 정본 SND 키 중 표 열이 없어 코드가 직접 부르는 것(03 §3.9.3 key). 표 열(hitSound·breakSound·passSound·stealSound·cardSound·
    /// enterSound·gainSound·bgm)에서 오는 키는 그 표 행이 정한다.
    /// </summary>
    public static class MvSoundKeys
    {
        public const string Throw = "throw";
        public const string Hit = "hit";
        public const string Crit = "crit";
        public const string Wood = "wood";
        public const string Lvup = "lvup";
        public const string Tierup = "tierup";
        public const string Land = "land";
        public const string Tick = "tick";
        public const string Card = "card";
        public const string Merge = "merge";
        public const string Buy = "buy";
        public const string Click = "click";
        public const string Warn = "warn";
        public const string Fail = "fail";
        public const string Win = "win";
        public const string BgmLobby = "bgm_lobby";
        public const string BgmRun = "bgm_run";

        /// <summary>변형 소리 key(07 §7.11.1): <c>tierup</c>, 3 → <c>tierup_3</c>.</summary>
        public static string Variant(string baseKey, int n) => baseKey + "_" + n.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// GAP 묶음 키(03 §3.9.5) = key 끝의 <c>_숫자</c>를 뗀 것. 정본은 인자와 상관없이 <c>tierup</c> 하나로 간격을 잰다(L1678).
        /// </summary>
        public static string GapGroup(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            int i = key.Length - 1;
            while (i >= 0 && key[i] >= '0' && key[i] <= '9') i--;
            if (i < key.Length - 1 && i > 0 && key[i] == '_') return key.Substring(0, i);
            return key;
        }
    }
}
