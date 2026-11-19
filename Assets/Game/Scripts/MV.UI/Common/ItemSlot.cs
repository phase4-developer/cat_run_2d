using jjevol;
using MV.Data;
using TMPro;
using UnityEngine;

namespace MV.UI
{
    /// <summary>보상·아이템 칸 표시 데이터: <c>01_wealth.type</c>(아이콘·등급)과 수량 글자.</summary>
    public readonly struct ItemSlotData
    {
        public readonly MvTables Tables;
        public readonly string WealthType;
        /// <summary>숫자 모양까지 정한 수량(예: <c>500</c>, <c>1.2K</c>). 「x{0}」 틀은 칸이 씌운다.</summary>
        public readonly string Count;

        public ItemSlotData(MvTables tables, string wealthType, string count)
        {
            Tables = tables;
            WealthType = wealthType;
            Count = count;
        }
    }

    /// <summary>
    /// 디자이너 공용 프리팹 <c>Slot_Item</c>(등급 틀·아이콘·수량)의 루트 부품(MV-UI3-RESULT, 노션 [팝업] 인게임 결과).
    /// 겉(배치·장식)은 프리팹이 정하고 값만 넣는다:
    /// <c>Backboard_Grade</c> = 재화 등급(<c>01_wealth.gradeId</c> → <c>15_grade.grade</c> → 09_uiResource 등급 틀, D-GRADE-TABLE),
    /// <c>Holder_Icon/Icon</c> = <c>01_wealth.icon</c>, <c>Slot_Count/Count Text (TMP)</c> = 「x{0}」(노션 텍스트 포맷).
    /// 등급이 없는 재화(gradeId 0)는 디자이너 틀 그림을 그대로 둔다.
    /// </summary>
    public sealed class ItemSlot : UIBase
    {
        /// <summary>수량 틀 「x{0}」(노션 「텍스트 포맷 : x{0}」).</summary>
        public const string CountKey = "ui.common.countX";
        public const string CountFallback = "x{0}";

        [SerializeField] AddressableImage gradeFrame;
        [SerializeField] AddressableImage icon;
        /// <summary><c>Slot_Count/Count Text (TMP)</c>.</summary>
        [SerializeField] TMP_Text count;

        ItemSlotData _data;

        public string WealthType => _data.WealthType;
        public string IconSprite => icon ? icon.spriteName : null;
        public string GradeSprite => gradeFrame ? gradeFrame.spriteName : null;
        public string CountText => count ? count.text : null;

        /// <summary>프리팹 연결 확인(시험·검사용): 빠진 참조 이름 목록.</summary>
        public string MissingRefs()
        {
            var miss = new System.Collections.Generic.List<string>();
            if (!gradeFrame) miss.Add(nameof(gradeFrame));
            if (!icon) miss.Add(nameof(icon));
            if (!count) miss.Add(nameof(count));
            return string.Join(", ", miss);
        }

        /// <summary>데이터를 넣고 켜서 그린다.</summary>
        public void ShowUI(in ItemSlotData data)
        {
            _data = data;
            ShowUI();
        }

        /// <summary>재화 행의 등급 번호(15_grade.grade). 등급 없음·행 없음이면 0.</summary>
        public static int GradeOf(MvTables t, string wealthType)
        {
            if (t == null || string.IsNullOrEmpty(wealthType)) return 0;
            int wealthId = t.Wealth.GetId(wealthType);
            if (wealthId == 0) return 0;
            int gradeId = t.Wealth.GradeIdOf(wealthId);
            return gradeId != 0 ? t.Grade.Get(gradeId)?.grade ?? 0 : 0;
        }

        protected override void _DrawContents()
        {
            var t = _data.Tables;
            var sprite = HudCurrencySlot.IconOf(t, _data.WealthType);
            if (sprite != null) HudImage.Set(icon, MvAtlas.UiIcon, sprite);
            int grade = GradeOf(t, _data.WealthType);
            if (grade > 0) HudImage.Set(gradeFrame, MvUiGrade.Frame(t, grade));
            if (count) count.text = string.Format(MvUiText.Get("ui", CountKey, CountFallback), _data.Count ?? "");
        }
    }
}
