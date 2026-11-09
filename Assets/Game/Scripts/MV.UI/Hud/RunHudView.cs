using jjevol;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 런 HUD 기본 겉(05 §5.2.1~5.2.6, 추출 스킨 Atlas_UI_Hud·Atlas_UI_Common·Atlas_UI_Icon).
    /// 참조가 비어 있으면 1080 기준 배치로 자식을 만든다. 프리팹에서 참조를 이어 두면 그대로 쓴다(겉 교체 지점).
    /// 화소 맞춤·연출(pulse·gpulse·glvup·calm)은 2차.
    /// </summary>
    public class RunHudView : MonoBehaviour, IRunHudView
    {
        const float U = MvUi.U;

        [Header("왼쪽 위")]
        [SerializeField] Text levelText;
        [SerializeField] Text floorText;
        [Header("재화 칩")]
        [SerializeField] Text goldText;
        [SerializeField] Text soulsText;
        [SerializeField] Text diaText;
        [Header("계단 상자")]
        [SerializeField] AddressableImage stairBox;
        [SerializeField] Text stairText;
        [Header("계단 게이지")]
        [SerializeField] Text gaugeLvText;
        [SerializeField] Text gaugePerText;
        [SerializeField] RectTransform gaugeFill;
        [Header("무기 카드")]
        [SerializeField] AddressableImage weaponCard;
        [SerializeField] AddressableImage weaponIcon;
        [SerializeField] Text weaponName;
        [SerializeField] Text weaponDmg;

        bool _built;

        void Awake() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            if (levelText) return; // 프리팹이 겉을 이미 가졌다
            var root = (RectTransform)transform;
            BuildTopLeft(root);
            BuildChips(root);
            BuildStairBox(root);
            BuildGauge(root);
            BuildWeapon(root);
        }

        // ---------------- IRunHudView ----------------

        public void SetLevel(string text) { EnsureBuilt(); levelText.text = text; }
        public void SetFloor(string text) { EnsureBuilt(); floorText.text = text; }
        public void SetGold(string text) { EnsureBuilt(); goldText.text = text; }
        public void SetSouls(string text) { EnsureBuilt(); soulsText.text = text; }
        public void SetDia(string text) { EnsureBuilt(); diaText.text = text; }

        public void SetStairs(string text, bool flying)
        {
            EnsureBuilt();
            stairText.text = text;
            if (stairBox) stairBox.SetSprite(flying ? "UI_Skin_StairBox_Fly" : "UI_Skin_StairBox");
        }

        public void SetGauge(string lvText, string perText, float fill01)
        {
            EnsureBuilt();
            gaugeLvText.text = lvText;
            gaugePerText.text = perText;
            MvUi.SetFill(gaugeFill, fill01, true);
        }

        public void SetWeapon(int tier, string iconSprite, string name, string damageText, Color gradeColor)
        {
            EnsureBuilt();
            int g = Mathf.Clamp(tier, 1, 8);
            if (weaponCard) weaponCard.SetSprite("UI_Skin_WpnCard_G" + g);
            if (weaponIcon && !string.IsNullOrEmpty(iconSprite)) weaponIcon.SetSprite(iconSprite);
            weaponName.text = name;
            weaponDmg.text = damageText;
        }

        // ---------------- 기본 배치(1080 기준) ----------------

        static Text Line(Transform parent, string name, float size, Color color, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, float width)
        {
            var t = MvUi.Label(parent, name, size, color, align);
            MvUi.Place(t.rectTransform, anchor, pivot, pos, new Vector2(width, size * 1.25f));
            return t;
        }

        void BuildTopLeft(RectTransform root)
        {
            // .tl: left 3u, top 3u, 사이 1u. 글 그림자 sh
            var tl = MvUi.Place(MvUi.Rect(root, "TopLeft"), new Vector2(0, 1), new Vector2(0, 1), new Vector2(3 * U, -3 * U), new Vector2(40 * U, 20 * U));
            levelText = MvUi.Shadowed(Line(tl, "Level", 4.6f * U, MvUi.Ink, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, 40 * U));
            var chip = MvUi.Skin(tl, "FloorChip", MvUi.AtlasCommon, "UI_Skin_Panel");
            MvUi.Place(chip.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -(4.6f * 1.25f + 1) * U), new Vector2(22 * U, 7.6f * U));
            floorText = MvUi.Label(chip.transform, "Text", 2.5f * U, MvUi.Ink, TextAnchor.MiddleLeft);
            MvUi.Stretch(floorText.rectTransform);
            floorText.rectTransform.offsetMin = new Vector2(2 * U, 0);
            floorText.lineSpacing = 1.1f;
        }

        Text Chip(RectTransform parent, int index, string icon)
        {
            // CurrencyChip: 아이콘 4.2u, 글 3.5u, 알약 바탕(05 §5.1.8). 오른쪽 정렬로 세로 3개, 사이 1.2u.
            float h = 5.4f * U, w = 24 * U;
            var chip = MvUi.Skin(parent, "Chip" + index, MvUi.AtlasCommon, "UI_Skin_Chip");
            MvUi.Place(chip.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -index * (h + 1.2f * U)), new Vector2(w, h));
            var ic = MvUi.Icon(chip.transform, "Icon", icon, 4.2f * U);
            MvUi.Place(ic.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(.8f * U, 0), new Vector2(4.2f * U, 4.2f * U));
            var t = MvUi.Label(chip.transform, "Value", 3.5f * U, MvUi.Ink, TextAnchor.MiddleRight);
            MvUi.Stretch(t.rectTransform);
            t.rectTransform.offsetMax = new Vector2(-2.4f * U, 0);
            return t;
        }

        void BuildChips(RectTransform root)
        {
            var tr = MvUi.Place(MvUi.Rect(root, "Chips"), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-3 * U, -3 * U), new Vector2(24 * U, 20 * U));
            goldText = Chip(tr, 0, "UI_Icon_Gold");   // 01_wealth 1
            soulsText = Chip(tr, 1, "UI_Icon_Soul");  // 01_wealth 3
            diaText = Chip(tr, 2, "UI_Icon_Dia");     // 01_wealth 2
        }

        void BuildStairBox(RectTransform root)
        {
            // #stairBox: 가운데, top 11u, 너비 42u
            stairBox = MvUi.Skin(root, "StairBox", MvUi.AtlasHud, "UI_Skin_StairBox");
            MvUi.Place(stairBox.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -11 * U), new Vector2(42 * U, 11 * U));
            var can = MvUi.Icon(stairBox.transform, "Can", "UI_Icon_Stair", 5.6f * U); // 09_uiResource hud.stairCan
            MvUi.Place(can.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-12 * U, 0), new Vector2(5.6f * U, 5.6f * U));
            stairText = MvUi.Label(stairBox.transform, "Stairs", 8.4f * U, MvUi.StairText, TextAnchor.MiddleCenter);
            MvUi.Place(stairText.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(1 * U, 0), new Vector2(18 * U, 9 * U));
            var unit = MvUi.Label(stairBox.transform, "Unit", 3.4f * U, MvUi.InkDim, TextAnchor.MiddleLeft);
            MvUi.Place(unit.rectTransform, new Vector2(.5f, .5f), new Vector2(0, .5f), new Vector2(10.5f * U, -1.5f * U), new Vector2(10 * U, 4 * U));
            unit.text = "계단"; // ui.hud.stairUnit
        }

        void BuildGauge(RectTransform root)
        {
            // #ampRow: left 2u, top 27%, 너비 13.5u
            var box = MvUi.Skin(root, "StairGauge", MvUi.AtlasCommon, "UI_Skin_Panel");
            box.color = new Color(.85f, .62f, .55f, 1f);
            MvUi.Place(box.rectTransform, new Vector2(0, .73f), new Vector2(0, 1), new Vector2(2 * U, 0), new Vector2(13.5f * U, 50 * U));
            var icon = MvUi.Icon(box.transform, "Icon", "UI_Icon_Fac_Tower_2", 8.4f * U); // 09_uiResource hud.gaugeIcon
            MvUi.Place(icon.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -1.4f * U), new Vector2(8.4f * U, 8.4f * U));
            var nameLabel = Line(box.transform, "Name", 2.3f * U, MvUi.Hex("#ffd9b8"), TextAnchor.UpperCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -10.7f * U), 13 * U);
            nameLabel.text = "계단"; // ui.hud.gaugeName
            gaugeLvText = Line(box.transform, "Lv", 5f * U, Color.white, TextAnchor.UpperCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -13.6f * U), 13 * U);
            var bar = MvUi.Skin(box.transform, "Bar", MvUi.AtlasHud, "UI_Skin_Bar_Stair", false);
            MvUi.Place(bar.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -20.4f * U), new Vector2(4.4f * U, 24 * U));
            var fillRoot = MvUi.Stretch(MvUi.Rect(bar.transform, "FillArea"));
            fillRoot.offsetMin = new Vector2(.6f * U, .6f * U);
            fillRoot.offsetMax = new Vector2(-.6f * U, -.6f * U);
            var fill = MvUi.Box(fillRoot, "Fill", MvUi.Hex("#e88a5a"));
            gaugeFill = fill.rectTransform;
            MvUi.SetFill(gaugeFill, 0, true);
            gaugePerText = Line(box.transform, "Per", 3.4f * U, MvUi.Hex("#ffe08a"), TextAnchor.UpperCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -45.4f * U), 13 * U);
        }

        void BuildWeapon(RectTransform root)
        {
            // #wpn: right 2u, top 30%, 너비 25u. 장착 카드 19u 정사각
            var wpn = MvUi.Place(MvUi.Rect(root, "Weapon"), new Vector2(1, .7f), new Vector2(1, 1), new Vector2(-2 * U, 0), new Vector2(25 * U, 25 * U));
            weaponCard = MvUi.Skin(wpn, "Card", MvUi.AtlasHud, "UI_Skin_WpnCard_G1");
            MvUi.Place(weaponCard.rectTransform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(19 * U, 19 * U));
            weaponIcon = MvUi.Icon(weaponCard.transform, "Icon", "UI_Icon_Weapon_1", 10.5f * U);
            MvUi.Place(weaponIcon.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -1.2f * U), new Vector2(10.5f * U, 10.5f * U));
            weaponName = Line(weaponCard.transform, "Name", 2.6f * U, Color.white, TextAnchor.UpperCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -12 * U), 18 * U);
            weaponDmg = Line(weaponCard.transform, "Dmg", 2f * U, MvUi.InkDim, TextAnchor.UpperCenter, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -15.4f * U), 18 * U);
            var hint = MvUi.Shadowed(Line(wpn, "Hint", 2.1f * U, MvUi.InkDim, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -20.2f * U), 25 * U));
            hint.text = "같은 무기 3장 → 머지"; // ui.hud.mergeHint
        }
    }
}
