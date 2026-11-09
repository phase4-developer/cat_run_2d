using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 로비 기본 겉(D-92 더미: 기본 상자·글자·버튼만, 정본 배치·스킨 맞춤 없음). 참조가 비면 자식을 만든다.
    /// 겉 교체 지점: 이 컴포넌트 대신 <see cref="ILobbyView"/>·<see cref="ILobbyMenuView"/>를 구현한 겉을 프리팹에 둔다.
    /// </summary>
    public class LobbyView : MonoBehaviour, ILobbyView, ILobbyMenuView
    {
        const float U = MvUi.U;

        [SerializeField] Text levelText;
        [SerializeField] Text areaText;
        [SerializeField] Text wealthText;
        [SerializeField] LobbyStartArea startArea;

        bool _built;

        // 사이드·강화 버튼(MV-UI-LOBBY 1차 더미 겉). 모델 항목 수만큼 만들고 남는 것은 숨긴다(표 행 추가 → 버튼 추가).
        sealed class SideCell { public Button Button; public Image Bg; public Text Title; public Text Sub; }
        sealed class UpCell { public string Key; public Button Button; public Image Bg; public Text Title; public Text Level; public Text Desc; public Text Price; public RectTransform Bar; public RectTransform Fill; public Text Prog; }

        static readonly Color SideBg = new Color(44 / 255f, 28 / 255f, 40 / 255f, .9f);
        static readonly Color LockedBg = new Color(.25f, .25f, .27f, .9f);
        static readonly Color UpBg = new Color(92 / 255f, 62 / 255f, 76 / 255f, .95f);
        static readonly Color UpOff = new Color(.3f, .27f, .29f, .95f);

        readonly Dictionary<string, SideCell> _sides = new Dictionary<string, SideCell>();
        readonly List<UpCell> _ups = new List<UpCell>();
        RectTransform _sideRoot, _upRoot;
        Text _roadLeft, _roadRight;
        RectTransform _roadFill;

        public event Action StartPressed;
        public event Action<string> SidePressed;
        public event Action<string> UpgradePressed;

        public LobbyStartArea StartArea { get { EnsureBuilt(); return startArea; } }

        void Awake() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            if (!levelText) Build((RectTransform)transform);
            if (startArea) startArea.Pressed += OnPressed;
        }

        void OnPressed() => StartPressed?.Invoke();

        void OnDestroy()
        {
            if (startArea) startArea.Pressed -= OnPressed;
        }

        public void SetLevel(string text) { EnsureBuilt(); levelText.text = text; }
        public void SetArea(string text) { EnsureBuilt(); areaText.text = text; }
        public void SetWealth(string gold, string souls, string dia)
        {
            EnsureBuilt();
            wealthText.text = "츄르 " + gold + "\n생선 " + souls + "\n캣닢 " + dia;
        }

        // ───────── ILobbyMenuView ─────────

        public void Render(LobbyMenuModel model)
        {
            EnsureBuilt();
            if (model == null) return;
            if (!_sideRoot) BuildMenu((RectTransform)transform);

            _roadLeft.text = model.RoadLeft;
            _roadRight.text = model.RoadRight;
            MvUi.SetFill(_roadFill, model.RoadFill, false);

            var seen = new HashSet<string>();
            int row = 0;
            foreach (var b in model.Side)
            {
                seen.Add(b.Id);
                var c = Side(b.Id);
                c.Button.gameObject.SetActive(b.Visible);
                if (!b.Visible) continue;
                c.Title.text = b.Title;
                c.Sub.text = b.Sub;
                c.Bg.color = b.Locked ? LockedBg : SideBg;
                c.Button.interactable = b.Enabled;
                // 왼쪽 세로 한 줄(정본 배치 맞춤 안 함, D-92).
                MvUi.Place((RectTransform)c.Button.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -row * 13.6f * U), new Vector2(19 * U, 12.4f * U));
                row++;
            }
            foreach (var kv in _sides)
                if (!seen.Contains(kv.Key)) kv.Value.Button.gameObject.SetActive(false);

            int n = model.Upgrades.Count;
            while (_ups.Count < n) _ups.Add(BuildUp(_upRoot, _ups.Count));
            float gap = 1.6f * U, width = 94 * U;
            float w = n > 0 ? (width - gap * (n - 1)) / n : width;
            for (int i = 0; i < _ups.Count; i++)
            {
                var c = _ups[i];
                bool on = i < n;
                c.Button.gameObject.SetActive(on);
                if (!on) continue;
                var u = model.Upgrades[i];
                c.Key = u.Key;
                c.Title.text = u.Title;
                c.Level.text = u.LevelText;
                c.Desc.text = u.Desc;
                c.Price.text = u.Price;
                c.Bar.gameObject.SetActive(u.HasProgress);
                if (u.HasProgress)
                {
                    MvUi.SetFill(c.Fill, u.Progress, false);
                    c.Prog.text = u.ProgressText;
                }
                c.Bg.color = u.Enabled ? UpBg : UpOff;
                c.Button.interactable = u.Enabled;
                MvUi.Place((RectTransform)c.Button.transform, Vector2.zero, Vector2.zero, new Vector2(i * (w + gap), 0), new Vector2(w, 34 * U));
            }
        }

        SideCell Side(string id)
        {
            if (_sides.TryGetValue(id, out var c)) return c;
            var btn = MvUi.Button(_sideRoot, "Side_" + id, "", SideBg, MvUi.Ink, 3.4f * U);
            c = new SideCell { Button = btn, Bg = btn.GetComponent<Image>(), Title = btn.GetComponentInChildren<Text>() };
            c.Title.alignment = TextAnchor.UpperCenter;
            c.Title.rectTransform.offsetMax = new Vector2(0, -1.4f * U);
            c.Sub = MvUi.Label(btn.transform, "Sub", 2.4f * U, MvUi.InkDim, TextAnchor.LowerCenter, FontStyle.Normal);
            MvUi.Stretch(c.Sub.rectTransform);
            c.Sub.rectTransform.offsetMin = new Vector2(0, 1.4f * U);
            btn.onClick.AddListener(() => SidePressed?.Invoke(id));
            _sides.Add(id, c);
            return c;
        }

        UpCell BuildUp(RectTransform parent, int index)
        {
            var c = new UpCell();
            var btn = MvUi.Button(parent, "Up" + index, "", UpBg, MvUi.Gold, 4f * U);
            c.Button = btn;
            c.Bg = btn.GetComponent<Image>();
            c.Title = btn.GetComponentInChildren<Text>();
            c.Title.alignment = TextAnchor.UpperCenter;
            c.Title.rectTransform.offsetMax = new Vector2(0, -1.4f * U);
            c.Level = Line(btn.transform, "Level", 2.5f * U, MvUi.InkDim, -7f);
            c.Bar = MvUi.Box(btn.transform, "Bar", new Color(14 / 255f, 13 / 255f, 18 / 255f, 1)).rectTransform;
            MvUi.Place(c.Bar, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -11f * U), new Vector2(24 * U, 3 * U));
            c.Fill = MvUi.Box(c.Bar, "Fill", MvUi.Hex("#3fa85a")).rectTransform;
            c.Prog = MvUi.Label(c.Bar, "Text", 2.4f * U, Color.white, TextAnchor.MiddleCenter);
            MvUi.Stretch(c.Prog.rectTransform);
            c.Desc = Line(btn.transform, "Desc", 2.4f * U, MvUi.Ink, -15f);
            c.Desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            c.Desc.rectTransform.sizeDelta = new Vector2(c.Desc.rectTransform.sizeDelta.x, 8 * U);
            c.Price = Line(btn.transform, "Price", 3.6f * U, MvUi.Gold, -26f);
            btn.onClick.AddListener(() => { if (c.Key != null) UpgradePressed?.Invoke(c.Key); });
            return c;
        }

        static Text Line(Transform parent, string name, float size, Color color, float topU)
        {
            var t = MvUi.Label(parent, name, size, color, TextAnchor.UpperCenter, FontStyle.Normal);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(.5f, 1);
            rt.anchoredPosition = new Vector2(0, topU * U);
            rt.sizeDelta = new Vector2(-1.6f * U, 4 * U);
            return t;
        }

        void BuildMenu(RectTransform root)
        {
            // 진행 상자(05 §5.4.2 .road): 글자·막대만.
            var road = MvUi.Box(root, "Road", MvUi.Panel);
            MvUi.Place(road.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-3 * U, -24.5f * U), new Vector2(74 * U, 8 * U));
            _roadLeft = MvUi.Label(road.transform, "Left", 2.3f * U, MvUi.Ink, TextAnchor.UpperLeft, FontStyle.Normal);
            MvUi.Place(_roadLeft.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(1.8f * U, -1.1f * U), new Vector2(50 * U, 3 * U));
            _roadRight = MvUi.Label(road.transform, "Right", 2.3f * U, MvUi.Ink, TextAnchor.UpperRight);
            MvUi.Place(_roadRight.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-1.8f * U, -1.1f * U), new Vector2(20 * U, 3 * U));
            var bar = MvUi.Box(road.transform, "Bar", new Color(1, 1, 1, .12f));
            MvUi.Place(bar.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 1.1f * U), new Vector2(70 * U, 1.7f * U));
            _roadFill = MvUi.Box(bar.transform, "Fill", MvUi.Hex("#1d6f69")).rectTransform;

            // 사이드·시스템 버튼 줄(왼쪽), 강화 버튼 줄(아래, left·right 3u, bottom 3.4u).
            _sideRoot = MvUi.Place(MvUi.Rect(root, "Side"), new Vector2(0, 1), new Vector2(0, 1), new Vector2(2.4f * U, -34 * U), new Vector2(19 * U, 120 * U));
            _upRoot = MvUi.Place(MvUi.Rect(root, "Upgrades"), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 3.4f * U), new Vector2(94 * U, 34 * U));
        }

        void Build(RectTransform root)
        {
            // 출발 영역(맨 아래 투명 전체, raycast 받음). 나머지 글·패널은 raycast 없음(05 §5.4.1).
            var area = MvUi.Box(root, "StartArea", new Color(0, 0, 0, 0), true);
            area.transform.SetAsFirstSibling();
            MvUi.Stretch(area.rectTransform);
            startArea = area.gameObject.AddComponent<LobbyStartArea>();

            var top = MvUi.Box(root, "TopPanel", MvUi.Panel);
            MvUi.Place(top.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(3 * U, -3 * U), new Vector2(50 * U, 16 * U));
            levelText = MvUi.Label(top.transform, "Level", 6.6f * U, MvUi.Ink, TextAnchor.UpperLeft);
            MvUi.Place(levelText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(2 * U, -1.5f * U), new Vector2(46 * U, 8 * U));
            areaText = MvUi.Label(top.transform, "Area", 4.2f * U, MvUi.Gold, TextAnchor.UpperLeft);
            MvUi.Place(areaText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(2 * U, -9.5f * U), new Vector2(46 * U, 5 * U));

            var wealth = MvUi.Box(root, "WealthPanel", MvUi.Panel);
            MvUi.Place(wealth.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-3 * U, -3 * U), new Vector2(30 * U, 16 * U));
            wealthText = MvUi.Label(wealth.transform, "Wealth", 3.5f * U, MvUi.Ink, TextAnchor.MiddleRight);
            MvUi.Stretch(wealthText.rectTransform);
            wealthText.rectTransform.offsetMax = new Vector2(-2 * U, 0);

            var hint = MvUi.Box(root, "Hint", new Color(40 / 255f, 24 / 255f, 36 / 255f, .78f));
            MvUi.Place(hint.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -35 * U), new Vector2(60 * U, 12 * U));
            var hintText = MvUi.Label(hint.transform, "Text", 4.4f * U, MvUi.Ink, TextAnchor.MiddleCenter);
            MvUi.Stretch(hintText.rectTransform);
            hintText.text = "드래그해서 출발\n<size=26>좌우 드래그 · 방향키로 이동, 공격은 자동</size>"; // ui.lobby.hint / ui.lobby.hintSub
        }
    }
}
