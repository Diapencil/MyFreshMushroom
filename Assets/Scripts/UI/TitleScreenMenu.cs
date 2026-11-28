using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    public sealed class TitleScreenMenu : MonoBehaviour
    {
        [SerializeField] private GameObject menu;
        [SerializeField] private Button newGame;
        [SerializeField] private Button continueGame;
        [SerializeField] private Button settings;
        [SerializeField] private Button quit;
        [SerializeField] private GameplayPopup popup;

        private readonly KoreanUiFont font = new KoreanUiFont();
        private Button lastSelected;
        private bool leaving;

        private void Start()
        {
            foreach (var label in menu.GetComponentsInChildren<Text>()) font.Apply(label);
            popup.Initialize(font, ClosePopup, null);
            newGame.onClick.AddListener(ConfirmNewGame);
            continueGame.onClick.AddListener(ContinueGame);
            settings.onClick.AddListener(OpenSettings);
            quit.onClick.AddListener(Quit);
            continueGame.interactable = SaveManager.HasSaveFiles();

            lastSelected = newGame;
            if (GameManager.TakeNewGameSaveFailure())
            {
                menu.SetActive(false);
                popup.ShowText("새 게임", "초기 데이터를 저장하지 못했습니다. 다시 시도해 주세요.");
            }
            else
                Select(lastSelected);
        }

        private void ConfirmNewGame()
        {
            if (leaving) return;
            if (!SaveManager.HasSaveFiles())
            {
                StartNewGame();
                return;
            }

            lastSelected = newGame;
            menu.SetActive(false);
            popup.ShowConfirmation("새 게임을 시작하시겠습니까?", "새 게임", StartNewGame);
            popup.SetText(SessionSettingsMenu.ResetWarning);
            popup.HideCloseButton();
        }

        private void StartNewGame()
        {
            if (leaving) return;
            leaving = true;
            GameManager.StartNewGame();
        }

        private void ContinueGame()
        {
            if (leaving || !SaveManager.HasSaveFiles()) return;
            leaving = true;
            SceneFade.Load(GameManager.MainScene);
        }

        private void OpenSettings()
        {
            if (leaving) return;
            lastSelected = settings;
            menu.SetActive(false);
            bool fullscreen = Screen.fullScreen;
            popup.ShowConfirmation("설정", fullscreen ? "창 모드로 전환" : "전체 화면으로 전환", () =>
            {
                Screen.fullScreen = !fullscreen;
                ClosePopup();
            });
            popup.SetText(fullscreen ? "화면 모드   전체 화면" : "화면 모드   창 모드");
        }

        private void ClosePopup()
        {
            popup.Hide();
            menu.SetActive(true);
            Select(lastSelected);
        }

        private static void Select(Button button)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private void Quit()
        {
            if (!leaving) Application.Quit();
        }

        private void OnDestroy()
        {
            newGame.onClick.RemoveListener(ConfirmNewGame);
            continueGame.onClick.RemoveListener(ContinueGame);
            settings.onClick.RemoveListener(OpenSettings);
            quit.onClick.RemoveListener(Quit);
            font.Dispose();
        }
    }
}
