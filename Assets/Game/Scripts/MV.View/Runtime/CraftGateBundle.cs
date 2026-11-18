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
    [DisallowMultipleComponent]
    public sealed class CraftGateBundle : GateRewardDisplay
    {
        public override GateRewardKind Kind => GateRewardKind.Weapon;

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
