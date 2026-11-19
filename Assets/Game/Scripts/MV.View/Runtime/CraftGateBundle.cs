using MV.Data;
using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 무기문(cgate) 위 무기 카드 묶음(MV-UI3-HUD9, 사용자 결정 2026-11-19 「둘로 나눔」·「만든 장 수 그대로」·「임시 묶음 만들기」, 같은 날 「CreaftGate용이고 … 다른덴 추가하지마」).
    /// 장 수 = 글자판 숫자와 같은 Made + Extra(상한 없음). 그림 = 지금 런 무기 단계(Bt)의 등급 틀(09_uiResource 등급 틀)과 103_item.icon. HUD 무기 칸과 같은 그림이다.
    /// 자리: <c>Obj_CraftGate</c> 변형에만 더한 자식 <c>WeaponBundle_Temp</c>. 배치·크기·통과 때 HUD로 넘기기는 부모 <see cref="GateRewardDisplay"/>가 한다.
    /// 임시 리소스: 디자이너 묶음 리소스가 오면 프리팹만 바꾼다(코드로 그림을 그리지 않는다).
    /// </summary>
    /// 문 안쪽 아이콘: 문 안쪽 카드(<see cref="_labelIcon"/>, 오브젝트 LabelIcon_Temp)에 얻을 무기의 같은 틀·그림을 칠한다
    /// (Notion 「Obj_CraftGate UI」 「Gate 내부에 얻을 수 있는 무기의 아이콘」, HellEscape와 차이 「라벨 옆에 무기 아이콘 없음」, 사용자 2026-11-19 「크래프트 문은 이걸로 조절」). 자리·크기는 프리팹 값.
    [DisallowMultipleComponent]
    public sealed class CraftGateBundle : GateRewardDisplay
    {
        [Tooltip("문 안쪽 무기 카드(켜 둔 GateRewardCard, LabelIcon_Temp). 자리는 이 오브젝트 위치.")]
        [SerializeField] GateRewardCard _labelIcon;
        [Tooltip("문 안쪽 카드 크기(m).")]
        [SerializeField] Vector2 _labelIconSize = new Vector2(0.46f, 0.4f);
        [Tooltip("문 안쪽 카드 정렬 순서(레이블 글자 105와 같은 층).")]
        [SerializeField] int _labelIconOrder = 105;

        Sprite _labelFrame, _labelSprite;

        public override GateRewardKind Kind => GateRewardKind.Weapon;
        public GateRewardCard LabelIcon => _labelIcon;
        public Vector2 LabelIconSize => _labelIconSize;

        public override void OnBind(RunObjectView view)
        {
            base.OnBind(view);
            _labelFrame = _labelSprite = null;
            PaintLabel(view);
        }

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            base.OnTick(view, f);
            PaintLabel(view);
        }

        /// <summary>문 안쪽 카드: 지금 무기 단계 그림이 바뀌었을 때만 다시 칠한다.</summary>
        void PaintLabel(RunObjectView view)
        {
            if (_labelIcon == null) return;
            Look(view, out var frame, out var icon);
            if (frame == null && icon == null) return;
            if (frame == _labelFrame && icon == _labelSprite) return;
            _labelFrame = frame;
            _labelSprite = icon;
            _labelIcon.Paint(frame, icon, _labelIconSize, _labelIconOrder);
        }

        protected override int Count(RunObject o) => o.Made + o.Extra;

        protected override void Look(RunObjectView view, out Sprite frame, out Sprite icon)
        {
            frame = icon = null;
            var c = view != null ? view.Ctx : null;
            if (c?.Sprites == null) return;
            if (!string.IsNullOrEmpty(c.GateWeaponIcon)) icon = c.Sprites.Get(MvAtlas.UiIcon, c.GateWeaponIcon);
            if (!string.IsNullOrEmpty(c.GateWeaponFrame)) frame = c.Sprites.Get(c.GateWeaponFrameAtlas, c.GateWeaponFrame);
        }
    }
}
