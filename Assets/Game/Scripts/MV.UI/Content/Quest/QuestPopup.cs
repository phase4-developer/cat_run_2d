namespace MV.UI
{
    /// <summary>퀘스트 팝업 데이터. <see cref="Quest"/>를 비우면 <see cref="MvQuestUiPort.Instance"/>.</summary>
    public sealed class QuestPopupData : MetaPopupData
    {
        public IQuestUiPort Quest;
    }

    /// <summary><c>UIPopup_Quest</c> 컨트롤러(05 §5.16 메타 공통 틀, 층 500, 막 누르면 안 닫힘). 화면 코드는 <see cref="QuestPresenter"/>.</summary>
    public class QuestPopup : MetaPanelPopup
    {
        protected override MetaPanelPresenter CreatePresenter() => new QuestPresenter();

        protected override void OnOpening(MetaPanelPresenter presenter)
        {
            if (presenter is QuestPresenter q) q.SetQuestPort((Data as QuestPopupData)?.Quest);
        }
    }
}
