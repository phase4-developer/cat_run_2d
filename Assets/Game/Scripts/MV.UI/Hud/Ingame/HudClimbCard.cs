using jjevol;
using MV.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 <c>Card_Class</c>(계단 등급 카드)의 값 연결(MV-UI2-HUD, [HUD] 인게임 ClimbStair).
    /// 등급 틀 = 115_cardTier.frameSprite, 계단 수 = 그 등급 need, 게이지 = 그 등급까지 채움, 제목 = 등급 이름, 숫자 = tierIndex + 1. 카드 그림(별자리)은 프리팹 그대로.
    /// 섹션은 <see cref="ShowUI(in ClimbCardData)"/>로 켜고 그린다(공용 부품 MV-UI3-HUD2).
    /// </summary>
    public sealed class HudClimbCard : UIBase
    {
        [SerializeField] AddressableImage grade;
        [SerializeField] TMP_Text floorCount;
        [SerializeField] Slider progress;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text level;

        public int Tier { get; private set; } = -1;
        public float Fill => progress ? progress.normalizedValue : 0;
        public string Title => title ? title.text : null;
        public string GradeSprite => grade ? grade.spriteName : null;
        public RectTransform Rect => (RectTransform)transform;

        ClimbCardData _data;

        /// <summary>데이터를 넣고 켜서 그린다(등급 + 게이지).</summary>
        public void ShowUI(in ClimbCardData data)
        {
            _data = data;
            ShowUI();
        }

        protected override void _DrawContents()
        {
            Set(_data.Tables, _data.TierIndex);
            SetFill(_data.Fill01);
        }

        /// <summary>켜고 끄지 않고 등급만 지금 그린다(화면 조합 전 기존 호출용).</summary>
        public void Set(MvTables t, int tierIndex)
        {
            _data = new ClimbCardData(t, tierIndex, _data.Fill01);
            Tier = tierIndex;
            var row = HudGrade.CardTier(t, tierIndex);
            if (row == null) return;
            HudImage.Set(grade, MvAtlas.UiCard, row.frameSprite);
            if (floorCount) floorCount.text = row.need.ToString();
            if (title) title.text = HudGrade.CardTierName(t, row);
            if (level) level.text = (row.tierIndex + 1).ToString();
        }

        public void SetFill(float fill01)
        {
            _data = new ClimbCardData(_data.Tables, _data.TierIndex, fill01);
            if (progress) progress.normalizedValue = Mathf.Clamp01(fill01);
        }

        /// <summary>진행 게이지(<c>Progress_Level</c>)는 진행 중인 Current 자리 카드에만 보인다(노션 ClimbStair 연출 보강 1).</summary>
        public void ShowProgress(bool on)
        {
            if (progress && progress.gameObject.activeSelf != on) progress.gameObject.SetActive(on);
        }

        public bool ProgressShown => progress && progress.gameObject.activeSelf;
    }

    /// <summary>계단 등급 카드 표시 데이터: 115_cardTier 등급 번호와 게이지(0~1).</summary>
    public readonly struct ClimbCardData
    {
        public readonly MvTables Tables;
        public readonly int TierIndex;
        public readonly float Fill01;

        public ClimbCardData(MvTables tables, int tierIndex, float fill01)
        {
            Tables = tables;
            TierIndex = tierIndex;
            Fill01 = fill01;
        }
    }
}
