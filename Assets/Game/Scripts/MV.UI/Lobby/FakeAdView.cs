using System;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 가짜 광고 기본 겉(D-92 더미: 검은 바탕·글자·버튼만). 참조가 비면 자식을 만든다.
    /// 겉 교체 지점: <see cref="IFakeAdView"/>를 구현한 겉을 프리팹에 둔다.
    /// </summary>
    public class FakeAdView : MonoBehaviour, IFakeAdView
    {
        const float U = MvUi.U;

        [SerializeField] Text titleText;
        [SerializeField] Text subText;
        [SerializeField] Button watchButton;
        [SerializeField] Button closeButton;

        bool _built;

        public event Action WatchPressed;
        public event Action ClosePressed;

        public Button WatchButton { get { EnsureBuilt(); return watchButton; } }

        void Awake() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            if (!titleText) Build((RectTransform)transform);
            if (watchButton) watchButton.onClick.AddListener(OnWatch);
            if (closeButton) closeButton.onClick.AddListener(OnClose);
        }

        void OnWatch() => WatchPressed?.Invoke();
        void OnClose() => ClosePressed?.Invoke();

        public void SetTexts(string title, string sub)
        {
            EnsureBuilt();
            titleText.text = title;
            subText.text = sub;
        }

        void Build(RectTransform root)
        {
            // 전체 바탕 #0b0b0e, 누름 막음(05 §5.12).
            var bg = MvUi.Box(root, "Bg", MvUi.Hex("#0b0b0e"), true);
            MvUi.Stretch(bg.rectTransform);
            bg.rectTransform.offsetMin = new Vector2(-1000, -1000);
            bg.rectTransform.offsetMax = new Vector2(1000, 1000);

            titleText = MvUi.Label(root, "Title", 6 * U, Color.white, TextAnchor.MiddleCenter);
            MvUi.Place(titleText.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 14 * U), new Vector2(90 * U, 10 * U));
            subText = MvUi.Label(root, "Sub", 3 * U, MvUi.InkDim, TextAnchor.MiddleCenter, FontStyle.Normal);
            MvUi.Place(subText.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 6 * U), new Vector2(90 * U, 5 * U));

            watchButton = MvUi.Button(root, "Watch", "광고 보기(가짜)", MvUi.Hex("#ffd84a"), MvUi.Hex("#2a1608"), 5 * U); // ui.ad.watchFake [창작]
            MvUi.Place((RectTransform)watchButton.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -8 * U), new Vector2(56 * U, 11 * U));
            closeButton = MvUi.Button(root, "Close", "닫기", new Color(1, 1, 1, .08f), MvUi.Ink, 3.6f * U); // ui.common.close
            MvUi.Place((RectTransform)closeButton.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -22 * U), new Vector2(30 * U, 8 * U));
        }
    }
}
