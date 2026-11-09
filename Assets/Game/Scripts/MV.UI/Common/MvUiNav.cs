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

        // ───────── 메타 패널(MV-UI-META). 로비 버튼 연결은 MV-UI-LOBBY. ─────────

        /// <summary>야옹신(05 §5.7).</summary>
        public static async UniTask<GodSelectPopup> OpenGodSelectAsync(MetaPopupData data = null) =>
            await Ui.SpawnUIPopupAsync(MvUiIds.GodSelect, data ?? new MetaPopupData()) as GodSelectPopup;

        /// <summary>Area 강화(05 §5.8).</summary>
        public static async UniTask<AreaUpgradePopup> OpenAreaUpgradeAsync(MetaPopupData data = null) =>
            await Ui.SpawnUIPopupAsync(MvUiIds.AreaUpgrade, data ?? new MetaPopupData()) as AreaUpgradePopup;

        /// <summary>부활(05 §5.9). 잠김이면 토스트만 내고 열지 않는다(null).</summary>
        public static async UniTask<RebirthPopup> OpenRebirthAsync(MetaPopupData data = null)
        {
            var port = data?.Port ?? MvMetaUiPort.Instance;
            if (port.Ready && !port.RebirthOpen)
            {
                int area = port.Tables?.ContentOpen.ByKey(MV.Meta.MvMetaRules.RebirthKey)?.conditionValue ?? 2;
                MvUiToast.Show("Area " + area + " 개방 시 해금"); // ui.rb.goLocked
                return null;
            }
            return await Ui.SpawnUIPopupAsync(MvUiIds.Rebirth, data ?? new MetaPopupData()) as RebirthPopup;
        }

        /// <summary>지도(05 §5.10).</summary>
        public static async UniTask<MapPopup> OpenMapAsync(MetaPopupData data = null)
        {
            MV.Sound.MvSound.PlayUi(MV.Sound.MvUiSound.ButtonClick); // 열 때 소리 click
            return await Ui.SpawnUIPopupAsync(MvUiIds.Map, data ?? new MetaPopupData()) as MapPopup;
        }

        /// <summary>신기능 해금 안내(05 §5.11). <paramref name="keys"/>가 null이면 아직 안 본 해금 전부. 보여 줄 것이 없으면 null.</summary>
        public static async UniTask<FeatureUnlockPopup> OpenFeatureUnlockAsync(IReadOnlyList<string> keys = null, IMetaUiPort port = null)
        {
            var p = port ?? MvMetaUiPort.Instance;
            keys = keys ?? p.PendingUnlockShows();
            if (keys.Count == 0) return null;
            return await Ui.SpawnUIPopupAsync(MvUiIds.FeatureUnlock, new FeatureUnlockPopupData { Keys = keys, Port = port }) as FeatureUnlockPopup;
        }

        /// <summary>테스트 패널(05 §5.14). 개발 빌드(<c>Debug.isDebugBuild</c>)가 아니면 열지 않는다.</summary>
        public static async UniTask<DebugPopup> OpenDebugAsync(MetaPopupData data = null)
        {
            if (!UnityEngine.Debug.isDebugBuild) return null;
            return await Ui.SpawnUIPopupAsync(MvUiIds.Debug, data ?? new MetaPopupData()) as DebugPopup;
        }

        /// <summary>가짜 광고(05 §5.12, MV-UI-LOBBY). 보통은 <see cref="MvFakeAd.Watch"/>가 부른다.</summary>
        public static async UniTask<FakeAdPopup> OpenFakeAdAsync(FakeAdPopupData data) =>
            await Ui.SpawnUIPopupAsync(MvUiIds.FakeAd, data) as FakeAdPopup;
    }
}
