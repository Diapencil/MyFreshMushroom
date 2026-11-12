using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    // The prefab owns the hierarchy and appearance; this component only presents it.
    public sealed class GameplayPopup : MonoBehaviour
    {
        [SerializeField] private RectTransform backdrop;
        [SerializeField] private RectTransform window;
        [SerializeField] private Text title;
        [SerializeField] private Text body;
        [SerializeField] private Button close;
        [SerializeField] private RectTransform scrollArea;
        [SerializeField] private RectTransform footer;
        [SerializeField] private Button confirm;
        [SerializeField] private Button cancel;
        [SerializeField] private Button hint;
        [SerializeField] private RectTransform content;
        [SerializeField, Min(1)] private float textHeight = 360;
        [SerializeField, Min(0)] private float bodyBottomPadding = 20;
        [SerializeField, Min(0)] private float confirmationBottomPadding = 80;

        private Action onClose;
        private Action restorationConfirm;
        private Action confirmationAction;
        private Action hintAction;
        private bool initialized;

        public RectTransform Window => window;
        public bool IsOpen => backdrop != null && backdrop.gameObject.activeSelf;

        public bool Initialize(KoreanUiFont font, Action closeAction, Action confirmAction)
        {
            if (backdrop == null || window == null || title == null || body == null || close == null
                || scrollArea == null || footer == null || confirm == null || cancel == null || hint == null)
            {
                Debug.LogError("GameplayPopup requires its prefab UI references.", this);
                return false;
            }
            onClose = closeAction;
            restorationConfirm = confirmAction;
            if (!initialized)
            {
                foreach (var label in GetComponentsInChildren<Text>(true)) font.Apply(label);
                close.onClick.AddListener(Close);
                cancel.onClick.AddListener(Close);
                confirm.onClick.AddListener(Confirm);
                hint.onClick.AddListener(ShowHint);
                initialized = true;
            }
            Hide();
            return true;
        }

        private void Close() => onClose?.Invoke();
        private void Confirm() => confirmationAction?.Invoke();
        private void ShowHint() => hintAction?.Invoke();

        public void ShowText(string heading, string description)
        {
            Show(heading, textHeight);
            SetText(description);
            body.rectTransform.anchoredPosition = Vector2.zero;
        }

        public void SetText(string description) => body.text = description;

        public void ShowConfirmation(string heading)
        {
            ShowText(heading, "");
            confirmationAction = restorationConfirm;
            confirm.interactable = true;
            cancel.GetComponentInChildren<Text>(true).text = "취소";
            confirm.GetComponentInChildren<Text>(true).text = "복원 확정";
            footer.gameObject.SetActive(true);
            SetBodyBottom(confirmationBottomPadding);
        }

        public void ShowExitConfirmation(bool hasRemaining, Action onConfirm, Action onHint)
        {
            ShowConfirmation("탐험 종료");
            confirmationAction = onConfirm;
            hintAction = onHint;
            cancel.GetComponentInChildren<Text>(true).text = "계속 탐색";
            confirm.GetComponentInChildren<Text>(true).text = "돌아가기(확정)";
            hint.gameObject.SetActive(hasRemaining);
        }

        public void SetConfirmationInteractable(bool value) => confirm.interactable = value;

        public void ShowContent(string heading, float height)
        {
            if (content == null)
            {
                Debug.LogError("This popup has no content assigned in its prefab.", this);
                return;
            }
            Show(heading, height);
            scrollArea.gameObject.SetActive(false);
            content.gameObject.SetActive(true);
        }

        private void Show(string heading, float height)
        {
            Hide();
            title.text = heading;
            window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            scrollArea.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            backdrop.gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
        }

        public void Hide()
        {
            if (backdrop == null) return;
            hint.gameObject.SetActive(false);
            confirmationAction = null;
            hintAction = null;
            footer.gameObject.SetActive(false);
            SetBodyBottom(bodyBottomPadding);
            if (content != null) content.gameObject.SetActive(false);
            if (IsOpen && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            backdrop.gameObject.SetActive(false);
        }

        private void SetBodyBottom(float padding)
        {
            var offset = scrollArea.offsetMin;
            offset.y = padding;
            scrollArea.offsetMin = offset;
        }

        private void OnDestroy()
        {
            if (close != null) close.onClick.RemoveListener(Close);
            if (cancel != null) cancel.onClick.RemoveListener(Close);
            if (confirm != null) confirm.onClick.RemoveListener(Confirm);
            if (hint != null) hint.onClick.RemoveListener(ShowHint);
        }
    }
}
