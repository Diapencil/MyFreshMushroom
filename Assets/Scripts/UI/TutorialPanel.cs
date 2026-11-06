using UnityEngine;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    public sealed class TutorialPanel : MonoBehaviour
    {
        [SerializeField] private GameManager session;
        [SerializeField] private ForestGridView forest;
        [SerializeField] private Text guidanceText;
        private TutorialManager tutorial;
        private readonly KoreanUiFont uiFont = new KoreanUiFont();

        private void OnEnable()
        {
            if (session == null || forest == null || guidanceText == null)
            {
                Debug.LogError("TutorialPanel requires GameManager, forest and guidance text.", this);
                return;
            }
            if (!session.Initialize()) return;
            tutorial = session.Tutorial;
            if (tutorial == null)
            {
                gameObject.SetActive(false);
                return;
            }
            uiFont.Apply(guidanceText);
            tutorial.Changed += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            guidanceText.text = tutorial.Guidance;
            foreach (var view in forest.Facilities)
                view.SetTutorialTarget(ReferenceEquals(view.State, tutorial.GuidanceTarget));
        }

        private void OnDisable()
        {
            if (tutorial != null) tutorial.Changed -= Refresh;
            tutorial = null;
            if (forest != null)
                foreach (var view in forest.Facilities) view.SetTutorialTarget(false);
        }

        private void OnDestroy() => uiFont.Dispose();
    }
}
