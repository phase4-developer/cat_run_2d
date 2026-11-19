namespace MV.UI
{
    /// <summary><c>UIPopup_Rebirth</c> 컨트롤러(05 §5.9, 층 500). 화면 코드는 <see cref="RebirthPresenter"/>. 잠겨 있어도 열고 Lock을 보인다(MV-UI2-REBIRTH). 겉은 <see cref="RebirthDesignView"/>.</summary>
    public class RebirthPopup : MetaPanelPopup
    {
        protected override MetaPanelPresenter CreatePresenter() => new RebirthPresenter();
    }
}
