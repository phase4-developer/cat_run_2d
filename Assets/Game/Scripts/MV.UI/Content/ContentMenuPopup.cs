using System;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 컨텐츠 메뉴 팝업 데이터(05 §5.15). 비우면 기본 입구와 <see cref="StartModeHandler"/>를 쓴다.
    /// </summary>
    public sealed class ContentMenuData : MetaPopupData
    {
        /// <summary>
        /// 모드 시작(인자 = <c>400_content.key</c>). 입장 판정(잠금·하루 한도·메타 <c>RecordModePlay</c>)을 통과하고 메뉴가 닫힌 뒤 1번 불린다.
        /// null이면 <see cref="StartModeHandler"/>. 둘 다 없으면 「준비 중」 토스트(메타 기록 없음).
        /// </summary>
        public Action<string> OnStartMode;

        /// <summary>성장 탭 시스템 화면 열기(인자 = <c>05_contentOpen.key</c>). null이면 <see cref="OpenMetaHandler"/>.</summary>
        public Action<string> OnOpenMeta;

        /// <summary>
        /// 교체 지점(G7 MV-CONTENT-&lt;모드&gt;): 게임 루프가 모드 런 시작 함수를 한 번 넣는다. 로비 버튼이 여는 메뉴는 이것을 쓴다.
        /// </summary>
        public static Action<string> StartModeHandler;

        /// <summary>교체 지점(MV-META-&lt;시스템&gt;): 시스템 화면(<c>UIPopup_Quest</c> 등)을 여는 함수.</summary>
        public static Action<string> OpenMetaHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            StartModeHandler = null;
            OpenMetaHandler = null;
        }
    }

    /// <summary><c>UIPopup_ContentMenu</c> 컨트롤러(05 §5.15, 층 500). 겉은 메타 공통 틀(<see cref="MetaPanelView"/>), 화면 코드는 <see cref="ContentMenuPresenter"/>.</summary>
    public class ContentMenuPopup : MetaPanelPopup
    {
        protected override MetaPanelPresenter CreatePresenter() => new ContentMenuPresenter();

        protected override void OnOpening(MetaPanelPresenter presenter)
        {
            var p = (ContentMenuPresenter)presenter;
            var data = Data as ContentMenuData;
            p.StartMode = data?.OnStartMode ?? ContentMenuData.StartModeHandler;
            p.OpenMeta = data?.OnOpenMeta ?? ContentMenuData.OpenMetaHandler;
        }
    }
}
