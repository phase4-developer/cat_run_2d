namespace MV.UI
{
    /// <summary><c>UIPopup_Research</c> 컨트롤러(08 §8.5.3, 05 §5.16 공통 틀, 층 500, 막 누르면 안 닫힘). 화면 코드는 <see cref="ResearchPresenter"/>.</summary>
    public class ResearchPopup : MetaPanelPopup
    {
        protected override MetaPanelPresenter CreatePresenter() => new ResearchPresenter();
    }
}
