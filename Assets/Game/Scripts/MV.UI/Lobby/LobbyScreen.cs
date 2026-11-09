using jjevol;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// <c>UIScreen_Lobby</c> 컨트롤러(05 §5.4, 1차: 출발 입력 5.4.1 + Level·Area·재화 표시).
    /// 강화 버튼·덱·사이드 버튼은 2차(MV-UI-LOBBY). 겉은 <see cref="ILobbyView"/>로만 만진다.
    /// </summary>
    public class LobbyScreen : UIScreen
    {
        [SerializeField] MonoBehaviour view;

        ILobbyView _view;
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

        LobbyViewData Data => screenData as LobbyViewData;
        ILobbySource Source => Data?.Source ?? MvSaveLobbySource.Instance;

        public void SetView(ILobbyView next)
        {
            if (_view != null) _view.StartPressed -= PressStart;
            _view = next;
            if (_view != null) _view.StartPressed += PressStart;
            _drawn = false;
        }

        protected override void Awake()
        {
            base.Awake();
            hudSettings.displayMode = UIHudDisplayMode.KeepPrevious; // 런 HUD는 게임 루프가 MvUiNav로 켜고 끈다
            MvUi.EnsureLayer(gameObject, MvUiLayer.Screen, true);
        }

        public override void ShowUI()
        {
            _started = false;
            _drawn = false;
            base.ShowUI();
        }

        protected override void _DrawContents()
        {
            _drawn = false;
            Tick();
        }

        void LateUpdate() => Tick();

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
            if (all || s.Level != _last.Level)
                v.SetLevel("Level " + s.Level); // ui.lobby.level
            if (all || s.AreaNo != _last.AreaNo || s.AreaName != _last.AreaName)
                v.SetArea(string.IsNullOrEmpty(s.AreaName) ? "Area " + s.AreaNo : s.AreaName + " · Area " + s.AreaNo); // ui.lobby.zoneNo
            if (all || s.Gold != _last.Gold || s.Souls != _last.Souls || s.Dia != _last.Dia || s.NeedSouls != _last.NeedSouls)
                v.SetWealth(MvUiFormat.Fmt(s.Gold), MvUiFormat.Fmt(s.Souls), MvUiFormat.Fmt(s.Dia));
            _last = s;
            _drawn = true;
        }

        protected override void OnDestroy()
        {
            if (_view != null) _view.StartPressed -= PressStart;
            base.OnDestroy();
        }
    }
}
