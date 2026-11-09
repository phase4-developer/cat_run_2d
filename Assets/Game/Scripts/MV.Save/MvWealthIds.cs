using System;
using System.Collections.Generic;
using System.Linq;

namespace MV.Save
{
    /// <summary>
    /// 재화 id(D-80, <c>01_wealth</c> <c>1001001</c> 식)와 Framework <c>WealthContainer</c> 행 키(type)의 대응.
    /// 저장은 Framework 규격대로 type별 quantity 행으로 둔다. 게임 코드는 id로 묻는다.
    /// 표(MV-DATA <c>01_wealth</c>)가 들어오면 <c>WealthTable.GetId/Get(type)</c>으로 바꿀 자리.
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

        private static readonly Dictionary<int, string> TypeById = new Dictionary<int, string>
        {
            [Gold] = "gold", [Dia] = "dia", [Souls] = "souls", [RbToken] = "rbToken", [Medal] = "medal",
            [PetFood] = "petFood", [Yarn] = "yarn", [GodCoin] = "godCoin", [GodShard] = "godShard",
            [FriendCoin] = "friendCoin", [FriendUp] = "friendUp", [FriendAny] = "friendAny", [StarCandy] = "starCandy",
            [BoxPaper] = "boxPaper", [BoxWood] = "boxWood", [BoxGold] = "boxGold", [NoAds] = "noAds",
        };

        private static readonly Dictionary<string, int> IdByType = TypeById.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

        public static IReadOnlyList<int> AllIds { get; } = Array.AsReadOnly(TypeById.Keys.OrderBy(id => id).ToArray());

        /// <summary>행 키(type). 모르는 id는 null.</summary>
        public static string TypeOf(int id) => TypeById.TryGetValue(id, out string type) ? type : null;

        /// <summary>id. 모르는 type은 0.</summary>
        public static int IdOf(string type) => type != null && IdByType.TryGetValue(type, out int id) ? id : 0;
    }
}
