using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    internal sealed class GameplayPopup
    {
        private readonly RectTransform backdrop;
        private readonly RectTransform window;
        private readonly Text title;
        private readonly Text body;
        private readonly Button close;
        private readonly RectTransform scrollArea;
        private readonly RectTransform footer;
        private readonly Button confirm;
        private RectTransform content;
        private Transform contentParent;
        private int contentIndex;

        public RectTransform Window => window;
        public bool IsOpen => backdrop.gameObject.activeSelf;

        public GameplayPopup(RectTransform canvas, KoreanUiFont font, Action onClose, Action onConfirm)
        {
            backdrop = CreateImage("Popup Backdrop", canvas, new Color(0, 0, 0, 0.68f)).rectTransform;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;

            window = CreateImage("Popup Window", backdrop, new Color(0.09f, 0.16f, 0.13f)).rectTransform;
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);

            title = CreateText("Title", window, font, 25, TextAnchor.MiddleLeft);
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(20, -54);
            title.rectTransform.offsetMax = new Vector2(-64, 0);

            scrollArea = CreateRect("Popup Scroll Area", window);
            scrollArea.anchorMax = Vector2.one;
            scrollArea.offsetMin = new Vector2(20, 20);
            scrollArea.offsetMax = new Vector2(-20, -58);
            var hitArea = scrollArea.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            scrollArea.gameObject.AddComponent<RectMask2D>();
            body = CreateText("Details", scrollArea, font, 22, TextAnchor.UpperLeft);
            body.rectTransform.anchorMin = new Vector2(0, 1);
            body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.pivot = new Vector2(0.5f, 1);
            body.rectTransform.sizeDelta = Vector2.zero;
            var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = scrollArea;
            scroll.content = body.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;

            close = CreateButton("Close Popup", "×", window, font, onClose);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-28, -27);
            closeRect.sizeDelta = new Vector2(40, 40);

            footer = CreateRect("Confirmation Actions", window);
            footer.anchorMin = footer.anchorMax = new Vector2(1, 0);
            footer.pivot = new Vector2(1, 0);
            footer.anchoredPosition = new Vector2(-20, 20);
            footer.sizeDelta = new Vector2(248, 44);
            var cancel = CreateButton("Cancel Restoration", "취소", footer, font, onClose);
            ((RectTransform)cancel.transform).anchoredPosition = new Vector2(60, 22);
            ((RectTransform)cancel.transform).sizeDelta = new Vector2(120, 44);
            confirm = CreateButton("Confirm Restoration", "복원 확정", footer, font, onConfirm);
            ((RectTransform)confirm.transform).anchoredPosition = new Vector2(188, 22);
            ((RectTransform)confirm.transform).sizeDelta = new Vector2(120, 44);
            Hide();
        }

        public void ShowText(string heading, string description)
        {
            Show(heading, 360);
            SetText(description);
            body.rectTransform.anchoredPosition = Vector2.zero;
        }

        public void SetText(string description) => body.text = description;

        public void ShowConfirmation(string heading)
        {
            ShowText(heading, "");
            footer.gameObject.SetActive(true);
            scrollArea.offsetMin = new Vector2(20, 80);
        }

        public void SetConfirmationInteractable(bool value) => confirm.interactable = value;

        public void ShowContent(string heading, RectTransform panel, float height)
        {
            Show(heading, height);
            body.transform.parent.gameObject.SetActive(false);
            content = panel;
            contentParent = panel.parent;
            contentIndex = panel.GetSiblingIndex();
            panel.SetParent(window, false);
            panel.gameObject.SetActive(true);
            close.transform.SetAsLastSibling();
        }

        private void Show(string heading, float height)
        {
            Hide();
            title.text = heading;
            window.sizeDelta = new Vector2(520, height);
            body.transform.parent.gameObject.SetActive(true);
            backdrop.SetAsLastSibling();
            backdrop.gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
        }

        public void Hide()
        {
            if (backdrop == null) return;
            footer.gameObject.SetActive(false);
            scrollArea.offsetMin = new Vector2(20, 20);
            if (content != null)
            {
                content.gameObject.SetActive(false);
                if (contentParent != null)
                {
                    content.SetParent(contentParent, false);
                    content.SetSiblingIndex(contentIndex);
                }
                content = null;
                contentParent = null;
            }
            if (IsOpen && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            backdrop.gameObject.SetActive(false);
        }

        public void Destroy()
        {
            if (backdrop != null) UnityEngine.Object.Destroy(backdrop.gameObject);
        }

        internal static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.anchoredPosition = rect.sizeDelta = Vector2.zero;
            rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }

        internal static Image CreateImage(string name, Transform parent, Color color)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        internal static Text CreateText(string name, Transform parent, KoreanUiFont font, int size, TextAnchor alignment)
        {
            var label = CreateRect(name, parent).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font.Apply(label);
            label.fontSize = size;
            label.alignment = alignment;
            label.color = new Color(0.95f, 0.97f, 0.92f);
            label.raycastTarget = false;
            return label;
        }

        internal static Button CreateButton(string name, string caption, Transform parent, KoreanUiFont font, Action action)
        {
            var image = CreateImage(name, parent, new Color(0.22f, 0.36f, 0.27f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = CreateText("Label", image.transform, font, 20, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.text = caption;
            if (action != null) button.onClick.AddListener(() => action());
            return button;
        }
    }
}
