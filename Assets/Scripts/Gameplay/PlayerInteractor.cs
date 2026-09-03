using UnityEngine;
using UnityEngine.InputSystem;

// Put this on Asher. Detects the nearest NPC in range, shows a "Press E"
// hint, and starts a dialogue when E is pressed.
public class PlayerInteractor : MonoBehaviour
{
    private NPCInteractable nearest;

    void Update()
    {
        if (DialogueManager.IsActive)
            return;

        // With the bag open, E belongs to nothing and the floating "Press E"
        // prompt would sit on top of the panel.
        if (InventoryUI.IsOpen)
        {
            if (DialogueManager.Instance != null) DialogueManager.Instance.ShowHint(null);
            return;
        }

        nearest = FindNearest();
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.ShowHint(nearest);

        var kb = Keyboard.current;
        if (nearest != null && kb != null && kb.eKey.wasPressedThisFrame
            && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.Begin(nearest, transform);
        }
    }

    NPCInteractable FindNearest()
    {
        NPCInteractable best = null;
        float bestDist = float.MaxValue;
        var all = Object.FindObjectsByType<NPCInteractable>(FindObjectsInactive.Exclude);
        foreach (var npc in all)
        {
            float d = Vector3.Distance(transform.position, npc.transform.position);
            if (d <= npc.interactRange && d < bestDist)
            {
                bestDist = d;
                best = npc;
            }
        }
        return best;
    }
}
