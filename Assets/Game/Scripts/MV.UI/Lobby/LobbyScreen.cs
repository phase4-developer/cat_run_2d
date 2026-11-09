using Cysharp.Threading.Tasks;
using jjevol;
using MV.Sound;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// <c>UIScreen_Lobby</c> 컨트롤러(05 §5.4): 출발 입력 5.4.1 + Level·Area·재화 표시 + 사이드·시스템 버튼·강화 버튼 3종(MV-UI-LOBBY 1차).
    /// 버튼 값·규칙은 화면 코드 <see cref="LobbyPresenter"/>, 겉은 <see cref="ILobbyView"/>·<see cref="ILobbyMenuView"/>로만 만진다.
    /// 덱(§5.5.2)과 머지·lvup 연출은 2차(MV-UI-FX2).
    /// </summary>
    public class LobbyScreen : UIScreen
    {
        [SerializeField] MonoBehaviour view;

        ILobbyView _view;
        ILobbyMenuView _menu;
        bool _menuBound;
        readonly LobbyPresenter _presenter = new LobbyPresenter();
        LobbyState _last;
        bool _drawn;
        bool _started;

        public ILobbyView View
        {
            get
            {
                if (_view == null) SetView(view as ILobbyView ?? GetComponent<ILobbyView>());
                return _view;
            }
        }

        /// <summary>이번에 보인 뒤 출발 콜백을 불렀는지.</summary>
        public bool Started => _started;

        /// <summary>사이드·강화 버튼 화면 코드. 겉이 아직 안 묶였으면 묶는다.</summary>
        public LobbyPresenter Presenter
        {
            get
            {
                if (!_menuBound) SetMenuView(view as ILobbyMenuView ?? GetComponent<ILobbyMenuView>());
                return _presenter;
            }
        }

        LobbyViewData Data => screenData as LobbyViewData;
        /// <summary>로비 값 입구(게임 루프가 시설 단계를 읽을 때도 쓴다).</summary>
        public ILobbySource Source => Data?.Source ?? MvSaveLobbySource.Instance;

        public void SetView(ILobbyView next)
        {
            if (_view != null) _view.StartPressed -= PressStart;
            _view = next;
            if (_view != null) _view.StartPressed += PressStart;
            _drawn = false;
        }

        public void SetMenuView(ILobbyMenuView next)
        {
            if (_menu != null)
            {
                _menu.SidePressed -= PressSide;
                _menu.UpgradePressed -= PressUpgrade;
            }
            _menuBound = true;
            _menu = next;
            if (_menu != null)
            {
                _menu.SidePressed += PressSide;
                _menu.UpgradePressed += PressUpgrade;
            }
            _presenter.SetView(next);
        }

        protected override void Awake()
        {
            base.Awake();
            _presenter.SideRequested += OnSideRequested;
            _presenter.Toast += MvUiToast.Show;
            _presenter.Bought += OnBought;
            _presenter.FacilityBump += OnFacilityBump;
            hudSettings.displayMode = UIHudDisplayMode.KeepPrevious; // 런 HUD는 게임 루프가 MvUiNav로 켜고 끈다
            MvUi.EnsureLayer(gameObject, MvUiLayer.Screen, true);
        }

        public override void ShowUI()
        {
            _started = false;
            _drawn = false;
            var p = Presenter;
            p.SetPort(Data?.Port);
            p.SetSource(Data?.Source);
            p.DevBuild = Debug.isDebugBuild; // 테스트 버튼은 개발 빌드 전용(05 §5.14)
            p.Muted = MvSound.Muted;
            base.ShowUI();
        }

        protected override void _DrawContents()
        {
            _drawn = false;
            Tick();
        }

        void LateUpdate()
        {
            Tick();
            if (IsVisible) Presenter.Tick(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 출발(05 §5.4.1). 한 번 보일 때 한 번만 콜백을 부른다. 화면 누름은 겉의 출발 영역이, 키보드는 Submit(Enter)이 부른다.
        /// Space는 Input System을 직접 읽어야 해서 게임 루프의 입력 어댑터가 이 메서드를 부른다.
        /// </summary>
        public void PressStart()
        {
            if (_started || !IsVisible) return;
            _started = true;
            Data?.OnStart?.Invoke();
        }

        /// <summary>값이 바뀐 항목만 겉에 넘긴다(영속 값은 자주 안 바뀌므로 비교 비용만 든다).</summary>
        public void Tick()
        {
            var v = View;
            if (v == null || !Source.TryRead(out var s)) return;
            bool all = !_drawn;
            if (all || !Same(s, _last)) Presenter.Draw(); // 재화·Area가 바뀌면 강화 버튼 켜짐·부제를 다시 그린다
            if (all || s.Level != _last.Level)
                v.SetLevel("Level " + s.Level); // ui.lobby.level
            if (all || s.AreaNo != _last.AreaNo || s.AreaName != _last.AreaName)
                v.SetArea(string.IsNullOrEmpty(s.AreaName) ? "Area " + s.AreaNo : s.AreaName + " · Area " + s.AreaNo); // ui.lobby.zoneNo
            if (all || s.Gold != _last.Gold || s.Souls != _last.Souls || s.Dia != _last.Dia || s.NeedSouls != _last.NeedSouls)
                v.SetWealth(MvUiFormat.Fmt(s.Gold), MvUiFormat.Fmt(s.Souls), MvUiFormat.Fmt(s.Dia));
            _last = s;
            _drawn = true;
        }

        static bool Same(in LobbyState a, in LobbyState b) =>
            a.Level == b.Level && a.AreaNo == b.AreaNo && a.Gold == b.Gold && a.Souls == b.Souls && a.Dia == b.Dia && a.NeedSouls == b.NeedSouls;

        /// <summary>사이드·시스템 버튼(<see cref="LobbySideIds"/>). 시험·입력 어댑터용.</summary>
        public void PressSide(string id) => Presenter.PressSide(id);

        /// <summary>강화 버튼(<c>10_lobbyUpgrade.key</c>). 시험·입력 어댑터용.</summary>
        public void PressUpgrade(string key) => Presenter.PressUpgrade(key);

        void Redraw() => Presenter.Draw();

        void OnSideRequested(string id)
        {
            var popup = new MetaPopupData { Port = Data?.Port, OnClosed = Redraw };
            switch (id)
            {
                case LobbySideIds.God:
                    MvUiNav.OpenGodSelectAsync(popup).Forget(Debug.LogException); // 정본: 소리 없음
                    break;
                case LobbySideIds.AreaUpgrade:
                    MvSound.PlayUi(MvUiSound.ButtonClick);
                    MvUiNav.OpenAreaUpgradeAsync(popup).Forget(Debug.LogException);
                    break;
                case LobbySideIds.Rebirth:
                    MvSound.PlayUi(MvUiSound.ButtonClick);
                    MvUiNav.OpenRebirthAsync(popup).Forget(Debug.LogException);
                    break;
                case LobbySideIds.Map:
                    MvUiNav.OpenMapAsync(popup).Forget(Debug.LogException); // 소리는 MvUiNav가 낸다
                    break;
                case LobbySideIds.Content:
                    MvSound.PlayUi(MvUiSound.ButtonClick);
                    MvUiNav.OpenContentMenuAsync(new ContentMenuData { Port = Data?.Port, OnClosed = Redraw }).Forget(Debug.LogException);
                    break;
                case LobbySideIds.Sound:
                    // 설정 자리(05 §5.14 소리 버튼). 저장은 메타 요청 settings.muted(실패해도 이번 실행의 소리는 바뀐 대로 둔다).
                    MvSound.Muted = !MvSound.Muted;
                    MvSound.PlayUi(MvUiSound.ButtonClick); // 정본 L3856: 전환 뒤 click
                    _presenter.Muted = MvSound.Muted;
                    _presenter.Port.SetMuted(MvSound.Muted);
                    Redraw();
                    break;
                case LobbySideIds.Debug:
                    MvUiNav.OpenDebugAsync(popup).Forget(Debug.LogException);
                    break;
            }
        }

        static void OnBought() => MvSound.PlayUi(MvUiSound.LobbyUpgradeBuy);

        void OnFacilityBump(string key) => Data?.OnFacilityBump?.Invoke(key);

        protected override void OnDestroy()
        {
            if (_view != null) _view.StartPressed -= PressStart;
            if (_menu != null)
            {
                _menu.SidePressed -= PressSide;
                _menu.UpgradePressed -= PressUpgrade;
            }
            _presenter.SideRequested -= OnSideRequested;
            _presenter.Toast -= MvUiToast.Show;
            _presenter.Bought -= OnBought;
            _presenter.FacilityBump -= OnFacilityBump;
            base.OnDestroy();
        }
    }
}
