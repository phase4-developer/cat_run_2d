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
            int n = codes?.Count ?? 0;
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
                b.interactable = !string.Equals(code, current, StringComparison.OrdinalIgnoreCase);
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
