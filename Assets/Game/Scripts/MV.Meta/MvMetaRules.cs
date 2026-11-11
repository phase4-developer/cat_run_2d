using System;
using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MvConsume = MV.Data.ConsumeData;

namespace MV.Meta
{
    /// <summary>단계 환산 결과(정본 <c>kegStart</c>·<c>wLevel</c>의 <c>{lv, step, need}</c>).</summary>
    public readonly struct MvStepLevel
    {
        public readonly int Lv;
        public readonly int Step;
        public readonly int Need;
        public MvStepLevel(int lv, int step, int need) { Lv = lv; Step = step; Need = need; }
        public override string ToString() => $"Lv{Lv} {Step}/{Need}";
    }

    /// <summary>
    /// 메타 공식(04 §4.11~4.12). 상태를 바꾸지 않는 순수 계산이고 숫자는 표에서만 읽는다.
    /// 화면·런 준비(RunSetup)도 이 계산을 읽는다.
    /// </summary>
    public static class MvMetaRules
    {
        public const string LobbyStair = "stair";
        public const string LobbyWeapon = "weapon";
        public const string LobbyIncome = "income";
        public const string RebirthKey = "rebirth";
        /// <summary>결과 광고 자리 key(<c>08_adPlacement</c>, 07 §7.13.3 필수 key).</summary>
        public const string CollectAdKey = "collect3";

        /// <summary>
        /// 결과 광고 배수 = <c>08_adPlacement[collect3].mul</c> 하나만 읽는다(00_config.collectAdMul 삭제, MV-SPEC2-FIX1).
        /// 행이 없거나 mul ≤ 0이면 1(배수 없음 — 검증기가 행 없음을 오류로 막는다).
        /// </summary>
        public static int CollectAdMul(MvTables t)
        {
            var row = t?.AdPlacement.ByKey(CollectAdKey);
            return row != null && row.mul > 0 ? row.mul : 1;
        }

        /// <summary>JS <c>Math.round</c>(§4.0.2).</summary>
        public static double JsRound(double x) => Math.Floor(x + 0.5);

        /// <summary>구매 수 P에서 다음 구매 비용(<c>16_levelPrice[group][P+1]</c>). 행이 없으면 null(MAX).</summary>
        public static IReadOnlyList<MvConsume> NextPrice(MvTables t, int levelPriceGroup, int bought)
            => t.LevelPrice.Get(levelPriceGroup, bought + 1)?.price;

        /// <summary>
        /// 캣타워 구매 수 → 런 시작 게이지(정본 <c>kegStart</c> L809~812). 단계 필요 수는 <c>11_stairLevel.need</c>,
        /// 상한 Lv은 <c>11_stairLevel</c> lv 최댓값(<see cref="MvTables.StairMaxLv"/>).
        /// </summary>
        public static MvStepLevel StairStart(MvTables t, int bought)
        {
            int maxLv = t.StairMaxLv();
            return Climb(t.StairLevel.Datas.Select(r => (r.lv, r.need)), bought, maxLv);
        }

        /// <summary>스크래쳐 구매 수 → 무기 Level(정본 <c>wLevel</c>·<c>baseTier</c> L813~817). 상한은 <c>12_weaponLevel</c> 마지막 lv.</summary>
        public static MvStepLevel WeaponLevel(MvTables t, int bought)
        {
            var rows = t.WeaponLevel.Datas.Select(r => (r.lv, r.need)).ToList();
            int maxLv = rows.Count == 0 ? 1 : rows.Max(r => r.lv);
            return Climb(rows, bought, maxLv);
        }

        static MvStepLevel Climb(IEnumerable<(int lv, int need)> rows, int bought, int maxLv)
        {
            var needByLv = rows.ToDictionary(r => r.lv, r => r.need);
            int Need(int lv) => needByLv.TryGetValue(lv, out int n) ? n : int.MaxValue;
            int level = 1, p = Math.Max(0, bought);
            while (level < maxLv && p >= Need(level)) { p -= Need(level); level++; }
            return new MvStepLevel(level, p, Need(level));
        }

        /// <summary>
        /// 덱에 등급 카드 1장을 넣고 3장 머지(정본 <c>deckAdd</c> L819). 상한 등급 = 덱 길이 − 1(정본 8).
        /// 돌려주는 값은 마지막으로 머지되어 생긴 등급(없으면 0).
        /// </summary>
        public static int DeckAdd(int[] deck, int tier)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            int top = deck.Length - 1;
            if (tier < 1 || tier > top) throw new ArgumentOutOfRangeException(nameof(tier));
            deck[tier]++;
            int merged = 0;
            while (tier < top && deck[tier] >= 3) { deck[tier] -= 3; tier++; deck[tier]++; merged = tier; }
            return merged;
        }

        /// <summary>생선 건조대 goldMul = 1 + <c>INCOME_PER_LV</c> × P(04 §4.12.1).</summary>
        public static double GoldMul(MvTables t, int incomeBought) => 1 + t.RunConfig.D(RunConfigKeys.INCOME_PER_LV) * incomeBought;

        /// <summary>목숨 조각 = TOK_BASE + floor(souls / TOK_SOUL_DIV) + (gate ? TOK_GATE : 0) + (개방 ? TOK_OPEN : 0)(§4.11.1).</summary>
        public static int Tokens(MvTables t, int runSouls, bool gate, bool opened)
        {
            var c = t.RunConfig;
            int div = Math.Max(1, c.I(RunConfigKeys.TOK_SOUL_DIV));
            return c.I(RunConfigKeys.TOK_BASE) + (int)Math.Floor((double)Math.Max(0, runSouls) / div)
                   + (gate ? c.I(RunConfigKeys.TOK_GATE) : 0) + (opened ? c.I(RunConfigKeys.TOK_OPEN) : 0);
        }

        /// <summary>이 Area(0기반)에서 다음 Area를 여는 누적 생선(<c>302_area.needSouls</c>). 행이 없으면 int.MaxValue.</summary>
        public static int NeedSouls(MvTables t, int area)
        {
            foreach (var row in t.Area.Datas)
                if (row.areaNo == area + 1) return row.needSouls;
            return int.MaxValue;
        }

        /// <summary>마지막 Area(<c>area ≥ PLAYABLE_AREAS − 1</c>, D-26).</summary>
        public static bool IsLastArea(MvTables t, int area) => area >= t.RunConfig.I(RunConfigKeys.PLAYABLE_AREAS) - 1;

        /// <summary>Zone 0기반 위치(<c>302_area.zoneId</c> → 301 sort 순).</summary>
        public static int ZoneIndex(MvTables t, int area) => t.ZoneIndexOfArea(area + 1);
    }
}
