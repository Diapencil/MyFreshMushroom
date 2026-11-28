using UnityEngine;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    public sealed class SessionSettingsMenu : MonoBehaviour
    {
        internal const string ResetWarning = "현재까지의 진행 상황이 모두 삭제되며, 처음부터 다시 시작합니다.\n\n"
            + "삭제된 데이터는 복구할 수 없습니다.";
        [SerializeField] private GameManager session;
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameplayPopup popup;

        private readonly KoreanUiFont font = new KoreanUiFont();

        private void OnEnable()
        {
            if (session == null || settingsButton == null || popup == null)
            {
                Debug.LogError("SessionSettingsMenu requires its session, settings button and popup.", this);
                return;
            }
            if (!session.Initialize() || !popup.Initialize(font, Close, null)) return;
            font.Apply(settingsButton.GetComponentInChildren<Text>(true));
            settingsButton.onClick.AddListener(Open);
        }

        private void Open()
        {
            session.IsSettingsOpen = true;
            popup.ShowConfirmation("설정", "처음부터 시작하기", ShowResetConfirmation);
            popup.SetSecondaryAction("타이틀로 돌아가기", ReturnToTitle, true);
        }

        private void ShowResetConfirmation()
        {
            popup.ShowConfirmation("처음부터 시작하시겠습니까?", "처음부터 시작", Restart);
            popup.SetText(ResetWarning);
            popup.HideCloseButton();
        }

        private void Restart()
        {
            if (session.TryRestart()) popup.SetConfirmationInteractable(false);
            else popup.SetText(ResetWarning + "\n\n초기화 데이터를 저장하지 못했습니다. 다시 시도해 주세요.");
        }

        private void ReturnToTitle()
        {
            if (!session.TryReturnToTitle())
            {
                popup.SetText("진행 상황을 저장하지 못했습니다. 다시 시도해 주세요.");
                return;
            }
            popup.SetSecondaryAction("타이틀로 돌아가기", ReturnToTitle, false);
            popup.SetConfirmationInteractable(false);
        }

        private void Close()
        {
            session.IsSettingsOpen = false;
            popup.Hide();
        }

        private void OnDisable()
        {
            if (settingsButton != null) settingsButton.onClick.RemoveListener(Open);
            if (session != null && session.IsSettingsOpen && popup != null && popup.IsOpen) Close();
        }

        private void OnDestroy() => font.Dispose();
    }
}
