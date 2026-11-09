namespace MV.UI
{
    /// <summary><c>UIPopup_Map</c> 컨트롤러(05 §5.10, 층 550 전체 패널). 화면 코드는 <see cref="MapPresenter"/>.</summary>
    public class MapPopup : MetaPanelPopup
    {
        protected override int Layer => MvUiLayer.FullPanel;
        protected override MetaPanelPresenter CreatePresenter() => new MapPresenter();
    }
}
