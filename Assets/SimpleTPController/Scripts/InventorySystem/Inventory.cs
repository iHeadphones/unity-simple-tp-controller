using System;
using System.Linq;
using UnityEngine;

namespace ThirdPersonController.InventorySystem
{
    /// <summary>
    /// Allows for basic inventory management.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [SerializeField] private ItemCollection[] m_ItemCollections = null;
        [SerializeField] private Vector3 m_DropOffset = new Vector3(0, 1, 1);

        /// <summary>
        /// The item collections within this inventory.
        /// </summary>
        public ItemCollection[] itemCollections
        {
            get { return m_ItemCollections; }
        }

        /// <summary>
        /// Invoked when an item is added to the inventory (and not combined)
        /// </summary>
        public event Action<ItemDataInstance, ItemCollection> itemAdded;

        /// <summary>
        /// Invoked when an item is dropped from the inventory.
        /// </summary>
        public event Action<GameObject> itemDropped;

        /// <summary>
        /// Awake is called when the script instance is being loaded.
        /// </summary>
        protected virtual void Awake()
        {
            InitializeCollections();
        }

        protected virtual void InitializeCollections()
        {
            foreach (var collection in m_ItemCollections)
            {
                collection.Initialize(this);
            }
        }

        /// <summary>
        /// Add an inventory item instance to the inventory.
        /// </summary>
        /// <param name="itemInstance">The item instance.</param>
        public virtual bool Add(IItemInstance itemInstance)
        {
            var data = new ItemDataInstance(itemInstance.baseData, itemInstance.stack);
            if (data.stack <= 0)
            {
                return false;
            }

            var collection = GetBestCollectionForItem(data);
            CombineWithExistingItems(data, collection);
            Destroy(itemInstance.gameObject);

            // Insert this item into the first available (empty) slot.
            collection.Insert(data);
            itemAdded?.Invoke(data, collection);

            return true;
        }

        /// <summary>
        /// Remove the given count of items with the given type from the given collection.
        /// </summary>
        /// <param name="itemType">The item type to remove.</param>
        /// <param name="count">The count of items to remove.</param>
        /// <param name="collection">The collection to remove the items from.</param>
        public virtual int RemoveItems(InventoryItem itemType, uint count, ItemCollection collection)
        {
            var items = collection.items.Where(x => x != null && x.item == itemType).ToArray();
            var removedCount = 0;

            for (int i = 0; i < items.Length; i++)
            {
                var itemData = items[i];
                var stackToRemove = 0u;

                for (int j = 0; j < itemData.stack; j++)
                {
                    if (count == 0)
                    {
                        break;
                    }

                    count--;
                    stackToRemove++;
                    removedCount++;
                }

                if (itemData.stack - stackToRemove == 0)
                {
                    collection.SetSlot(null, (uint)collection.GetSlot(itemData));
                }
                else
                {
                    itemData.SetStack(itemData.stack - stackToRemove);
                }

                if (count == 0)
                {
                    break;
                }
            }

            return removedCount;
        }

        /// <summary>
        /// Distributes the item data among existing items within the target collection.
        /// </summary>
        /// <param name="data">The item data instance.</param>
        /// <param name="collection">The target collection.</param>
        protected virtual void CombineWithExistingItems(ItemDataInstance data, ItemCollection collection)
        {
            var existingItems = collection.items
                .Where(x => x != null && x.stack < x.item.maxStack && x.item.id == data.item.id)
                .ToArray();

            if (existingItems.Length > 0)
            {
                foreach (var itemData in existingItems)
                {
                    Combine(data, itemData);

                    if (data.stack == 0)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Moves the item to the next available collection, otherwise drops the item.
        /// </summary>
        /// <param name="itemData">The item data.</param>
        public virtual void AutoMoveItem(ItemDataInstance itemData, bool combineWithItems = true)
        {
            if (itemData == null)
            {
                return;
            }

            var collection = GetBestCollectionForItem(itemData);
            var currentSlot = (uint)m_ItemCollections.FirstOrDefault(x => x.Contains(itemData)).GetSlot(itemData);
            if (collection == null)
            {
                DropItem(itemData);
            }
            else
            {
                if (combineWithItems)
                {
                    CombineWithExistingItems(itemData, collection);
                }

                if (itemData.stack > 0)
                {
                    var allowedSlot = collection.GetFirstAllowedSlot(itemData);

                    if (collection.IsFullyOccupied() || allowedSlot == -1)
                    {
                        DropItem(itemData);
                    }
                    else
                    {
                        MoveItem(itemData, collection, (uint)allowedSlot);
                    }
                }
                else
                {
                    collection.SetSlot(null, currentSlot);
                }
            }
        }

        /// <summary>
        /// Drop a given item from the inventory.
        /// </summary>
        /// <param name="itemData">The items data instance.</param>
        /// <param name="dropPoint">The point at which to drop the item.</param>
        /// <param name="dropRotation">The rotation of the dropped item.</param>
        public virtual GameObject DropItem(ItemDataInstance itemData)
        {
            var fromCollection = itemCollections.FirstOrDefault(x => x.Contains(itemData));
            fromCollection.SetSlot(null, (uint)fromCollection.GetSlot(itemData));

            var objInstance = Instantiate(itemData.item.dropObject, transform.position + transform.TransformDirection(m_DropOffset), transform.rotation);
            itemDropped?.Invoke(objInstance);

            var itemInstance = objInstance.GetComponent<IItemInstance>();
            itemInstance?.SetStack(itemData.stack);
            
            return objInstance;
        }

        /// <summary>
        /// Move the item to the given slot in its current collection.
        /// </summary>
        /// <param name="itemData">The item to move.</param>
        /// <param name="slot">The slot to move the item to.</param>
        public virtual void MoveItem(ItemDataInstance itemData, uint slot)
        {
            MoveItem(itemData, itemCollections.FirstOrDefault(x => x.Contains(itemData)), slot);
        }

        /// <summary>
        /// Move an item from its current slot in its current collection to the target collection in the target slot.
        /// </summary>
        /// <param name="itemData">The item to move.</param>
        /// <param name="collection">The collection to move it to.</param>
        /// <param name="slot">The slot to move the item to.</param>
        public virtual void MoveItem(ItemDataInstance itemData, ItemCollection collection, uint slot)
        {
            if (!collection.AllowItem(itemData))
            {
                return;
            }

            if (!collection.SlotAllows(itemData, slot))
            {
                return;
            }

            var fromCollection = itemCollections.FirstOrDefault(x => x.Contains(itemData));
            var fromSlot = fromCollection.GetSlot(itemData);
            var existingItem = collection.items.ElementAt((int)slot);
            
            if (itemData == existingItem)
            {
                return;
            }

            if (existingItem != null)
            {
                if (collection.SlotAllows(existingItem, slot) && fromCollection.SlotAllows(existingItem, (uint)fromSlot))
                {
                    if (existingItem.item.id == itemData.item.id && existingItem.stack < existingItem.item.maxStack)
                    {
                        Combine(itemData, existingItem);
                    }
                    else
                    {
                        Swap(itemData, fromCollection, existingItem, collection);
                    }
                }
            }
            else
            {
                fromCollection.SetSlot(null, (uint)fromSlot);
                collection.SetSlot(itemData, slot);
            }
        }

        /// <summary>
        /// Swaps item1 with item2.
        /// </summary>
        /// <param name="item1">The first item.</param>
        /// <param name="item2">The second item.</param>
        protected virtual void Swap(
            ItemDataInstance item1, ItemCollection item1Collection, 
            ItemDataInstance item2, ItemCollection item2Collection)
        {
            item1Collection.SetSlot(item2, (uint)item1Collection.GetSlot(item1));
            item2Collection.SetSlot(item1, (uint)item2Collection.GetSlot(item2));
        }

        /// <summary>
        /// Attempts to combine item1 with item2.
        /// </summary>
        /// <param name="item1">The item to combine with item2.</param>
        /// <param name="item2">The item receiving the combination.</param>
        public virtual void Combine(ItemDataInstance item1, ItemDataInstance item2)
        {
            var amount1 = item1.stack;
            var amount2 = item2.stack;

            while (amount2 < item2.item.maxStack && amount1 > 0)
            {
                amount1--;
                amount2++;
            }

            item1.SetStack(amount1);
            item2.SetStack(amount2);
        }

        /// <summary>
        /// Gets the highest priority collection for the given item where the
        /// collection allows the item.
        /// </summary>
        /// <param name="itemData">The item data.</param>
        public virtual ItemCollection GetBestCollectionForItem(ItemDataInstance itemData)
        {
            return Array
                .FindAll(m_ItemCollections, x => x.AllowItem(itemData) && !x.IsFull())
                .Where(x => !x.Contains(itemData))
                .OrderBy(x => x.priority)
                .FirstOrDefault();
        }
        
        /// <summary>
        /// Gets first the collection with the given name.
        /// </summary>
        /// <param name="collectionName">The collection name.</param>
        public virtual ItemCollection GetCollection(string collectionName)
        {
            return m_ItemCollections.FirstOrDefault(x => x.name == collectionName);
        }
    }
}