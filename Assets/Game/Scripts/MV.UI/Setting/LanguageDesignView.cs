using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 <c>Popup_Setting_Language</c> 겉. 언어 버튼 수는 고를 수 있는 언어 수(<see cref="IMvLanguageService.Codes"/>)로 정한다.
    /// 디자이너가 놓은 버튼을 차례로 쓰고, 모자라면 첫 버튼을 복제, 남으면 숨긴다. 글자는 그 언어로 적은 이름, 지금 언어 버튼은 누를 수 없다(정크버스터즈와 같음).
    /// </summary>
    public class LanguageDesignView : MonoBehaviour
    {
        [SerializeField] List<Button> buttons = new List<Button>();
        [SerializeField] TMP_Text title;
        [SerializeField] Button closeButton;

        readonly List<string> _codes = new List<string>();
        List<string> _designLabels; // 시안 버튼 글자(첫 그리기 전 그대로) = 시안 순서

        /// <summary>
        /// 시안 순서로 언어를 늘어놓는다(노션 [팝업] 언어설정: English → 한국어 → 日本語). 시안 버튼 글자와 언어 이름이 같은 자리에 두고,
        /// 시안에 없는 언어는 원래 순서대로 뒤에 붙인다. Locale 정렬값이 같으면 Unity가 이름순(English·Japanese·Korean)으로 주기 때문.
        /// </summary>
        public static List<string> DesignOrder(IReadOnlyList<string> codes, IReadOnlyList<string> designLabels, Func<string, string> displayName)
        {
            var rest = new List<string>(codes ?? Array.Empty<string>());
            var ordered = new List<string>();
            if (designLabels != null && displayName != null)
                foreach (var label in designLabels)
                {
                    int k = rest.FindIndex(c => string.Equals(displayName(c), label, StringComparison.Ordinal));
                    if (k < 0) continue;
                    ordered.Add(rest[k]);
                    rest.RemoveAt(k);
                }
            ordered.AddRange(rest);
            return ordered;
        }

        public event Action<string> LanguagePressed;
        public event Action ClosePressed;

        void Awake()
        {
            if (closeButton) closeButton.onClick.AddListener(() => ClosePressed?.Invoke());
        }

        /// <summary>언어 목록을 그린다. 지금 언어는 누를 수 없다.</summary>
        public void Draw(IReadOnlyList<string> codes, string current, Func<string, string> displayName)
        {
            if (title) title.text = MvUiText.Ui("ui.setting.language");
            if (_designLabels == null)
            {
                _designLabels = new List<string>();
                foreach (var db in buttons)
                {
                    var t = db ? db.GetComponentInChildren<TMP_Text>(true) : null;
                    if (t) _designLabels.Add(t.text);
                }
            }
            codes = DesignOrder(codes, _designLabels, displayName);
            int n = codes.Count;
            EnsureButtons(n);
            _codes.Clear();
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                if (!b) continue;
                bool used = i < n;
                b.gameObject.SetActive(used);
                if (!used) continue;
                string code = codes[i];
                _codes.Add(code);
                var label = b.GetComponentInChildren<TMP_Text>(true);
                if (label) label.text = displayName != null ? displayName(code) : code;
                b.interactable = true; // 지금 언어를 눌러도 닫힌다(노션: 버튼 클릭 시 팝업 종료)
                int index = i;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => LanguagePressed?.Invoke(_codes[index]));
            }
        }

        void EnsureButtons(int n)
        {
            buttons.RemoveAll(b => !b);
            if (buttons.Count == 0 || buttons.Count >= n) return;
            var template = buttons[0];
            while (buttons.Count < n)
                buttons.Add(Instantiate(template, template.transform.parent));
        }

        // 시험용 읽기
        public int ShownCount
        {
            get
            {
                int c = 0;
                foreach (var b in buttons) if (b && b.gameObject.activeSelf) c++;
                return c;
            }
        }

        public Button ButtonAt(int i) => i >= 0 && i < buttons.Count ? buttons[i] : null;
    }
}
