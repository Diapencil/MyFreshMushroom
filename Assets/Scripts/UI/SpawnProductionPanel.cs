using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    public sealed class SpawnProductionPanel : MonoBehaviour
    {
        [SerializeField] private GameManager session;
        [SerializeField] private ForestGridView forest;
        [SerializeField] private GameObject content;
        [SerializeField] private RectTransform recipeList;
        [SerializeField] private Button recipeButtonPrefab;
        [SerializeField] private Text detailsText;
        [SerializeField] private GameObject recipeTotals;
        [SerializeField] private Image mushroomIcon;
        [SerializeField] private Image spawnIcon;
        [SerializeField] private Text mushroomCostText;
        [SerializeField] private Text spawnOutputText;
        [SerializeField] private Text ownedText;
        [SerializeField] private Text countText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button produceButton;
        [SerializeField, Min(1)] private float popupHeight = 370;

        private readonly KoreanUiFont uiFont = new KoreanUiFont();
        private readonly List<Button> recipeButtons = new List<Button>();
        private readonly List<SpawnRecipeData> visibleRecipes = new List<SpawnRecipeData>();
        private SpawnRecipeData selectedRecipe;
        private int batches = 1;
        private string feedback = "";
        private InventoryService inventory;
        private FacilitySelection selection;
        private SpawnProductionService production;
        private TutorialManager tutorial;
        public RectTransform Content => (RectTransform)content.transform;
        public float PopupHeight => popupHeight;

        private void OnEnable()
        {
            if (session == null || forest == null || content == null || recipeList == null || recipeButtonPrefab == null
                || detailsText == null || recipeTotals == null || mushroomIcon == null || spawnIcon == null
                || mushroomCostText == null || spawnOutputText == null || ownedText == null
                || countText == null || feedbackText == null
                || decreaseButton == null || increaseButton == null || produceButton == null)
            {
                Debug.LogError("SpawnProductionPanel requires all scene references.", this);
                return;
            }
            if (!session.Initialize()) return;
            forest.Initialize();
            inventory = session.Inventory;
            production = session.SpawnProduction;
            selection = forest.Selection;
            tutorial = session.Tutorial;
            if (tutorial != null) tutorial.Changed += Refresh;
            foreach (var label in content.GetComponentsInChildren<Text>(true)) uiFont.Apply(label);
            decreaseButton.onClick.AddListener(Decrease);
            increaseButton.onClick.AddListener(Increase);
            produceButton.onClick.AddListener(Produce);
            inventory.Changed += Refresh;
            selection.Changed += OnSelectionChanged;
            OnSelectionChanged(selection.Selected);
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.Changed -= Refresh;
            if (selection != null) selection.Changed -= OnSelectionChanged;
            if (tutorial != null) tutorial.Changed -= Refresh;
            tutorial = null;
            if (decreaseButton != null) decreaseButton.onClick.RemoveListener(Decrease);
            if (increaseButton != null) increaseButton.onClick.RemoveListener(Increase);
            if (produceButton != null) produceButton.onClick.RemoveListener(Produce);
            if (content != null) content.SetActive(false);
            inventory = null;
            selection = null;
            production = null;
        }

        private bool WorkbenchSelected => selection != null && selection.Selected != null
            && selection.Selected.Definition.Kind == FacilityKind.SpawnWorkbench;

        private void OnSelectionChanged(FacilityState facility)
        {
            content.SetActive(false);
            if (!WorkbenchSelected) return;
            batches = 1;
            feedback = "";
            RebuildRecipes();
            Refresh();
        }

        private void RebuildRecipes()
        {
            foreach (var button in recipeButtons)
            {
                button.gameObject.SetActive(false);
                Destroy(button.gameObject);
            }
            recipeButtons.Clear();
            visibleRecipes.Clear();
            selectedRecipe = null;
            foreach (var recipe in production.Recipes)
            {
                if (!production.IsAvailable(recipe)) continue;
                var button = Instantiate(recipeButtonPrefab, recipeList);
                var label = button.GetComponentInChildren<Text>(true);
                var icon = button.transform.Find("Icon").GetComponent<Image>();
                var mushroom = recipe.Mushroom;
                ApplyIcon(icon, mushroom.Icon);
                label.text = mushroom.DisplayName;
                label.enabled = mushroom.Icon == null;
                if (label.enabled) uiFont.Apply(label);
                button.onClick.AddListener(() => SelectRecipe(recipe));
                button.gameObject.SetActive(true);
                recipeButtons.Add(button);
                visibleRecipes.Add(recipe);
                if (selectedRecipe == null) selectedRecipe = recipe;
            }
        }

        private void SelectRecipe(SpawnRecipeData recipe)
        {
            if (!WorkbenchSelected || !production.IsAvailable(recipe)) return;
            selectedRecipe = recipe;
            batches = 1;
            feedback = "";
            Refresh();
        }

        private void Decrease()
        {
            if (!WorkbenchSelected || batches <= 1) return;
            batches--;
            feedback = "";
            Refresh();
        }

        private void Increase()
        {
            if (!WorkbenchSelected || !CanIncrease()) return;
            batches++;
            feedback = "";
            Refresh();
        }

        private void Produce()
        {
            if (!WorkbenchSelected || selectedRecipe == null) return;
            if (production.TryGetTotals(selectedRecipe, batches, out _, out int output)
                && production.TryProduce(selectedRecipe, batches))
                feedback = $"종균 {output}개를 만들었습니다.";
            else feedback = "보유량과 제작 수량을 확인해 주세요.";
            Refresh();
        }

        private void Refresh()
        {
            if (production == null || !WorkbenchSelected) return;
            var mushroom = selectedRecipe?.Mushroom;
            int owned = mushroom == null ? 0 : inventory.State.GetAmounts(mushroom.Id).Mushrooms;
            if (mushroom != null)
                batches = Mathf.Min(batches, Mathf.Max(1, owned / selectedRecipe.MushroomCost));

            bool valid = production.TryGetTotals(selectedRecipe, batches, out int cost, out int output);
            for (int i = 0; i < recipeButtons.Count; i++)
                recipeButtons[i].interactable = visibleRecipes[i] != selectedRecipe;
            countText.text = valid ? $"{batches}회" : "";
            decreaseButton.interactable = valid && batches > 1;
            increaseButton.interactable = CanIncrease();
            produceButton.interactable = valid && production.CanProduce(selectedRecipe, batches);
            recipeTotals.SetActive(valid);
            if (!valid)
            {
                detailsText.text = "제작 가능한 버섯이 없습니다.";
                ownedText.text = "";
                feedbackText.text = feedback;
                return;
            }
            detailsText.text = mushroom.DisplayName;
            ApplyIcon(mushroomIcon, mushroom.Icon);
            ApplyIcon(spawnIcon, mushroom.SpawnIcon);
            mushroomCostText.text = mushroom.Icon == null ? $"버섯 {cost}개" : $"{cost}";
            spawnOutputText.text = mushroom.SpawnIcon == null ? $"종균 {output}개" : $"{output}";
            ownedText.text = $"보유 버섯: {owned}개";
            feedbackText.text = feedback.Length > 0 ? feedback
                : !production.CanChooseBatchCount(selectedRecipe, batches) ? "튜토리얼 안내 순서대로 진행해 주세요."
                : owned < cost ? "버섯이 부족합니다."
                : !produceButton.interactable ? "종균 수량 한도를 초과합니다." : "";
        }

        private bool CanIncrease() => batches < int.MaxValue && production.CanProduce(selectedRecipe, batches + 1);

        private static void ApplyIcon(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private void OnDestroy() => uiFont.Dispose();
    }
}
