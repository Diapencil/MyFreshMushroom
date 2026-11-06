using UnityEngine;
using UnityEngine.UI;
using System.Text;

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
            session.Progress.DiscoveryChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.Changed -= Refresh;
                session.Progress.DiscoveryChanged -= Refresh;
            }
            inventory = null;
        }

        private void Refresh()
        {
            var text = new StringBuilder();
            foreach (var mushroom in session.Mushrooms)
            {
                if (!session.Progress.IsDiscovered(mushroom.Id)) continue;
                if (text.Length > 0) text.AppendLine();
                var amounts = inventory.State.GetAmounts(mushroom.Id);
                text.Append(mushroom.DisplayName).Append('\n').Append("버섯: ").Append(amounts.Mushrooms)
                    .Append("개    종균: ").Append(amounts.Spawn).Append('개');
            }
            quantitiesText.text = text.ToString();
        }

        private void OnDestroy() => uiFont.Dispose();
    }
}
