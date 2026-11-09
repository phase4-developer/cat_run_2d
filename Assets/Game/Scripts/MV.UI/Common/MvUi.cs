using jjevol;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 기본 겉(뷰)을 코드로 짤 때 쓰는 작은 도구. 컨트롤러는 쓰지 않는다(D-92: 컨트롤러는 뷰 인터페이스로만 겉을 만진다).
    /// </summary>
    public static class MvUi
    {
        /// <summary>1u = 스테이지 폭 / 100 = 10.8(05 §5.1.1).</summary>
        public const float U = 10.8f;

        public const string AtlasIcon = "Atlas_UI_Icon";
        public const string AtlasHud = "Atlas_UI_Hud";
        public const string AtlasCommon = "Atlas_UI_Common";

        public static readonly Color Ink = Hex("#fbf6ec");
        public static readonly Color InkDim = Hex("#c9c1b2");
        public static readonly Color Gold = Hex("#f2c14e");
        public static readonly Color StairText = Hex("#ffd27a");
        public static readonly Color Panel = new Color(44 / 255f, 28 / 255f, 40 / 255f, .74f);
        public static readonly Color Dim = new Color(5 / 255f, 4 / 255f, 8 / 255f, .72f);

        static Font _font;

        /// <summary>
        /// 글꼴. TMP 글꼴 자산(05 §5.1.7 Font_BlackHanSans_SDF 등)과 TMP 기본 자원이 저장소에 아직 없어 uGUI 내장 글꼴로 그린다.
        /// 교체 지점: 글꼴 자산이 들어오면 뷰의 Text를 TextMeshProUGUI로 바꾼다(컨트롤러는 그대로).
        /// </summary>
        public static Font Font => _font ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Color Hex(string hex) => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>앵커·피벗·위치·크기를 한 번에 정한다(05 §5.1.3 표).</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Box(Transform parent, string name, Color color, bool raycast = false)
        {
            var img = Rect(parent, name).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>아틀라스 스킨 상자(9-slice). 프리팹에는 스프라이트 키만 남는다(D-05).</summary>
        public static AddressableImage Skin(Transform parent, string name, string atlas, string sprite, bool sliced = true)
        {
            var img = Rect(parent, name).gameObject.AddComponent<AddressableImage>();
            img.raycastTarget = false;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.SetAtlasSprite(atlas, sprite);
            return img;
        }

        public static AddressableImage Icon(Transform parent, string name, string sprite, float size, string atlas = AtlasIcon)
        {
            var img = Skin(parent, name, atlas, sprite, false);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        public static Text Label(Transform parent, string name, float size, Color color, TextAnchor align, FontStyle style = FontStyle.Bold)
        {
            var t = Rect(parent, name).gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = Mathf.RoundToInt(size);
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>글자 그림자 근사(프리셋 sh, 05 §5.1.7).</summary>
        public static Text Shadowed(Text t)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, .55f);
            s.effectDistance = new Vector2(0, -6);
            return t;
        }

        /// <summary>기본 상자 버튼(D-92 더미 겉).</summary>
        public static Button Button(Transform parent, string name, string text, Color bg, Color fg, float fontSize)
        {
            var img = Box(parent, name, bg, true);
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None; // 정본은 누름 효과 없음(05 §5.1.8)
            var label = Label(img.transform, "Text", fontSize, fg, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.text = text;
            return b;
        }

        /// <summary>층 Canvas(05 §5.1.5). 프리팹에 이미 있으면 값만 맞춘다.</summary>
        public static Canvas EnsureLayer(GameObject go, int sortingOrder, bool raycast)
        {
            var c = go.GetComponent<Canvas>();
            if (!c) c = go.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = sortingOrder;
            if (raycast && !go.GetComponent<GraphicRaycaster>()) go.AddComponent<GraphicRaycaster>();
            return c;
        }

        /// <summary>비율로 채우는 막대(둥근 끝 유지를 위해 fillAmount 대신 크기, 05 §5.1.8 ProgressBar).</summary>
        public static void SetFill(RectTransform fill, float t, bool vertical)
        {
            t = Mathf.Clamp01(t);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = vertical ? new Vector2(1, t) : new Vector2(t, 1);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }
    }
}
