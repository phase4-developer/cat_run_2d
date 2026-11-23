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
    /// <c>Backboard_Grade</c> = 재화 등급(<c>01_wealth.gradeId</c> → <c>15_grade</c> 등급 틀(frameSprite), D-GRADE-TABLE),
    /// <c>Holder_Icon/Icon</c> = <c>01_wealth.icon</c>, <c>Slot_Count/Count Text (TMP)</c> = 「x{0}」(노션 텍스트 포맷).
    /// 등급이 없는 재화(gradeId 0)는 디자이너 틀 그림(프리팹 <c>Backboard_Grade</c>)과 같은 등급의 틀을 다시 넣는다. 다시 쓰는 칸에 앞 보상 틀이 남지 않게 한다(MV-UI3-RESAUDIT).
    /// 아이콘이 빈 재화는 아이콘을 숨긴다(디자이너 예시 그림이 남지 않게).
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
        /// <summary>프리팹 <c>Backboard_Grade</c> 그림 이름(처음 그리기 전 값). 등급 없는 재화의 틀을 정한다.</summary>
        string _designerFrame;
        bool _designerFrameRead;

        public string WealthType => _data.WealthType;
        public string IconSprite => icon ? icon.spriteName : null;
        public string GradeSprite => gradeFrame ? gradeFrame.spriteName : null;
        public bool IconShown => icon && icon.enabled;
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

        /// <summary>디자이너 틀 그림과 같은 그림을 쓰는 등급(<c>15_grade.frameSprite</c>). 없으면 0.</summary>
        public static int GradeOfFrame(MvTables t, string frameSprite)
        {
            if (t == null || string.IsNullOrEmpty(frameSprite)) return 0;
            foreach (var g in t.Grade.Datas)
                if (MvUiGrade.Frame(t, g.grade)?.sprite == frameSprite) return g.grade;
            return 0;
        }

        protected override void _DrawContents()
        {
            var t = _data.Tables;
            if (!_designerFrameRead)
            {
                _designerFrameRead = true;
                if (gradeFrame) _designerFrame = !string.IsNullOrEmpty(gradeFrame.spriteName) ? gradeFrame.spriteName : gradeFrame.sprite ? gradeFrame.sprite.name : null;
            }
            MvConsumeIcon.TryWealth(t, _data.WealthType, out var iconAtlas, out var iconSprite);
            HudImage.SetOrHide(icon, iconAtlas, iconSprite);
            int grade = GradeOf(t, _data.WealthType);
            if (grade <= 0) grade = GradeOfFrame(t, _designerFrame);
            if (grade > 0) HudImage.Set(gradeFrame, MvUiGrade.Frame(t, grade));
            if (count) count.text = string.Format(MvUiText.Get("ui", CountKey, CountFallback), _data.Count ?? "");
        }
    }
}
