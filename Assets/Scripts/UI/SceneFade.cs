using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    public sealed class SceneFade : MonoBehaviour
    {
        [SerializeField] private Image cover;
        [SerializeField, Min(0)] private float coverSeconds = 0.2f;
        [SerializeField, Min(0)] private float revealSeconds = 0.3f;

        private static SceneFade active;
        private EventSystem navigationSystem;
        private bool previousNavigation;

        internal static bool IsRunning => active != null;

        internal static void Load(string sceneName)
        {
            if (IsRunning) return;
            var prefab = Resources.Load<GameObject>("SceneFade")?.GetComponent<SceneFade>();
            if (prefab == null)
            {
                Debug.LogError("The SceneFade prefab is missing from Resources.");
                SceneManager.LoadScene(sceneName);
                return;
            }

            active = Instantiate(prefab);
            DontDestroyOnLoad(active.gameObject);
            active.StartCoroutine(active.Transition(sceneName));
        }

        private IEnumerator Transition(string sceneName)
        {
            SetOpacity(0);
            BlockNavigation();
            SceneManager.sceneLoaded += OnSceneLoaded;
            yield return FadeTo(1, coverSeconds);
            yield return SceneManager.LoadSceneAsync(sceneName);
            // Keep the cover opaque through the incoming scene's first frame.
            yield return null;
            yield return FadeTo(0, revealSeconds);
            Destroy(gameObject);
        }

        private IEnumerator FadeTo(float opacity, float duration)
        {
            float start = cover.color.a;
            float elapsed = 0;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                SetOpacity(Mathf.Lerp(start, opacity, elapsed / duration));
            }
            SetOpacity(opacity);
        }

        private void SetOpacity(float opacity)
        {
            Color color = cover.color;
            color.a = opacity;
            cover.color = color;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BlockNavigation();

        private void BlockNavigation()
        {
            var current = EventSystem.current;
            if (current == navigationSystem) return;
            RestoreNavigation();
            navigationSystem = current;
            if (navigationSystem == null) return;
            previousNavigation = navigationSystem.sendNavigationEvents;
            navigationSystem.sendNavigationEvents = false;
        }

        private void RestoreNavigation()
        {
            if (navigationSystem != null) navigationSystem.sendNavigationEvents = previousNavigation;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RestoreNavigation();
            if (active == this) active = null;
        }
    }
}
