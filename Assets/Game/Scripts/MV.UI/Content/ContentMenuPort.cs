using System.Collections.Generic;
using MV.Meta;

namespace MV.UI
{
    /// <summary>
    /// 컨텐츠 메뉴(05 §5.15)가 메타에 묻는 것 중 <see cref="IMetaUiPort"/>에 없는 것. 기본 구현 <see cref="MvContentMenuPort"/>.
    /// 시험은 <see cref="IMetaUiPort"/>와 이 인터페이스를 함께 구현한 가짜 하나를 <see cref="MetaPanelPresenter.SetPort"/>로 넣는다.
    /// </summary>
    public interface IContentMenuPort
    {
        /// <summary>오늘 이 모드 입장 수(<c>mvDaily.modePlays[key]</c>).</summary>
        int ModePlaysToday(string contentKey);
        /// <summary>입장 기록 요청(메타 판정: <c>400_content.dailyLimit</c> 넘으면 <see cref="MvMetaErrors.DailyLimit"/>).</summary>
        MetaResult RecordModePlay(string contentKey);
        /// <summary>열림 && NEW 안 봄(<c>05_contentOpen.key</c>).</summary>
        bool IsNew(string openKey);
        /// <summary>메뉴를 닫을 때: 보인 NEW key와 마지막 탭 저장.</summary>
        MetaResult MarkMenuSeen(IReadOnlyCollection<string> newSeen, string tab);
        /// <summary>마지막으로 본 탭(<c>mvSettings.contentTab</c>). 없으면 빈 문자열.</summary>
        string LastTab { get; }
        /// <summary>
        /// 입장권(<c>02_playResource</c>) 보유 수. 모르면 −1.
        /// 교체 지점: 메타에 입장권 보유·충전 API가 생기면 그 값을 돌려준다(MV-META 변경 요청). 지금은 늘 −1.
        /// </summary>
        long TicketCount(int playResourceId);
    }

    /// <summary><see cref="MvMeta.Current"/>를 매번 읽는 기본 입구.</summary>
    public sealed class MvContentMenuPort : IContentMenuPort
    {
        public static readonly MvContentMenuPort Instance = new MvContentMenuPort();

        static MvMeta M => MvMeta.Current;
        static bool Ready => M != null && M.IsReady;

        public int ModePlaysToday(string contentKey) => Ready ? M.ModePlaysToday(contentKey) : 0;

        public MetaResult RecordModePlay(string contentKey)
        {
            if (!Ready) return MetaResult.Fail(MvMetaErrors.NoStore);
            var r = M.RecordModePlay(contentKey);
            return r == null ? MetaResult.Fail(null) : r.Ok ? MetaResult.Success() : MetaResult.Fail(r.Code);
        }

        public bool IsNew(string openKey) => Ready && M.IsNew(openKey);

        public MetaResult MarkMenuSeen(IReadOnlyCollection<string> newSeen, string tab)
        {
            if (!Ready) return MetaResult.Fail(MvMetaErrors.NoStore);
            var r = M.MarkSeen(null, newSeen != null && newSeen.Count > 0 ? newSeen : null, tab);
            return r == null ? MetaResult.Fail(null) : r.Ok ? MetaResult.Success() : MetaResult.Fail(r.Code);
        }

        public string LastTab => Ready ? M.Store.Settings.ContentTab ?? "" : "";

        // 교체 지점: 입장권(플레이 자원) 보유·충전은 메타·저장에 아직 없다(MV-META 변경 요청). 메타 API가 생기면 여기서 읽는다.
        public long TicketCount(int playResourceId) => -1;
    }
}
