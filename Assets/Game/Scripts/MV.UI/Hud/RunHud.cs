using System;
using jjevol;
using MV.Run;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// <c>UIHud_Run</c> 컨트롤러(05 §5.2). 화면 코드는 <see cref="RunHudPresenter"/>,
    /// 겉은 자식 디자이너 프리팹 <c>IngameScreen</c>(<see cref="IngameHudView"/>), 계단 정산 글자·런 안내는 자식 프리팹 <c>HUD_ClimbNotice</c>(<see cref="ClimbNoticeView"/>).
    /// UI는 프리팹에만 있고 이 컨트롤러는 만들지 않는다.
    /// </summary>
    public class RunHud : UIHudBase
    {
        /// <summary>겉 컴포넌트(<see cref="IRunHudView"/> 구현). 비면 같은 오브젝트에서 찾는다.</summary>
        [SerializeField] MonoBehaviour view;
        /// <summary>계단 정산 글자(등급 이름·냥파워 카드 확정)와 런 안내(토스트). 자식 프리팹 <c>HUD_ClimbNotice</c>.</summary>
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

        /// <summary>런 상태를 직접 그리는 겉(디자이너 IngameScreen: 손패·계단 카드·가호 룰렛).</summary>
        IRunHudStateView StateView => Presenter.View as IRunHudStateView;

        /// <summary>런 시작에 값 출처를 묶는다. 다음 갱신에서 모든 항목을 다시 그린다.</summary>
        public void Bind(IRunHudSource source)
        {
            Presenter.Bind(source);
            var state = source is RunStateHudSource rs ? rs.Current : (Func<RunState>)null;
            StateView?.BindRun(state);
            if (Notice) Notice.Bind(state);
            // 런 안내(MvUiToast)는 이 HUD가 보일 때 여기에 띄운다. 숨었으면 로그만(기존 동작).
            MvUiToast.Handler = text => { if (this != null && isActiveAndEnabled) { if (Notice) Notice.Toast(text); } else Debug.Log("[Toast] " + text); };
            if (IsVisible) Presenter.Tick();
        }

        public ClimbNoticeView Notice
        {
            get
            {
                if (notice) return notice;
                notice = GetComponentInChildren<ClimbNoticeView>(true);
                if (!notice) Debug.LogWarning("[RunHud] 계단 안내 프리팹(HUD_ClimbNotice)이 자식에 없습니다.", this);
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
            if (notice) notice.Tick(Time.unscaledDeltaTime);
        }
    }
}
