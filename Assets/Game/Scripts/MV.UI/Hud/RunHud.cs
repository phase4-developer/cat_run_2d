using System;
using jjevol;
using MV.Run;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// <c>UIHud_Run</c> 컨트롤러(05 §5.2, 1차: 재화 칩·계단 상자·계단 게이지·무기 카드). 화면 코드는 <see cref="RunHudPresenter"/>,
    /// 겉은 <see cref="IRunHudView"/>(기본 <see cref="RunHudView"/>). 가호 카드 룰렛·가호 줄은 <see cref="BlessingCardsView"/>(런 상태 출처일 때).
    /// 2차(MV-UI2-HUD): 디자이너 <c>IngameScreen</c>을 자식으로 두면 그 겉(<see cref="IngameHudView"/>)을 쓴다.
    /// </summary>
    public class RunHud : UIHudBase
    {
        /// <summary>겉 컴포넌트(<see cref="IRunHudView"/> 구현). 비면 같은 오브젝트에서 찾는다.</summary>
        [SerializeField] MonoBehaviour view;
        /// <summary>가호 카드 룰렛·가호 줄(05 §5.2.9·§5.2.7). 비면 자식으로 만든다.</summary>
        [SerializeField] BlessingCardsView cards;
        /// <summary>계단 정산 글자(등급 이름·냥파워 카드 확정)와 런 안내(토스트). 비면 마지막 자식(HUD 위)으로 만든다.</summary>
        [SerializeField] ClimbNoticeView notice;

        readonly RunHudPresenter _presenter = new RunHudPresenter();

        public RunHudPresenter Presenter
        {
            get
            {
                if (_presenter.View == null) _presenter.SetView(ResolveView());
                return _presenter;
            }
        }

        /// <summary>겉: 참조 → 같은 오브젝트 → 자식(디자이너 IngameScreen을 중첩한 프리팹, MV-UI2-HUD).</summary>
        IRunHudView ResolveView()
        {
            if (view is IRunHudView v) return v;
            v = GetComponent<IRunHudView>();
            return v ?? GetComponentInChildren<IRunHudView>(true);
        }

        /// <summary>런 상태를 직접 그리는 겉(디자이너 IngameScreen). 있으면 1차 가호 카드 화면을 만들지 않는다.</summary>
        IRunHudStateView StateView => Presenter.View as IRunHudStateView;

        /// <summary>런 시작에 값 출처를 묶는다. 다음 갱신에서 모든 항목을 다시 그린다.</summary>
        public void Bind(IRunHudSource source)
        {
            Presenter.Bind(source);
            var state = source is RunStateHudSource rs ? rs.Current : (Func<RunState>)null;
            if (StateView != null) StateView.BindRun(state);
            else Cards.Bind(state);
            Notice.Bind(state);
            // 런 안내(MvUiToast)는 이 HUD가 보일 때 여기에 띄운다. 숨었으면 로그만(기존 동작).
            MvUiToast.Handler = text => { if (this != null && isActiveAndEnabled) Notice.Toast(text); else Debug.Log("[Toast] " + text); };
            if (IsVisible) Presenter.Tick();
        }

        public BlessingCardsView Cards
        {
            get
            {
                if (cards) return cards;
                cards = GetComponentInChildren<BlessingCardsView>(true);
                if (!cards) cards = MvUi.Stretch(MvUi.Rect(transform, "BlessingCards")).gameObject.AddComponent<BlessingCardsView>();
                return cards;
            }
        }

        public ClimbNoticeView Notice
        {
            get
            {
                if (notice) return notice;
                notice = GetComponentInChildren<ClimbNoticeView>(true);
                if (!notice) notice = MvUi.Stretch(MvUi.Rect(transform, "ClimbNotice")).gameObject.AddComponent<ClimbNoticeView>();
                return notice;
            }
        }

        protected virtual void Awake()
        {
            // HUD 전체는 누를 수 없다(드래그가 아래로 지나감). 겉에 버튼(배속 토글 등)이 있으면 그 버튼만 받는다(겉이 나머지 그림의 raycastTarget을 끈다).
            MvUi.EnsureLayer(gameObject, MvUiLayer.Hud, GetComponentInChildren<Selectable>(true) != null);
        }

        protected override void _DrawContents()
        {
            Presenter.Invalidate();
            Presenter.Tick();
        }

        void LateUpdate()
        {
            Presenter.Tick();
            StateView?.TickRun(Time.unscaledDeltaTime);
            if (cards) cards.Tick(Time.unscaledDeltaTime);
            if (notice) notice.Tick(Time.unscaledDeltaTime);
        }
    }
}
