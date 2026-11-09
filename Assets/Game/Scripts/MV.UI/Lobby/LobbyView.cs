using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 로비 기본 겉(D-92 더미: 기본 상자·글자만, 정본 배치·스킨 맞춤 없음). 참조가 비면 자식을 만든다.
    /// 겉 교체 지점: 이 컴포넌트 대신 <see cref="ILobbyView"/>를 구현한 겉을 프리팹에 둔다.
    /// </summary>
    public class LobbyView : MonoBehaviour, ILobbyView
    {
        const float U = MvUi.U;

        [SerializeField] Text levelText;
        [SerializeField] Text areaText;
        [SerializeField] Text wealthText;
        [SerializeField] LobbyStartArea startArea;

        bool _built;

        public event Action StartPressed;

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
