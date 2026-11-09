using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 메타 공통 패널 기본 겉(D-92 더미: 막·상자·글자·버튼만, 스킨·연출·정본 배치 없음). 직렬화 참조가 비면 자식을 코드로 만든다.
    /// 틀 = 제목·닫기·재화 줄·부제·탭·스크롤 목록·빈 글·아래 큰 버튼(05 §5.16). 겉 교체 지점: <see cref="IMetaPanelView"/>를 구현한 겉을 프리팹에 둔다.
    /// </summary>
    public class MetaPanelView : MonoBehaviour, IMetaPanelView
    {
        const float U = MvUi.U;

        static readonly Color BoxColor = new Color(58 / 255f, 38 / 255f, 52 / 255f, .97f);
        static readonly Color RowColor = new Color(0, 0, 0, .28f);
        static readonly Color HeadColor = new Color(21 / 255f, 19 / 255f, 28 / 255f, 1f);
        static readonly Color BtnOn = MvUi.Hex("#3fa85a");
        static readonly Color BtnOff = MvUi.Hex("#4a4656");
        static readonly Color CloseRed = MvUi.Hex("#c8463a");

        [SerializeField] Text titleText;
        [SerializeField] Text currencyText;
        [SerializeField] Text subText;
        [SerializeField] RectTransform tabBar;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] Text emptyText;
        [SerializeField] Button closeButton;
        [SerializeField] Button confirmButton;
        [SerializeField] Button dimButton;

        readonly List<GameObject> _rows = new List<GameObject>();
        readonly List<GameObject> _tabs = new List<GameObject>();
        bool _built;

        public event Action ClosePressed;
        public event Action DimPressed;
        public event Action<string> ActionPressed;
        public event Action<string> TabPressed;

        /// <summary>마지막으로 그린 본문 칸 수(시험·확인용).</summary>
        public int RowObjectCount => _rows.Count;
        public Button ConfirmButton { get { EnsureBuilt(); return confirmButton; } }

        string _confirmId;

        void Awake() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            if (!titleText) Build((RectTransform)transform);
            if (closeButton) closeButton.onClick.AddListener(() => ClosePressed?.Invoke());
            if (dimButton) dimButton.onClick.AddListener(() => DimPressed?.Invoke());
            if (confirmButton) confirmButton.onClick.AddListener(() => { if (_confirmId != null) ActionPressed?.Invoke(_confirmId); });
        }

        public void Render(MetaPanelModel m)
        {
            EnsureBuilt();
            titleText.text = m.Title;
            currencyText.text = m.Currencies;
            subText.text = m.Sub;
            subText.gameObject.SetActive(!string.IsNullOrEmpty(m.Sub));

            Clear(_tabs);
            tabBar.gameObject.SetActive(m.Tabs.Count > 0);
            foreach (var t in m.Tabs) _tabs.Add(Tab(t));

            Clear(_rows);
            int focus = -1;
            for (int i = 0; i < m.Rows.Count; i++)
            {
                var row = m.Rows[m.BottomUp ? m.Rows.Count - 1 - i : i];
                if (row.Id == m.FocusRowId) focus = i;
                _rows.Add(Row(row));
            }
            emptyText.text = m.Rows.Count == 0 ? m.Empty : "";

            _confirmId = m.Confirm?.Id;
            confirmButton.gameObject.SetActive(m.Confirm != null);
            if (m.Confirm != null)
            {
                confirmButton.interactable = m.Confirm.Enabled;
                confirmButton.GetComponentInChildren<Text>().text = m.Confirm.Text;
                confirmButton.GetComponent<Image>().color = m.Confirm.Enabled ? MvUi.Gold : MvUi.Hex("#5a5662");
            }

            if (focus >= 0 && _rows.Count > 1) scroll.verticalNormalizedPosition = 1f - (float)focus / (_rows.Count - 1);
            else if (m.BottomUp) scroll.verticalNormalizedPosition = 0f;
            else scroll.verticalNormalizedPosition = 1f;
        }

        static void Clear(List<GameObject> list)
        {
            foreach (var go in list)
                if (go)
                {
                    go.SetActive(false);
                    if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
                }
            list.Clear();
        }

        GameObject Tab(MetaTab t)
        {
            var b = MvUi.Button(tabBar, "Tab_" + t.Id, t.Text + (string.IsNullOrEmpty(t.Sub) ? "" : "\n" + t.Sub),
                t.Selected ? MvUi.Gold : new Color(1, 1, 1, t.Locked ? .08f : .16f), t.Selected ? MvUi.Hex("#15131c") : MvUi.Ink, 2.3f * U);
            string id = t.Id;
            b.onClick.AddListener(() => TabPressed?.Invoke(id));
            return b.gameObject;
        }

        GameObject Row(MetaRow r)
        {
            bool head = r.Kind == MetaRowKind.Header || r.Kind == MetaRowKind.Banner;
            var bg = MvUi.Box(content, "Row_" + r.Id, r.Kind == MetaRowKind.Text ? Color.clear : head ? HeadColor : RowColor);
            if (r.Highlight) bg.color = new Color(MvUi.Gold.r, MvUi.Gold.g, MvUi.Gold.b, .35f);
            float h = r.Kind == MetaRowKind.Text ? 6 * U : head ? 8 * U : 13 * U;
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;

            var text = MvUi.Label(bg.transform, "Text", (head ? 3.4f : 2.8f) * U, r.Locked ? MvUi.InkDim : MvUi.Ink,
                head ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, head ? FontStyle.Bold : FontStyle.Normal);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            MvUi.Stretch(text.rectTransform);
            float actW = r.Actions.Count * 19 * U;
            text.rectTransform.offsetMin = new Vector2(2 * U, 0);
            text.rectTransform.offsetMax = new Vector2(-(actW + 2 * U), 0);
            text.text = Compose(r);

            for (int i = 0; i < r.Actions.Count; i++)
            {
                var a = r.Actions[i];
                var b = MvUi.Button(bg.transform, "Act_" + a.Id, a.Text, a.Enabled ? BtnOn : BtnOff, a.Enabled ? Color.white : MvUi.Hex("#a8a2b4"), 2.6f * U);
                b.interactable = a.Enabled;
                MvUi.Place((RectTransform)b.transform, new Vector2(1, .5f), new Vector2(1, .5f),
                    new Vector2(-(1 * U + (r.Actions.Count - 1 - i) * 19 * U), 0), new Vector2(18 * U, 8 * U));
                string id = a.Id;
                b.onClick.AddListener(() => ActionPressed?.Invoke(id));
            }
            return bg.gameObject;
        }

        static string Compose(MetaRow r)
        {
            string s = r.Locked ? "[잠김] " + r.Title : r.Title;
            if (!string.IsNullOrEmpty(r.Value)) s += "\n" + r.Value;
            if (!string.IsNullOrEmpty(r.Desc)) s += "\n" + r.Desc;
            return s;
        }

        void Build(RectTransform root)
        {
            // 막: Canvas 전체(05 §5.16 막). 누름 처리는 화면 코드가 정한다.
            var dim = MvUi.Box(root, "Dim", MvUi.Dim, true);
            MvUi.Stretch(dim.rectTransform);
            dim.rectTransform.offsetMin = new Vector2(-1000, -1000);
            dim.rectTransform.offsetMax = new Vector2(1000, 1000);
            dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;

            // 상자: 가운데 94u × 150u
            var box = MvUi.Box(root, "Box", BoxColor, true).rectTransform;
            MvUi.Place(box, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(94 * U, 150 * U));

            titleText = MvUi.Label(box, "Title", 5f * U, MvUi.Gold, TextAnchor.MiddleCenter);
            MvUi.Place(titleText.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -2 * U), new Vector2(76 * U, 8 * U));

            closeButton = MvUi.Button(box, "Close", "✕", CloseRed, Color.white, 3.6f * U);
            MvUi.Place((RectTransform)closeButton.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-2 * U, -2 * U), new Vector2(7 * U, 7 * U));

            currencyText = MvUi.Label(box, "Currencies", 2.8f * U, MvUi.Ink, TextAnchor.MiddleRight, FontStyle.Normal);
            MvUi.Place(currencyText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-3 * U, -11 * U), new Vector2(88 * U, 4 * U));

            subText = MvUi.Label(box, "Sub", 2.6f * U, MvUi.InkDim, TextAnchor.MiddleCenter, FontStyle.Normal);
            subText.horizontalOverflow = HorizontalWrapMode.Wrap;
            MvUi.Place(subText.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -15.5f * U), new Vector2(88 * U, 7 * U));

            tabBar = MvUi.Place(MvUi.Rect(box, "Tabs"), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -23 * U), new Vector2(88 * U, 8 * U));
            var hl = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 1 * U;
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = true;
            hl.childControlWidth = true;
            hl.childControlHeight = true;

            // 본문: ScrollRect + 세로 배치(1차는 칸을 매번 다시 만든다. 교체 지점: 재사용 풀)
            var view = MvUi.Box(box, "Scroll", new Color(0, 0, 0, .12f), true).rectTransform;
            MvUi.Stretch(view);
            view.offsetMin = new Vector2(3 * U, 16 * U);
            view.offsetMax = new Vector2(-3 * U, -32 * U);
            view.gameObject.AddComponent<RectMask2D>();
            content = MvUi.Rect(view, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 1.4f * U;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            emptyText = MvUi.Label(view, "Empty", 3f * U, MvUi.InkDim, TextAnchor.MiddleCenter);
            MvUi.Stretch(emptyText.rectTransform);

            confirmButton = MvUi.Button(box, "Confirm", "", MvUi.Gold, MvUi.Hex("#0b0b0e"), 4f * U);
            MvUi.Place((RectTransform)confirmButton.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 3 * U), new Vector2(88 * U, 10 * U));
        }
    }
}
