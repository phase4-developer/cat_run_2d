namespace MV.UI
{
    /// <summary><c>UIPopup_AreaUpgrade</c> 컨트롤러(05 §5.8, 층 500, 막 누르면 안 닫힘). 화면 코드는 <see cref="AreaUpgradePresenter"/>.</summary>
    public class AreaUpgradePopup : MetaPanelPopup
    {
        protected override MetaPanelPresenter CreatePresenter() => new AreaUpgradePresenter();
    }
}
