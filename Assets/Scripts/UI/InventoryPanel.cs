using UnityEngine;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    [DisallowMultipleComponent]
    public sealed class InventoryPanel : MonoBehaviour
    {
        [SerializeField] private GameManager session;
        [SerializeField] private Text quantitiesText;

        private InventoryService inventory;
        private readonly KoreanUiFont uiFont = new KoreanUiFont();

        private void OnEnable()
        {
            if (session == null || quantitiesText == null)
            {
                Debug.LogError("InventoryPanel requires a session and a quantity text reference.", this);
                return;
            }

            uiFont.Apply(quantitiesText);
            if (!session.Initialize())
                return;

            inventory = session.Inventory;
            inventory.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.Changed -= Refresh;
            inventory = null;
        }

        private void Refresh()
        {
            var mushroom = session.StartingMushroom;
            var amounts = inventory.State.GetAmounts(mushroom.Id);
            quantitiesText.text = $"{mushroom.DisplayName}\n버섯: {amounts.Mushrooms}개    종균: {amounts.Spawn}개";
        }

        private void OnDestroy() => uiFont.Dispose();
    }
}
