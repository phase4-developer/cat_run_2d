using Cysharp.Threading.Tasks;
using jjevol;
using MV.Save;
using MV.Sound;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 지도(LevelProgress) 팝업 데이터. 런 끝 흐름은 <see cref="RunEnd"/>와 이번 런 영혼 수를 넘긴다.
    /// 로비(Area 이름표 누름)는 비워 두면 저장된 영혼 수만 보여 준다.
    /// </summary>
    public sealed class LevelProgressPopupData : MetaPopupData
    {
        /// <summary>런 끝 자동 연출(진척 오름 → 스스로 닫힘 → 결과).</summary>
        public bool RunEnd;
        /// <summary>정산 전 저장 영혼 수에 더해 보여 줄 이번 런 영혼 수.</summary>
        public long RunSouls;
    }

    /// <summary>
    /// <c>UIPopup_Map</c> 컨트롤러(05 §5.10 지도, 디자이너 판 <c>Popup_LevelProgress</c>, MV-UI3-LVPROG, 층 550).
    /// 열기·닫기·뒤로 가기와 데이터 준비만 맡고, 화면은 <see cref="LevelProgressView"/>가 그린다.
    /// 닫히면 <see cref="MetaPopupData.OnClosed"/>를 한 번 부른다(런 끝 지도 → 결과 계약).
    /// 기획서 5.10의 기능 상자(<c>05_contentOpen</c>)는 디자이너 시안에 자리가 없어 그리지 않는다(사용자 2026-11-19 「숨김, 로직 유지」). 해금 판정·신기능 팝업은 그대로다.
    /// </summary>
    public class MapPopup : UIPopup
    {
        [SerializeField] LevelProgressView view;

        public LevelProgressView View => view;
        MetaPopupData Data => popupData as MetaPopupData;

        protected override void Awake()
        {
            base.Awake();
            if (view) view.CloseRequested += OnCloseRequested;
            else Debug.LogWarning("[MapPopup] LevelProgressView 참조가 없습니다. Assets/Game/Addressables/Local/UI/Meta/UIPopup_Map.prefab의 view를 채우세요.");
        }

        protected override void _DrawContents()
        {
            if (!view) return;
            var data = Data;
            var port = data?.Port ?? MvMetaUiPort.Instance;
            var tables = port.Tables;
            if (tables == null) { Debug.LogWarning("[MapPopup] 표가 없어 지도를 그리지 않습니다."); return; }
            long saved = port.Wealth(MvWealthIds.Souls);
            var lp = data as LevelProgressPopupData;
            view.ShowUI(new LevelProgressViewData
            {
                Model = LevelProgressModel.Build(tables, port.AreaIndex + 1),
                FromSouls = saved,
                ToSouls = saved + (lp?.RunSouls ?? 0),
                AutoClose = lp != null && lp.RunEnd,
            });
        }

        /// <summary>닫기 버튼과 같은 길(시험·런 끝 흐름).</summary>
        public void PressClose()
        {
            MvSound.PlayUi(MvUiSound.ButtonClick);
            Close();
        }

        void OnCloseRequested()
        {
            var lp = Data as LevelProgressPopupData;
            if (lp == null || !lp.RunEnd) MvSound.PlayUi(MvUiSound.ButtonClick);
            Close();
        }

        /// <summary>
        /// 뒤로 가기·Esc는 Framework가 <c>ClosePopup</c>을 직접 불러 <see cref="MetaPopupData.OnClosed"/>를 건너뛰므로(MV-P0-STAB)
        /// 같은 닫기 길로 보내고 false를 돌려 Framework가 다시 닫지 않게 한다. 런 끝 연출 중이면 끝 값으로 건너뛰고 닫는다.
        /// </summary>
        public override UniTask<bool> OnBackRequestAsync()
        {
            if (IsOpen && !IsTransitioning) PressClose();
            return UniTask.FromResult(false);
        }

        /// <summary>실제로 닫혔을 때만 OnClosed를 한 번 부른다.</summary>
        void Close()
        {
            if (!IsOpen) return;
            var data = Data;
            if (view) view.Complete();
            ClosePopup(true);
            if (IsOpen) return;
            data?.OnClosed?.Invoke();
        }

        protected override void OnDestroy()
        {
            if (view) view.CloseRequested -= OnCloseRequested;
            base.OnDestroy();
        }
    }
}
