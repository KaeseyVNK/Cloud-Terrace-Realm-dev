using UnityEngine;

public class UnfreezeInteraction : MonoBehaviour
{
    public float interactRadius = 1.5f;
    public KeyCode interactKey = KeyCode.F;

    void Update()
    {
        if (!Input.GetKeyDown(interactKey)) return;

        // Tìm building bị freeze gần nhất
        var buildings = FindObjectsByType<FreezableBuilding>(FindObjectsInactive.Exclude);
        foreach (var b in buildings)
        {
            if (!b.IsFrozen) continue;
            float dist = Vector3.Distance(transform.position, b.GetPosition());
            if (dist <= interactRadius)
            {
                b.Unfreeze();
                return;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}