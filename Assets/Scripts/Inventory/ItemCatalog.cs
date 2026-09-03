using System.Collections.Generic;
using UnityEngine;

// Every item that exists in the game, in one file.
//
// Same idea as SettingsCatalog: adding an item is one entry here and nothing
// else. The inventory model, the grid, the tabs, the detail panel and the save
// format all read from this list, so none of them need touching.
//
// Ids are what save files store. Renaming one orphans that item in every
// existing save, so treat the id column as permanent and change displayName
// instead.
public static class ItemCatalog
{
    static Dictionary<string, ItemDefinition> byId;
    static List<ItemDefinition> all;

    public static List<ItemDefinition> All { get { Build(); return all; } }

    public static ItemDefinition Get(string id)
    {
        Build();
        if (string.IsNullOrEmpty(id)) return null;
        return byId.TryGetValue(id, out var def) ? def : null;
    }

    public static bool Exists(string id) { return Get(id) != null; }

    // -------------------------------------------------------------- the list
    static void Build()
    {
        if (all != null) return;

        all = new List<ItemDefinition>
        {
            // ---- consumables
            Item("ration",   "Canned Ration",  ItemCategory.Consumable, ItemShape.Can,
                 new Color(0.72f, 0.62f, 0.36f), 8, "EAT",
                 "Dented but sealed. Three days past the printed date, which stopped meaning anything a long time ago."),

            Item("water",    "Water Flask",    ItemCategory.Consumable, ItemShape.Bottle,
                 new Color(0.45f, 0.68f, 0.75f), 4, "DRINK",
                 "Rainwater, boiled twice. It tastes of the pot more than of water."),

            Item("bandage",  "Clean Bandage",  ItemCategory.Consumable, ItemShape.Bandage,
                 new Color(0.86f, 0.83f, 0.74f), 6, "USE",
                 "Cut from a bedsheet and boiled. Logan keeps a stack of them by the stove."),

            // ---- tools
            Item("wrench",   "Steel Wrench",   ItemCategory.Tool, ItemShape.Wrench,
                 new Color(0.55f, 0.57f, 0.60f), 1, "",
                 "Heavy at one end, worn smooth at the other. Opens most things that were meant to stay shut."),

            Item("battery",  "Spent Battery",  ItemCategory.Tool, ItemShape.Battery,
                 new Color(0.63f, 0.55f, 0.28f), 3, "",
                 "A little charge left in it, maybe. Worth carrying until something needs one."),

            // ---- materials
            Item("scrap",    "Scrap Metal",    ItemCategory.Material, ItemShape.Scrap,
                 new Color(0.50f, 0.44f, 0.38f), 20, "",
                 "Prised off a doorframe. Everything out here is made of something that used to be something else."),

            Item("cloth",    "Torn Cloth",     ItemCategory.Material, ItemShape.Cloth,
                 new Color(0.68f, 0.58f, 0.52f), 12, "",
                 "Enough for a bandage, or a filter, or nothing at all."),

            // ---- key items
            KeyItem("bunker_key", "Bunker Key", ItemShape.Key,
                 new Color(0.76f, 0.66f, 0.35f),
                 "Cold, and heavier than a key should be. Logan handed it over without saying what it opens."),

            KeyItem("mother_note", "Folded Note", ItemShape.Note,
                 new Color(0.85f, 0.80f, 0.68f),
                 "Your mother's handwriting, softened by however many times it has been folded and opened again."),
        };

        byId = new Dictionary<string, ItemDefinition>(all.Count);
        foreach (var def in all)
        {
            if (byId.ContainsKey(def.id))
            {
                Debug.LogError("[ItemCatalog] duplicate item id '" + def.id + "' - the later one is ignored.");
                continue;
            }
            byId[def.id] = def;
        }
    }

    // ------------------------------------------------------------- shorthand
    static ItemDefinition Item(string id, string name, ItemCategory category, ItemShape shape,
                               Color tint, int maxStack, string useVerb, string description)
    {
        return new ItemDefinition
        {
            id = id,
            displayName = name,
            description = description,
            category = category,
            shape = shape,
            tint = tint,
            maxStack = maxStack,
            useVerb = useVerb,
            consumeOnUse = true,
            canDrop = true,
        };
    }

    // Key items are single, unusable and cannot be thrown away - losing one
    // would leave the player unable to finish whatever it belongs to.
    static ItemDefinition KeyItem(string id, string name, ItemShape shape, Color tint, string description)
    {
        return new ItemDefinition
        {
            id = id,
            displayName = name,
            description = description,
            category = ItemCategory.Key,
            shape = shape,
            tint = tint,
            maxStack = 1,
            useVerb = "",
            consumeOnUse = false,
            canDrop = false,
        };
    }
}
