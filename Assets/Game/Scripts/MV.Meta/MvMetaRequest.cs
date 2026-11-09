using System;
using Newtonsoft.Json.Linq;

namespace MV.Meta
{
    /// <summary>
    /// 영속 상태를 바꾸는 요청 1건(PROJECT_CONTEXT 「AI가 따를 규칙」 서버 판정 구조). 이름·파라미터는 서버 함수와 같게 둔다.
    /// Framework 대체: platform.network 고정본 없음 → <c>NetworkRequest</c>(Name·Params·DataKeys)와 같은 모양.
    /// 고정본이 나오면 이 클래스를 <c>NetworkRequest</c> 하위 클래스로 바꾸고 <see cref="MvMeta"/>가 <c>NetworkManager</c>로 보낸다.
    /// </summary>
    public sealed class MvMetaRequest
    {
        public string Name { get; }
        public JObject Params { get; }

        public MvMetaRequest(string name, JObject parameters = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("요청 이름이 필요합니다.", nameof(name));
            Name = name;
            Params = parameters ?? new JObject();
        }

        public override string ToString() => Name + " " + Params.ToString(Newtonsoft.Json.Formatting.None);
    }

    /// <summary>요청 이름(서버 함수 이름과 같게 유지).</summary>
    public static class MvMetaRequestNames
    {
        public const string Settle = "meta.settle";
        public const string BuyLobbyUp = "meta.buyLobbyUp";
        public const string BuyAreaUp = "meta.buyAreaUp";
        public const string BuyPerk = "meta.buyPerk";
        public const string Rebirth = "meta.rebirth";
        public const string SelectGod = "meta.selectGod";
        public const string GrantAd = "meta.grantAd";
        public const string UseAd = "meta.useAd";
        public const string RecordModePlay = "meta.recordModePlay";
        public const string MarkSeen = "meta.markSeen";
    }

    /// <summary>거절 코드(<c>NetworkRejection.Code</c> 자리).</summary>
    public static class MvMetaErrors
    {
        public const string NoStore = "meta.noStore";
        public const string UnknownRequest = "meta.unknownRequest";
        public const string BadParams = "meta.badParams";
        public const string UnknownKey = "meta.unknownKey";
        public const string Max = "meta.max";
        public const string NotEnough = "meta.notEnough";
        public const string AlreadySettled = "meta.alreadySettled";
        public const string BadMul = "meta.badMul";
        public const string Locked = "meta.locked";
        public const string Cooldown = "meta.cooldown";
        public const string NoCooldown = "meta.noCooldown";
        public const string DailyLimit = "meta.dailyLimit";
        public const string AdNotWatched = "meta.adNotWatched";
        public const string NoReward = "meta.noReward";
        public const string SaveFailed = "meta.saveFailed";
    }

    /// <summary>
    /// 요청 결과. 성공이면 <see cref="Data"/>(컨테이너 dataKey → 새 값, 병합)를 저장소에 한 번에 반영한 뒤 돌려준다.
    /// <see cref="Result"/>는 화면용 부가 정보(머지 등급, 새 해금 등).
    /// </summary>
    public sealed class MvMetaResponse
    {
        public bool Ok => Code == null;
        public string Code { get; }
        public string Message { get; }
        public JObject Data { get; }
        public JObject Result { get; }

        private MvMetaResponse(string code, string message, JObject data, JObject result)
        {
            Code = code; Message = message; Data = data ?? new JObject(); Result = result ?? new JObject();
        }

        public static MvMetaResponse Success(JObject data, JObject result = null) => new MvMetaResponse(null, null, data, result);
        public static MvMetaResponse Reject(string code, string message = null) => new MvMetaResponse(code ?? MvMetaErrors.BadParams, message, null, null);

        public override string ToString() => Ok ? "OK " + Result.ToString(Newtonsoft.Json.Formatting.None) : Code + " " + Message;
    }

    /// <summary>
    /// 요청 처리기. 지금은 로컬 백엔드(<see cref="MvLocalMetaBackend"/>)만 있다.
    /// Framework 대체: platform.network <c>INetworkLocalService</c>. 서버 전환은 이 구현만 바꾸고 요청·호출 코드는 그대로 둔다.
    /// 처리기는 상태를 바꾸지 않는다 — 새 값만 계산해 응답으로 돌려준다(반영은 <see cref="MvMeta"/>).
    /// </summary>
    public interface IMvMetaBackend
    {
        MvMetaResponse Handle(MvMetaRequest request);
    }
}
