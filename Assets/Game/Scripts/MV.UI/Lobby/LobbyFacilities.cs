using MV.Data;
using MV.Meta;

namespace MV.UI
{
    /// <summary>
    /// 로비 아지트 시설 단계(정본 <c>azStages</c> L3468~3471). 로비 월드(<c>LobbyStage.SetFacility</c>, MV.View)에 넘길 값만 계산하고
    /// 월드는 만지지 않는다(연결은 게임 루프, <see cref="ILobbySource.FacilityStage"/>).
    /// 문턱 숫자는 정본 상수다(표 열 없음 → MV-DATA 변경 요청 후보).
    /// </summary>
    public static class LobbyFacilities
    {
        public const string Tower = "tower";
        public const string Scratch = "scratch";
        public const string Rack = "rack";
        public const string Basket = "basket";
        public const string Stove = "stove";

        /// <summary>시설 key 전부(<c>LobbyIso.Slots</c>와 같은 key).</summary>
        public static readonly string[] Keys = { Stove, Tower, Rack, Scratch, Basket };

        /// <summary>
        /// 시설 단계(0 = 잠김, 1~3). <paramref name="extra"/> = 건조대 생선 수(정본 <c>fish = min(6, 1 + floor(inc/2))</c>), 그 밖 0.
        /// 메타가 없으면 구매 수 0으로 본다(캣타워·스크래쳐·건조대·바구니 1, 난로 0).
        /// </summary>
        public static int Stage(IMetaUiPort port, string key, out int extra)
        {
            extra = 0;
            port = port ?? MvMetaUiPort.Instance;
            var t = port.Tables;
            switch (key)
            {
                case Tower:
                {
                    int kl = t != null ? MvMetaRules.StairStart(t, port.Level(MvGrowth.Lobby, MvMetaRules.LobbyStair)).Lv : 1;
                    return TowerStage(kl);
                }
                case Scratch:
                {
                    int wt = t != null ? MvMetaRules.WeaponLevel(t, port.Level(MvGrowth.Lobby, MvMetaRules.LobbyWeapon)).Lv : 1;
                    return wt >= 6 ? 3 : wt >= 3 ? 2 : 1;
                }
                case Rack:
                {
                    int inc = port.Level(MvGrowth.Lobby, MvMetaRules.LobbyIncome);
                    extra = System.Math.Min(6, 1 + inc / 2);
                    return RackStage(inc);
                }
                case Basket:
                {
                    int toy = 0;
                    if (t != null)
                        foreach (var row in t.AreaUpgrade.Sorted) toy += port.Level(MvGrowth.AreaUp, row.key);
                    return toy >= 12 ? 3 : toy >= 4 ? 2 : 1;
                }
                case Stove:
                {
                    if (!port.RebirthOpen) return 0;
                    int rb = port.RebirthLevel;
                    return rb >= 3 ? 3 : rb >= 1 ? 2 : 1;
                }
                default: return 0;
            }
        }

        /// <summary>캣타워 단계(시작 계단 Lv 기준, 정본 문턱 — 표 열 없음). 로비 아이콘(LobbyPresenter)도 이것을 쓴다.</summary>
        public static int TowerStage(int stairLv) => stairLv >= 4 ? 3 : stairLv >= 2 ? 2 : 1;

        /// <summary>생선 건조대 단계(구매 수 기준, 정본 문턱 — 표 열 없음). 로비 아이콘(LobbyPresenter)도 이것을 쓴다.</summary>
        public static int RackStage(int incomeBought) => incomeBought >= 15 ? 3 : incomeBought >= 5 ? 2 : 1;

        /// <summary>강화 효과 종류 → 구매 때 반짝일 시설(정본 L2644 <c>{stair:'tower', weapon:'scratch', income:'rack'}</c>).</summary>
        public static string OfEffect(LobbyEffectType effect)
        {
            switch (effect)
            {
                case LobbyEffectType.StartStairLv: return Tower;
                case LobbyEffectType.DeckAdd: return Scratch;
                default: return Rack;
            }
        }
    }
}
