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
    /// </summary>
    public sealed class HudClimbCard : MonoBehaviour
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

        public void Set(MvTables t, int tierIndex)
        {
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
            if (progress) progress.normalizedValue = Mathf.Clamp01(fill01);
        }
    }
}
