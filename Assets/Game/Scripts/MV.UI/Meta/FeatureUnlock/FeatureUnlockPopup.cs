namespace MV.UI
{
    /// <summary><c>UIPopup_FeatureUnlock</c> 컨트롤러(05 §5.11, 층 600, 막 누름 막음). 화면 코드는 <see cref="FeatureUnlockPresenter"/>.</summary>
    public class FeatureUnlockPopup : MetaPanelPopup
    {
        protected override int Layer => MvUiLayer.Unlock;
        protected override MetaPanelPresenter CreatePresenter() => new FeatureUnlockPresenter();

        protected override void OnOpening(MetaPanelPresenter presenter) =>
            ((FeatureUnlockPresenter)presenter).SetKeys((Data as FeatureUnlockPopupData)?.Keys);
    }
}
