using System;
using System.Collections.Generic;
using System.Linq;
using jjevol;

namespace MV.Save
{
    /// <summary>저장 이름과 스키마 id(09 §9.5.1).</summary>
    public static class MvSaveSchema
    {
        public const string ProfileId = "p0";
        public const string KeyNamespace = "moonvillage.progress.v1";

        /// <summary>정본(웹 프로토) <c>catescape_moon_v1</c> 평면 JSON. v0→v1 고리가 D-67 변환 함수다.</summary>
        public const string V0 = "mv:local:v0";
        public const string V1 = "mv:local:v1";
        public const string Current = V1;

        /// <summary>저장 JSON 최상위의 스키마 칸(<c>_containerSchema</c>).</summary>
        public const string SchemaKey = RepositoryStorageAdapter.SchemaKey;
    }

    /// <summary>컨테이너 dataKey(09 §9.2). 저장 JSON 최상위 키다.</summary>
    public static class MvSaveKeys
    {
        public const string Wealth = WealthContainer.PayloadKey;
        public const string Progress = "mvProgress";
        public const string Lobby = "mvLobby";
        public const string AreaUp = "mvAreaUp";
        public const string Rebirth = "mvRebirth";
        public const string God = "mvGod";
        public const string Unlock = "mvUnlock";
        public const string Daily = "mvDaily";
        public const string PlayRes = "mvPlayRes";
        public const string Mode = "mvMode";
        public const string Quest = "mvQuest";
        public const string Research = "mvResearch";
        public const string Shop = "mvShop";
        public const string Pet = "mvPet";
        public const string Friend = "mvFriend";
        public const string Gear = "mvGear";
        public const string Pass = "mvPass";
        public const string Blessing = "mvBlessing";
        public const string Settings = "mvSettings";
        public const string Meta = "mvMeta";

        private static readonly string[] AllKeys =
        {
            Wealth, Progress, Lobby, AreaUp, Rebirth, God, Unlock, Daily, PlayRes, Mode, Quest, Research, Shop,
            Pet, Friend, Gear, Pass, Blessing, Settings, Meta
        };

        /// <summary>등록하는 모든 컨테이너. 2차 시스템도 지금 빈 상태로 등록한다(원칙 6).</summary>
        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(AllKeys);

        /// <summary>진행 초기화(D-69)가 기본값으로 바꾸는 키. <c>mvSettings</c>만 유지한다.</summary>
        public static IReadOnlyList<string> ProgressResetKeys { get; } = Array.AsReadOnly(AllKeys.Where(key => key != Settings).ToArray());

        /// <summary>프로토 저장 가져오기(D-67)가 바꾸는 키. 소리(<c>ce_mute</c>)는 대상이 아니다.</summary>
        public static IReadOnlyList<string> ProtoImportKeys { get; } = Array.AsReadOnly(AllKeys.Where(key => key != Settings).ToArray());
    }

    /// <summary>
    /// 사건별 저장 묶음(09 §9.4 「키」 열). MV.Meta 진입점이 <c>MvSaveStore.ApplyAndSave</c>에
    /// 그대로 넘긴다. <c>mvMeta</c>(마지막 저장 시각)는 저장소가 매번 덧붙인다. 상태 계산은 MV.Meta 몫이다.
    /// </summary>
    public static class MvSaveTriggers
    {
        /// <summary>결과 정산. Area 개방(pass)이면 로비 강화·덱·Area 강화·해금도 같은 묶음.</summary>
        public static string[] Settle(bool areaPassed) => areaPassed
            ? new[] { MvSaveKeys.Wealth, MvSaveKeys.Progress, MvSaveKeys.Rebirth, MvSaveKeys.Lobby, MvSaveKeys.AreaUp, MvSaveKeys.Unlock }
            : new[] { MvSaveKeys.Wealth, MvSaveKeys.Progress, MvSaveKeys.Rebirth };
        public static string[] SelectGod() => new[] { MvSaveKeys.God };
        public static string[] BuyAreaUp() => new[] { MvSaveKeys.Wealth, MvSaveKeys.AreaUp };
        public static string[] BuyPerk() => new[] { MvSaveKeys.Wealth, MvSaveKeys.Rebirth };
        public static string[] BuyLobbyUp() => new[] { MvSaveKeys.Wealth, MvSaveKeys.Lobby };
        /// <summary>부활(09 §9.2 「부활 때」 열에서 값이 바뀌는 키).</summary>
        public static string[] Rebirth() => new[] { MvSaveKeys.Wealth, MvSaveKeys.Progress, MvSaveKeys.Lobby, MvSaveKeys.AreaUp, MvSaveKeys.Rebirth };
        /// <summary>광고 캣닢 보상.</summary>
        public static string[] GrantAdDia() => new[] { MvSaveKeys.Wealth, MvSaveKeys.Daily };
        /// <summary>광고 부활 쿨타임 단축.</summary>
        public static string[] GrantAdSkip() => new[] { MvSaveKeys.Rebirth, MvSaveKeys.Daily };
        public static string[] ToggleMute() => new[] { MvSaveKeys.Settings };
        /// <summary>해금 연출 봄 / 컨텐츠 메뉴 닫음(마지막 탭 포함).</summary>
        public static string[] MarkSeen() => new[] { MvSaveKeys.Unlock, MvSaveKeys.Settings };
        public static string[] ResetProgress() => MvSaveKeys.ProgressResetKeys.ToArray();
    }
}
