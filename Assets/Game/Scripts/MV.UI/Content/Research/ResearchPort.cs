using System.Collections.Generic;
using MV.Content;
using MV.Data;
using MV.Meta;

namespace MV.UI
{
    /// <summary>
    /// 연구 화면(08 §8.5.3)이 메타에 묻는 것 중 <see cref="IMetaUiPort"/>에 없는 것. 기본 구현 <see cref="MvResearchUiPort"/>.
    /// 시험은 <see cref="IMetaUiPort"/>와 이 인터페이스를 함께 구현한 가짜 하나를 <see cref="MetaPanelPresenter.SetPort"/>로 넣는다.
    /// </summary>
    public interface IResearchUiPort
    {
        /// <summary>메타 판정 시각(UTC ms, <see cref="MvMeta.Clock"/>).</summary>
        long NowMs { get; }
        /// <summary>받은 단계(<c>mvResearch.lv</c>).</summary>
        int ResearchLevel(int group);
        /// <summary>진행 중 연구(<c>mvResearch.run</c>).</summary>
        MvResearchRun ResearchRun { get; }
        bool CanAfford(IReadOnlyList<ConsumeData> price);
        /// <summary>단계 올리기 시작 요청 1번.</summary>
        MetaResult StartResearch(int group);
        /// <summary>완료 수령 요청 1번.</summary>
        MetaResult ClaimResearch(int group);
    }

    /// <summary><see cref="MvMeta.Current"/>를 매번 읽는 기본 입구. 변경은 <see cref="MvResearch"/>(MV.Meta 요청)로만 한다.</summary>
    public sealed class MvResearchUiPort : IResearchUiPort
    {
        public static readonly MvResearchUiPort Instance = new MvResearchUiPort();

        static MvMeta M => MvMeta.Current;
        static bool Ready => M != null && M.IsReady;

        public long NowMs => M?.Clock.NowMs ?? MvSystemClock.Instance.NowMs;
        public int ResearchLevel(int group) => Ready ? MvResearch.Level(M.Store, group) : 0;
        public MvResearchRun ResearchRun => Ready ? MvResearch.Run(M.Store) : MvResearchRun.None;
        public bool CanAfford(IReadOnlyList<ConsumeData> price) => Ready && M.CanAfford(price);
        public MetaResult StartResearch(int group) => Of(Ready ? MvResearch.Start(M, group) : null);
        public MetaResult ClaimResearch(int group) => Of(Ready ? MvResearch.Claim(M, group) : null);

        static MetaResult Of(MvMetaResponse r)
            => r == null ? MetaResult.Fail(MvMetaErrors.NoStore) : r.Ok ? MetaResult.Success() : MetaResult.Fail(r.Code);
    }
}
