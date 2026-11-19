using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 <c>Popup_CommonSystem</c> 겉: Title·Contents 글자, Button Group(Button_OK·Button_Cancel), Button Close.
    /// 알림이면 Button_OK를 끈다(노션: 선택일 때만 OK 활성). Cancel과 X는 같은 닫기(취소)다.
    /// </summary>
    public class SystemPopupView : MonoBehaviour
    {
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text contents;
        [SerializeField] Button okButton;
        [SerializeField] TMP_Text okLabel;
        [SerializeField] Button cancelButton;
        [SerializeField] TMP_Text cancelLabel;
        [SerializeField] Button closeButton;
        [SerializeField] ScrollRect scroll;

        public event Action OkPressed;
        public event Action CancelPressed;

        bool _bound;

        void Awake() => Bind();

        void Bind()
        {
            if (_bound) return;
            _bound = true;
            if (okButton) okButton.onClick.AddListener(() => OkPressed?.Invoke());
            if (cancelButton) cancelButton.onClick.AddListener(() => CancelPressed?.Invoke());
            if (closeButton) closeButton.onClick.AddListener(() => CancelPressed?.Invoke());
        }

        public void Draw(string titleText, string contentsText, bool showOk, string okText, string cancelText)
        {
            Bind();
            if (title) title.text = titleText ?? "";
            if (contents) contents.text = contentsText ?? "";
            if (okButton) okButton.gameObject.SetActive(showOk);
            if (okLabel) okLabel.text = okText ?? "";
            if (cancelLabel) cancelLabel.text = cancelText ?? "";
            if (scroll) scroll.verticalNormalizedPosition = 1f; // 긴 본문은 처음부터
        }

        // 시험용 읽기·누르기
        public bool OkShown => okButton && okButton.gameObject.activeSelf;
        public string TitleShown => title ? title.text : null;
        public string ContentsShown => contents ? contents.text : null;
        public string CancelShown => cancelLabel ? cancelLabel.text : null;
        public void ClickOk() { if (okButton) okButton.onClick.Invoke(); }
        public void ClickCancel() { if (cancelButton) cancelButton.onClick.Invoke(); }
        public void ClickClose() { if (closeButton) closeButton.onClick.Invoke(); }
    }
}
