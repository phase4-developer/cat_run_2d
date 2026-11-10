using System;
using System.Collections.Generic;
using System.Linq;

namespace MV.Save
{
    /// <summary>
    /// 재화 id(D-80, <c>01_wealth</c> <c>1001001</c> 식)와 Framework <c>WealthContainer</c> 행 키(type)의 대응.
    /// 저장은 Framework 규격대로 type별 quantity 행으로 둔다. 게임 코드는 id로 묻는다.
    /// 상수는 <c>01_wealth</c>와 같아야 한다: Boot 6단계가 <see cref="Mismatches"/>로 표와 대조하고(어긋나면 시작 중단),
    /// EditMode 시험이 TSV 원문과 대조한다. 행이 더해지면 이 목록도 함께 고친다.
    /// </summary>
    public static class MvWealthIds
    {
        public const int Gold = 1001001;
        public const int Dia = 1001002;
        public const int Souls = 1001003;
        public const int RbToken = 1001004;
        public const int Medal = 1001005;
        public const int PetFood = 1001006;
        public const int Yarn = 1001007;
        public const int GodCoin = 1001008;
        public const int GodShard = 1001009;
        public const int FriendCoin = 1001010;
        public const int FriendUp = 1001011;
        public const int FriendAny = 1001012;
        public const int StarCandy = 1001013;
        public const int BoxPaper = 1001014;
        public const int BoxWood = 1001015;
        public const int BoxGold = 1001016;
        public const int NoAds = 1001017;
        /// <summary>보라 마법석(유료, MV-SPEC-02 Q-30). 쓰는 곳은 BM 결정 뒤.</summary>
        public const int PurpleGem = 1001018;

        private static readonly Dictionary<int, string> TypeById = new Dictionary<int, string>
        {
            [Gold] = "gold", [Dia] = "dia", [Souls] = "rescueCat", [RbToken] = "goldenFish", [Medal] = "medal",
            [PetFood] = "petFood", [Yarn] = "yarn", [GodCoin] = "godCoin", [GodShard] = "godShard",
            [FriendCoin] = "friendCoin", [FriendUp] = "friendUp", [FriendAny] = "friendAny", [StarCandy] = "starCandy",
            [BoxPaper] = "boxPaper", [BoxWood] = "boxWood", [BoxGold] = "boxGold", [NoAds] = "noAds", [PurpleGem] = "purpleGem",
        };

        private static readonly Dictionary<string, int> IdByType = TypeById.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

        public static IReadOnlyList<int> AllIds { get; } = Array.AsReadOnly(TypeById.Keys.OrderBy(id => id).ToArray());

        /// <summary>행 키(type). 모르는 id는 null.</summary>
        public static string TypeOf(int id) => TypeById.TryGetValue(id, out string type) ? type : null;

        /// <summary>id. 모르는 type은 0.</summary>
        public static int IdOf(string type) => type != null && IdByType.TryGetValue(type, out int id) ? id : 0;

        /// <summary>표 행(id, type)과 이 목록의 차이. 같으면 빈 목록.</summary>
        public static IReadOnlyList<string> Mismatches(IEnumerable<KeyValuePair<int, string>> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var problems = new List<string>();
            var seen = new HashSet<int>();
            foreach (KeyValuePair<int, string> row in rows)
            {
                seen.Add(row.Key);
                string type = TypeOf(row.Key);
                if (type == null) problems.Add("표에만 있는 재화 " + row.Key + "(" + row.Value + ")");
                else if (!string.Equals(type, row.Value, StringComparison.Ordinal)) problems.Add("재화 " + row.Key + " type 다름: 표 " + row.Value + ", 코드 " + type);
            }
            foreach (int id in AllIds)
                if (!seen.Contains(id)) problems.Add("코드에만 있는 재화 " + id + "(" + TypeOf(id) + ")");
            return problems.AsReadOnly();
        }
    }
}
