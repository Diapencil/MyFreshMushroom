using System;
using UnityEngine;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    [DisallowMultipleComponent]
    public sealed class InventoryPanel : MonoBehaviour
    {
        [Serializable]
        private sealed class MushroomColumn
        {
            public MushroomData mushroom;
            public GameObject root;
            public Image mushroomIcon;
            public Image spawnIcon;
            public Text mushroomQuantity;
            public Text spawnQuantity;
        }

        [SerializeField] private GameManager session;
        [SerializeField] private MushroomColumn[] columns = Array.Empty<MushroomColumn>();
        [SerializeField, Min(0)] private float backgroundRightPadding = 5f;

        private InventoryService inventory;

        private void OnEnable()
        {
            if (session == null)
            {
                Debug.LogError("InventoryPanel requires a session reference.", this);
                return;
            }

            if (!session.Initialize())
                return;

            inventory = session.Inventory;
            inventory.Changed += Refresh;
            session.Progress.DiscoveryChanged += RefreshDiscovery;
            RefreshDiscovery();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.Changed -= Refresh;
                session.Progress.DiscoveryChanged -= RefreshDiscovery;
            }
            inventory = null;
        }

        private void RefreshDiscovery()
        {
            float backgroundWidth = 0f;
            foreach (var column in columns)
            {
                string id = column.mushroom.Id;
                bool visible = inventory.State.TryGetAmounts(id, out _)
                    && session.Progress.IsDiscovered(id);
                column.root.SetActive(visible);
                if (visible)
                {
                    var columnRect = (RectTransform)column.root.transform;
                    backgroundWidth = Mathf.Max(backgroundWidth, columnRect.offsetMax.x + backgroundRightPadding);
                    ApplyIcon(column.mushroomIcon, column.mushroom.Icon);
                    ApplyIcon(column.spawnIcon, column.mushroom.SpawnIcon);
                }
            }
            ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, backgroundWidth);
            Refresh();
        }

        private static void ApplyIcon(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.SetNativeSize();
        }

        private void Refresh()
        {
            foreach (var column in columns)
            {
                if (!column.root.activeSelf) continue;
                var amounts = inventory.State.GetAmounts(column.mushroom.Id);
                column.mushroomQuantity.text = amounts.Mushrooms.ToString();
                column.spawnQuantity.text = amounts.Spawn.ToString();
            }
        }
    }
}
