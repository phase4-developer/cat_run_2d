using System;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 결과 기본 겉(D-92 더미: 막·상자·글자·버튼만, 스킨·연출 없음). 참조가 비면 자식을 만든다.
    /// 겉 교체 지점: <see cref="IResultView"/>를 구현한 겉을 프리팹에 둔다.
    /// </summary>
    public class ResultView : MonoBehaviour, IResultView, IResultAdView
    {
        const float U = MvUi.U;

        [SerializeField] Text titleText;
        [SerializeField] Text progressLeft;
        [SerializeField] Text progressRight;
        [SerializeField] RectTransform progressFill;
        [SerializeField] Text goldText;
        [SerializeField] Text diaText;
        [SerializeField] Text tokText;
        [SerializeField] Text statText;
        [SerializeField] Button collectButton;
        [SerializeField] Button x3Button;

        bool _built;

        public event Action CollectPressed;
        public event Action X3Pressed;

        public Button CollectButton { get { EnsureBuilt(); return collectButton; } }
        public Button X3Button { get { EnsureBuilt(); return x3Button; } }

        void Awake() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            if (!titleText) Build((RectTransform)transform);
            if (!x3Button) BuildX3((RectTransform)collectButton.transform.parent);
            if (collectButton) collectButton.onClick.AddListener(OnCollect);
            if (x3Button) x3Button.onClick.AddListener(OnX3);
        }

        void OnCollect() => CollectPressed?.Invoke();
        void OnX3() => X3Pressed?.Invoke();

        public void SetX3(string text, bool interactable)
        {
            EnsureBuilt();
            x3Button.GetComponentInChildren<Text>().text = text;
            x3Button.interactable = interactable;
            x3Button.GetComponent<Image>().color = interactable ? MvUi.Hex("#ffd84a") : new Color(.45f, .42f, .36f, 1);
        }

        void BuildX3(RectTransform box)
        {
            // 광고 버튼 AdButton 큰 모양(05 §5.6): 바탕 #ffd84a, 글 #2a1608. 「괜찮아요」 위.
            // 글 「×N 보상」은 ResultPresenter.Draw가 08_adPlacement mul로 채운다(여기서 배수를 박지 않는다).
            x3Button = MvUi.Button(box, "X3", "", MvUi.Hex("#ffd84a"), MvUi.Hex("#2a1608"), 5.6f * U); // ui.result.adX3
            MvUi.Place((RectTransform)x3Button.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -68 * U), new Vector2(70 * U, 10 * U));
            // 「괜찮아요」는 아래로 내린다(밑줄 글 버튼 자리).
            MvUi.Place((RectTransform)collectButton.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -81 * U), new Vector2(40 * U, 8 * U));
        }

        public void SetTitle(string text, Color color) { EnsureBuilt(); titleText.text = text; titleText.color = color; }

        public void SetProgress(string left, string right, float fill01)
        {
            EnsureBuilt();
            progressLeft.text = left;
            progressRight.text = right;
            MvUi.SetFill(progressFill, fill01, false);
        }

        public void SetRewards(ResultRewardText gold, ResultRewardText dia, ResultRewardText tok)
        {
            EnsureBuilt();
            goldText.text = gold.Name + "\n" + gold.Value;
            diaText.text = dia.Name + "\n" + dia.Value;
            tokText.text = tok.Name + "\n" + tok.Value;
        }

        public void SetStat(string text) { EnsureBuilt(); statText.text = text; }

        public void SetCollectInteractable(bool interactable) { EnsureBuilt(); collectButton.interactable = interactable; }

        void Build(RectTransform root)
        {
            // 막: 전체, 누름 막음(눌러도 안 닫힘). Canvas 전체를 덮도록 크게 둔다(05 §5.1.2 막).
            var dim = MvUi.Box(root, "Dim", MvUi.Dim, true);
            MvUi.Stretch(dim.rectTransform);
            dim.rectTransform.offsetMin = new Vector2(-1000, -1000);
            dim.rectTransform.offsetMax = new Vector2(1000, 1000);

            // 상자: 가운데, 너비 80%
            var box = MvUi.Place(MvUi.Rect(root, "Box"), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(80 * U, 110 * U));
            titleText = MvUi.Label(box, "Title", 7.6f * U, MvUi.Gold, TextAnchor.MiddleCenter);
            MvUi.Place(titleText.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(80 * U, 10 * U));

            progressLeft = MvUi.Label(box, "ProgLeft", 3.4f * U, MvUi.Ink, TextAnchor.MiddleLeft);
            MvUi.Place(progressLeft.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -14 * U), new Vector2(60 * U, 4.5f * U));
            progressRight = MvUi.Label(box, "ProgRight", 3.4f * U, MvUi.Ink, TextAnchor.MiddleRight);
            MvUi.Place(progressRight.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -14 * U), new Vector2(30 * U, 4.5f * U));
            var bar = MvUi.Box(box, "ProgBar", new Color(1, 1, 1, .1f));
            MvUi.Place(bar.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -19.7f * U), new Vector2(80 * U, 3 * U));
            progressFill = MvUi.Box(bar.transform, "Fill", MvUi.Gold).rectTransform;
            MvUi.SetFill(progressFill, 0, false);

            // 보상 3칸, 사이 2u
            float cw = (80 - 4) / 3f * U;
            goldText = Cell(box, 0, cw);
            diaText = Cell(box, 1, cw);
            tokText = Cell(box, 2, cw);

            statText = MvUi.Label(box, "Stat", 2.7f * U, MvUi.InkDim, TextAnchor.UpperCenter, FontStyle.Normal);
            MvUi.Place(statText.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -50 * U), new Vector2(80 * U, 16 * U));

            collectButton = MvUi.Button(box, "Collect", "괜찮아요", MvUi.Gold, MvUi.Hex("#0b0b0e"), 4.4f * U); // ui.result.next
            MvUi.Place((RectTransform)collectButton.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -72 * U), new Vector2(40 * U, 10 * U));
        }

        static Text Cell(RectTransform box, int i, float cw)
        {
            var cell = MvUi.Box(box, "Reward" + i, new Color(48 / 255f, 42 / 255f, 62 / 255f, .92f));
            MvUi.Place(cell.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * (cw + 2 * U), -25 * U), new Vector2(cw, 20 * U));
            var t = MvUi.Label(cell.transform, "Text", 4.4f * U, MvUi.Ink, TextAnchor.MiddleCenter);
            MvUi.Stretch(t.rectTransform);
            return t;
        }
    }
}
