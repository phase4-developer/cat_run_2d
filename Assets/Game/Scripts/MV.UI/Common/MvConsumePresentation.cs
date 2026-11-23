using jjevol;
using MV.Data;
using FwConsume = jjevol.ConsumeData;

namespace MV.UI
{
    /// <summary>
    /// Framework <see cref="IConsumePresentationProvider"/> 게임 구현(com.jjevol.economy 가이드 「표시 metadata 공급」). 아이콘은
    /// <see cref="MvConsumeIcon"/>(07_consumeType), 이름은 언어팩 <c>name:&lt;id&gt;</c>. <c>PriceLabel</c>·<c>PriceBtn</c>이 이 값으로 아이콘을 그린다(MV-UI-ICONSRC).
    /// </summary>
    public sealed class MvConsumePresentation : IConsumePresentationProvider
    {
        readonly MvTables _tables;

        public MvConsumePresentation(MvTables tables) { _tables = tables; }

        static MvConsumePresentation s_current;

        /// <summary>현재 표(<see cref="MvTables.Current"/>)의 Provider. 표가 없으면 null(Framework가 WealthTable 기본을 쓴다).</summary>
        public static MvConsumePresentation Current
        {
            get
            {
                var t = MvTables.Current;
                if (t == null) return null;
                if (s_current == null || s_current._tables != t) s_current = new MvConsumePresentation(t);
                return s_current;
            }
        }

        public bool TryResolve(ConsumeTypeId typeId, int itemId, out ConsumePresentation presentation)
        {
            if (MvConsumeIcon.TryGet(_tables, typeId.Value, itemId, out var atlas, out var sprite))
            {
                presentation = new ConsumePresentation(null, sprite, atlas, MvUiText.Name(itemId, ""));
                return true;
            }
            presentation = default;
            return false;
        }

        public ConsumePresentation Resolve(FwConsume data) =>
            data != null && TryResolve(data.TypeId, data.ItemId, out var p) ? p : default;
    }
}
