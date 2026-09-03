using System;

// What kind of thing an item is. Drives the tab row and nothing else, so
// adding a category is one entry here plus one entry in InventoryUI.Tabs.
public enum ItemCategory
{
    Consumable,   // food, water, medicine
    Tool,         // things carried and used
    Material,     // crafting stock
    Key           // story items - never droppable
}

// The procedural glyph drawn on a slot. There is no item art in the project,
// so an icon is a shape plus a colour, painted by ItemIcon.
public enum ItemShape
{
    Can, Bottle, Bandage, Wrench, Battery, Scrap, Cloth, Key, Note
}

// One item type. Immutable description of a thing, not a thing the player owns
// - what the player owns is an ItemStack pointing at one of these.
//
// Every item in the game is declared in ItemCatalog. Nothing constructs these
// anywhere else, which is what keeps "what items exist" answerable by opening
// a single file.
[Serializable]
public class ItemDefinition
{
    // Stable across builds: it is what a save file stores. Renaming an id
    // orphans that item in every existing save.
    public string id;

    public string displayName;
    public string description;

    public ItemCategory category;
    public ItemShape shape;

    // Slot tint. Kept per item rather than per category so two medicines can
    // still read as different things at a glance.
    public UnityEngine.Color tint;

    // 1 means the item never stacks and always takes a slot of its own.
    public int maxStack = 1;

    // Wording on the use button - "EAT" reads better than "USE" on a ration.
    // Empty means the item cannot be used and the button is hidden.
    public string useVerb = "";

    // Whether using it removes one from the stack.
    public bool consumeOnUse = true;

    public bool canDrop = true;

    public bool CanUse { get { return !string.IsNullOrEmpty(useVerb); } }
    public bool Stacks { get { return maxStack > 1; } }
}
