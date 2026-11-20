#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MV.Meta;
using MV.Save;

namespace MV.UI
{
    /// <summary>
    /// 에디터 전용 재화 지급 치트(MV-DEV-CHEAT-wealth). 빌드에는 들어가지 않는다(파일 전체 <c>UNITY_EDITOR</c>).
    /// 지급은 테스트 패널(05 §5.14)과 같은 메타 요청 <see cref="MvDebugRequests.Grant"/>로만 보낸다 —
    /// 백엔드가 Framework 재화 서비스로 확인하고 저장하므로 로비 재화 칸·<c>PriceBtn</c> 잔액이 그대로 따라온다. 저장을 직접 쓰지 않는다.
    /// 창은 <see cref="MvWealthCheatWindow"/>.
    /// </summary>
    public static class MvWealthCheat
    {
        /// <summary>치트 대상 재화(<c>01_wealth.type</c>): 골드·다이아.</summary>
        public static IReadOnlyList<string> Types { get; } = Array.AsReadOnly(new[] { "gold", "dia" });

        /// <summary><paramref name="wealthType"/>(<c>01_wealth.type</c>)을 <paramref name="amount"/>만큼 지급한다. 0 이하·모르는 재화는 보내지 않고 거절한다.</summary>
        public static MetaResult Grant(IMetaUiPort port, string wealthType, long amount)
        {
            if (port == null || !port.Ready) return MetaResult.Fail(MvMetaErrors.NoStore);
            int id = MvWealthIds.IdOf(wealthType);
            if (id == 0) return MetaResult.Fail(MvMetaErrors.UnknownKey);
            if (amount <= 0) return MetaResult.Fail(MvMetaErrors.BadParams);
            return port.SendDebug(MvDebugRequests.Grant, id.ToString(), amount);
        }

        /// <summary>지금 잔액. 메타가 없으면 0.</summary>
        public static long Balance(IMetaUiPort port, string wealthType)
        {
            int id = MvWealthIds.IdOf(wealthType);
            return port == null || id == 0 ? 0 : port.Wealth(id);
        }
    }
}
#endif
