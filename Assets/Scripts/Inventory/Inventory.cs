using System;
using System.Collections.Generic;
using UnityEngine;

// One occupied slot: which item, and how many.
[Serializable]
public class ItemStack
{
    public string id;
    public int count;

    public ItemDefinition Definition { get { return ItemCatalog.Get(id); } }
    public bool IsEmpty { get { return string.IsNullOrEmpty(id) || count <= 0; } }
}

// The serialised shape of the bag. Data only - JsonUtility writes this
// straight into the save file.
[Serializable]
public class InventoryData
{
    public int slotCount = 20;
    public List<ItemStack> stacks = new List<ItemStack>();
}

// What the player is carrying.
//
// A flat list of stacks, addressed by slot index. Empty slots are represented
// by empty stacks rather than nulls so the grid and the save file both stay a
// fixed length and the UI never has to reason about holes.
//
// Knows nothing about the UI: InventoryUI listens to Changed and redraws.
// That split is what lets the bag be filled from a script, a cutscene or a
// loaded save without any of them touching a widget.
public class Inventory : MonoBehaviour, ISaveable
{
    public static Inventory Instance { get; private set; }

    [Tooltip("How many slots the bag has. Changing this after a save exists " +
             "keeps the items; the extra slots are simply appended.")]
    [SerializeField] int slotCount = 20;

    [Tooltip("Items granted on a new game, as id:count. Test content until " +
             "there is a way to pick things up in the world.")]
    [SerializeField] string[] startingItems =
    {
        "ration:3", "water:2", "bandage:2", "wrench:1", "battery:1",
        "scrap:7", "cloth:4", "bunker_key:1", "mother_note:1",
    };

    // Raised after any change to the contents. The UI redraws from this rather
    // than polling.
    public event Action Changed;

    // Raised when a stack is added or grown, with the slot it landed in, so the
    // UI can flash exactly that slot instead of the whole grid.
    public event Action<int> SlotGained;

    InventoryData data = new InventoryData();
    bool granted;

    public int SlotCount { get { return data.slotCount; } }
    public IReadOnlyList<ItemStack> Stacks { get { return data.stacks; } }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        EnsureSize();
    }

    void OnEnable() { SaveRegistry.Register(this); }
    void OnDisable() { SaveRegistry.Unregister(this); }

    void Start()
    {
        // Only if a load did not already fill the bag - RestoreState runs
        // before Start when a save is being loaded.
        if (!granted && IsEmpty()) GrantStartingItems();
    }

    // ------------------------------------------------------------------- API
    public ItemStack At(int slot)
    {
        EnsureSize();
        return slot >= 0 && slot < data.stacks.Count ? data.stacks[slot] : null;
    }

    public bool IsEmpty()
    {
        foreach (var s in data.stacks)
            if (s != null && !s.IsEmpty) return false;
        return true;
    }

    public int CountOf(string id)
    {
        int total = 0;
        foreach (var s in data.stacks)
            if (s != null && s.id == id) total += s.count;
        return total;
    }

    // Returns how many could not fit. Fills partial stacks before opening a
    // new slot, which is what makes picking up two rations top up the stack
    // already in the bag instead of starting a second one.
    public int Add(string id, int amount = 1)
    {
        var def = ItemCatalog.Get(id);
        if (def == null)
        {
            Debug.LogError("[Inventory] unknown item id '" + id + "'");
            return amount;
        }
        if (amount <= 0) return 0;

        EnsureSize();
        int remaining = amount;
        int lastTouched = -1;

        if (def.Stacks)
        {
            for (int i = 0; i < data.stacks.Count && remaining > 0; i++)
            {
                var s = data.stacks[i];
                if (s.id != id || s.count >= def.maxStack) continue;

                int room = def.maxStack - s.count;
                int moved = Mathf.Min(room, remaining);
                s.count += moved;
                remaining -= moved;
                lastTouched = i;
            }
        }

        for (int i = 0; i < data.stacks.Count && remaining > 0; i++)
        {
            var s = data.stacks[i];
            if (!s.IsEmpty) continue;

            int moved = Mathf.Min(def.maxStack, remaining);
            s.id = id;
            s.count = moved;
            remaining -= moved;
            lastTouched = i;
        }

        if (lastTouched >= 0)
        {
            Changed?.Invoke();
            SlotGained?.Invoke(lastTouched);
        }
        return remaining;
    }

    public bool RemoveAt(int slot, int amount = 1)
    {
        var s = At(slot);
        if (s == null || s.IsEmpty || amount <= 0) return false;

        s.count -= amount;
        if (s.count <= 0) { s.id = ""; s.count = 0; }

        Changed?.Invoke();
        return true;
    }

    // Uses one from the slot. Returns false when the item has no use verb, so
    // the caller can leave the button hidden rather than showing a dead one.
    public bool Use(int slot)
    {
        var s = At(slot);
        var def = s != null ? s.Definition : null;
        if (def == null || !def.CanUse) return false;

        // Nothing consumes anything yet - there are no stats to feed. The hook
        // is here so an effect can be added per item without the UI changing.
        Debug.Log("[Inventory] used " + def.displayName);

        if (def.consumeOnUse) RemoveAt(slot);
        else Changed?.Invoke();

        return true;
    }

    public bool Drop(int slot)
    {
        var s = At(slot);
        var def = s != null ? s.Definition : null;
        if (def == null || !def.canDrop) return false;

        // The whole stack goes, not one of it: the confirm the player just
        // answered said the item's name, not "one of them".
        s.id = "";
        s.count = 0;

        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        foreach (var s in data.stacks) { s.id = ""; s.count = 0; }
        Changed?.Invoke();
    }

    public void GrantStartingItems()
    {
        granted = true;
        if (startingItems == null) return;

        foreach (var entry in startingItems)
        {
            if (string.IsNullOrEmpty(entry)) continue;

            var parts = entry.Split(':');
            string id = parts[0].Trim();
            int count = 1;
            if (parts.Length > 1) int.TryParse(parts[1], out count);

            Add(id, Mathf.Max(1, count));
        }
    }

    // ------------------------------------------------------------------ save
    public string SaveId { get { return "inventory"; } }

    public string CaptureState()
    {
        return JsonUtility.ToJson(data);
    }

    public void RestoreState(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        JsonUtility.FromJsonOverwrite(json, data);
        granted = true;          // a loaded bag is the bag, even an empty one

        // A save written before an item was removed from the catalog would
        // otherwise leave a slot the UI cannot draw.
        foreach (var s in data.stacks)
            if (!s.IsEmpty && !ItemCatalog.Exists(s.id)) { s.id = ""; s.count = 0; }

        EnsureSize();
        Changed?.Invoke();
    }

    // Keeps the list exactly slotCount long, whichever direction it changed in.
    void EnsureSize()
    {
        if (data.stacks == null) data.stacks = new List<ItemStack>();

        data.slotCount = Mathf.Max(1, slotCount);

        while (data.stacks.Count < data.slotCount) data.stacks.Add(new ItemStack());
        while (data.stacks.Count > data.slotCount) data.stacks.RemoveAt(data.stacks.Count - 1);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
