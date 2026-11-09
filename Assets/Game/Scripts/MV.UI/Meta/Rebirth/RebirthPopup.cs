namespace MV.UI
{
    /// <summary><c>UIPopup_Rebirth</c> 컨트롤러(05 §5.9, 층 500). 화면 코드는 <see cref="RebirthPresenter"/>. 잠김이면 <see cref="MvUiNav.OpenRebirthAsync"/>가 토스트만 낸다.</summary>
    public class RebirthPopup : MetaPanelPopup
    {
        protected override MetaPanelPresenter CreatePresenter() => new RebirthPresenter();
    }
}
