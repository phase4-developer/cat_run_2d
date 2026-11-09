using System;
using System.Collections.Generic;
using System.Globalization;

namespace MV.Save
{
    /// <summary>
    /// 저장 기본값이 읽는 게임 설정. 값은 정본(프로토)과 07의 기본값이다.
    /// 표가 들어오면(MV-DATA <c>00_config</c>·<c>08_adPlacement</c>) Boot가 그 값으로 채워 넘긴다 — 바꿀 자리.
    /// </summary>
    public sealed class MvSaveConfig
    {
        /// <summary><c>00_config.startDeck</c>(정본 L844 DEF.deck). 인덱스 = 무기 등급, 0 미사용.</summary>
        public int[] StartDeck { get; set; } = { 0, 1, 0, 0, 0, 0, 0, 0, 0 };
        /// <summary><c>00_config.startGodKey</c>(정본 L844 DEF.god).</summary>
        public string StartGodKey { get; set; } = "thunder";
        /// <summary>07 <c>CP_MAX</c>. 체크포인트 상한.</summary>
        public int CpMax { get; set; } = 2;
        /// <summary>07 <c>FLOOR_COUNT</c>. 벽 기록의 Floor 수.</summary>
        public int FloorCount { get; set; } = 3;
        /// <summary><c>00_config.dataVersion</c>. <c>mvMeta.tableVersion</c>에 남긴다.</summary>
        public int TableVersion { get; set; } = 1;
        /// <summary><c>00_config.adDayUtcOffsetHour</c>. 0 = UTC 0시(D-29·D-77).</summary>
        public int DayUtcOffsetHour { get; set; }
        /// <summary><c>08_adPlacement.dailyLimit</c>(key별). 정본 <c>adDay()</c>의 dia 3·skip 4. 프로토 가져오기의 사용 수 환산에만 쓴다.</summary>
        public IReadOnlyDictionary<string, int> AdDailyLimits { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal) { ["dia"] = 3, ["skip"] = 4 };

        public static MvSaveConfig CreateDefault() => new MvSaveConfig();

        internal void Validate()
        {
            if (StartDeck == null || StartDeck.Length == 0) throw new ArgumentException("StartDeck이 비었습니다.");
            foreach (int count in StartDeck)
                if (count < 0) throw new ArgumentException("StartDeck 값은 0 이상이어야 합니다.");
            if (string.IsNullOrWhiteSpace(StartGodKey)) throw new ArgumentException("StartGodKey가 비었습니다.");
            if (CpMax < 0 || FloorCount < 1 || TableVersion < 0) throw new ArgumentException("CpMax·FloorCount·TableVersion 범위가 잘못됐습니다.");
            if (DayUtcOffsetHour < -23 || DayUtcOffsetHour > 23) throw new ArgumentException("DayUtcOffsetHour는 -23~23입니다.");
            if (AdDailyLimits == null) throw new ArgumentException("AdDailyLimits가 없습니다.");
        }

        internal int AdDailyLimit(string placementKey) => AdDailyLimits.TryGetValue(placementKey, out int limit) ? limit : 0;
    }

    /// <summary>저장 시각 규칙(09 §9.1 원칙 4). 시각은 UTC Unix 밀리초, 하루 경계는 <c>YYYY-MM-DD</c>.</summary>
    public static class MvSaveTime
    {
        public static long UtcNowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>하루 키 = (UTC + offset 시간)의 날짜. offset 0은 정본 <c>toISOString().slice(0,10)</c>과 같다.</summary>
        public static string DayKey(long utcMs, int utcOffsetHour)
            => DateTimeOffset.FromUnixTimeMilliseconds(utcMs).UtcDateTime.AddHours(utcOffsetHour).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        internal static bool IsDayKey(string value)
            => value != null && value.Length == 10 && DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
