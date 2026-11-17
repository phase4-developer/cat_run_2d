using System;
using jjevol;
using MV.Run;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// <c>UIHud_Run</c> 컨트롤러(05 §5.2, 1차: 재화 칩·계단 상자·계단 게이지·무기 카드). 화면 코드는 <see cref="RunHudPresenter"/>,
    /// 겉은 <see cref="IRunHudView"/>(기본 <see cref="RunHudView"/>). 가호 카드 룰렛·가호 줄은 <see cref="BlessingCardsView"/>(런 상태 출처일 때).
    /// 연료 게이지·등급 미터·HAND는 2차.
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
                if (_presenter.View == null) _presenter.SetView(view as IRunHudView ?? GetComponent<IRunHudView>());
                return _presenter;
            }
        }

        /// <summary>런 시작에 값 출처를 묶는다. 다음 갱신에서 모든 항목을 다시 그린다.</summary>
        public void Bind(IRunHudSource source)
        {
            Presenter.Bind(source);
            var state = source is RunStateHudSource rs ? rs.Current : (Func<RunState>)null;
            Cards.Bind(state);
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
            MvUi.EnsureLayer(gameObject, MvUiLayer.Hud, false); // HUD 전체는 누를 수 없다(드래그가 아래로 지나감)
        }

        protected override void _DrawContents()
        {
            Presenter.Invalidate();
            Presenter.Tick();
        }

        void LateUpdate()
        {
            Presenter.Tick();
            if (cards) cards.Tick(Time.unscaledDeltaTime);
            if (notice) notice.Tick(Time.unscaledDeltaTime);
        }
    }
}
