using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using jjevol;

namespace MV.UI
{
    /// <summary>
    /// 로비→런→결과→로비 화면 전환 묶음. 게임 루프(MV-PLAY-INT)가 부른다. UIManager가 시작된 뒤에만 쓴다.
    /// 로비: Screen(풀링). 런 HUD: hudManager에 직접 켬(로비 화면은 KeepPrevious라 건드리지 않음). 결과: Popup(ForceHide로 HUD 숨김).
    /// </summary>
    public static class MvUiNav
    {
        static UIManager Ui => UIManager.Instance;

        /// <summary>로비를 연다(런 HUD는 끈다).</summary>
        public static async UniTask<LobbyScreen> OpenLobbyAsync(LobbyViewData data)
        {
            var screen = await Ui.screenManager.ChangeUIScreenAsync(MvUiIds.Lobby, data) as LobbyScreen;
            Ui.hudManager.HideAll();
            return screen;
        }

        /// <summary>로비를 숨기고 런 HUD를 켠다. 같은 프레임에 런이 시작되도록 HUD 첫 로드만 기다린다.</summary>
        public static async UniTask<RunHud> EnterRunAsync(IRunHudSource source)
        {
            var current = Ui.screenManager.CurrentScreen;
            if (current && current.IsVisible) current.HideUI();
            var hud = Ui.hudManager;
            await hud.CachingHudAsync(new List<string> { MvUiIds.Run });
            hud.ShowHuds(new List<UIHudRequest> { new UIHudRequest { hudType = MvUiIds.Run } }, null);
            var run = hud.hudPool.TryGetValue(MvUiIds.Run, out var b) ? b as RunHud : null;
            if (run) run.Bind(source);
            return run;
        }

        /// <summary>결과 팝업을 연다(HUD는 팝업 설정으로 숨는다).</summary>
        public static async UniTask<ResultPopup> OpenResultAsync(ResultViewData data) =>
            await Ui.SpawnUIPopupAsync(MvUiIds.Result, new ResultPopupData(data)) as ResultPopup;
    }
}
