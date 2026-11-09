namespace MV.UI
{
    /// <summary>
    /// <c>UIPopup_Debug</c> 컨트롤러(05 §5.14, 층 410, 개발 빌드 전용). 화면 코드는 <see cref="DebugPanelPresenter"/>.
    /// 출시 빌드에서는 <see cref="MvUiNav.OpenDebugAsync"/>가 열지 않는다(프리팹을 출시 그룹에 넣지 않는 것은 Addressables 담당).
    /// </summary>
    public class DebugPopup : MetaPanelPopup
    {
        protected override int Layer => MvUiLayer.Debug;
        protected override MetaPanelPresenter CreatePresenter() => new DebugPanelPresenter();
    }
}
