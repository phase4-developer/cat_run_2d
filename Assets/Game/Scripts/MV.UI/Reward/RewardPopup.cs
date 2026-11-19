using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Data;
using MV.Sound;
using ConsumeData = MV.Data.ConsumeData;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 공용 보상 팝업 데이터(MV-UI3-REWARD). 보상 목록과 닫힌 뒤 부를 콜백.
    /// 표는 넘기지 않으면 메타 포트의 표(<see cref="MvMetaUiPort"/>)를 쓴다.
    /// </summary>
    public sealed class RewardPopupData : IPopupData
    {
        public readonly IReadOnlyList<ConsumeData> Rewards;
        public readonly MvTables Tables;
        public readonly Action OnClosed;

        public RewardPopupData(IReadOnlyList<ConsumeData> rewards, Action onClosed = null, MvTables tables = null)
        {
            Rewards = rewards ?? Array.Empty<ConsumeData>();
            OnClosed = onClosed;
            Tables = tables;
        }
    }

    /// <summary>
    /// <c>UIPopup_Reward</c> 컨트롤러(노션 [팝업] 보상획득 팝업, 정크버스터즈 RewardPopup 참고). 여는 곳은 <see cref="MvUiNav.OpenRewardAsync"/>.
    /// 겉은 <see cref="RewardDesignView"/>(디자이너 Popup_Reward). Backboard 터치·뒤로 가기는 칸 등장이 끝난 뒤에만 닫는다.
    /// 닫히면 <see cref="RewardPopupData.OnClosed"/>를 한 번 부른다. 지급은 여는 쪽이 이미 끝낸 뒤다(이 팝업은 보여 주기만 한다).
    /// </summary>
    public class RewardPopup : UIPopup
    {
        [SerializeField] RewardDesignView view;

        bool _bound;
        bool _closedInvoked;

        public RewardDesignView View => view;

        protected override void Awake()
        {
            base.Awake();
            Bind();
        }

        void Bind()
        {
            if (_bound || view == null) return;
            _bound = true;
            view.ClosePressed += PressClose;
        }

        public override void ShowUI()
        {
            _closedInvoked = false;
            base.ShowUI(); // Refresh → _DrawContents → Open
        }

        protected override void _DrawContents()
        {
            Bind();
            if (view == null)
            {
                Debug.LogWarning("[RewardPopup] RewardDesignView 참조가 없습니다(Assets/Game/Addressables/Local/UI/Common/UIPopup_Reward.prefab).");
                return;
            }
            var data = popupData as RewardPopupData;
            var tables = data?.Tables ?? MvMetaUiPort.Instance.Tables;
            view.Show(RewardItems.Build(tables, data?.Rewards));
        }

        /// <summary>Backboard 터치: 등장 중이면 무시, 끝났으면 닫는다.</summary>
        public void PressClose()
        {
            if (!IsOpen || IsTransitioning || view == null || !view.CanClose) return;
            MvSound.PlayUi(MvUiSound.ButtonClick);
            ClosePopup(true);
        }

        /// <summary>뒤로 가기·Esc: Backboard 터치와 같은 조건(등장이 끝나야 닫힘).</summary>
        public override UniTask<bool> OnBackRequestAsync() => UniTask.FromResult(view == null || view.CanClose);

        public override void HideUI()
        {
            if (!IsOpen) return;
            base.HideUI();
            InvokeClosedOnce();
        }

        void InvokeClosedOnce()
        {
            if (_closedInvoked) return;
            _closedInvoked = true;
            (popupData as RewardPopupData)?.OnClosed?.Invoke();
        }

        protected override void OnDestroy()
        {
            if (view != null && _bound) view.ClosePressed -= PressClose;
            base.OnDestroy();
        }
    }
}
