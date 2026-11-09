namespace MV.UI
{
    /// <summary><c>UIPopup_GodSelect</c> 컨트롤러(05 §5.7, 층 500, 막 누르면 닫힘). 화면 코드는 <see cref="GodSelectPresenter"/>.</summary>
    public class GodSelectPopup : MetaPanelPopup
    {
        protected override MetaPanelPresenter CreatePresenter() => new GodSelectPresenter();
    }
}
