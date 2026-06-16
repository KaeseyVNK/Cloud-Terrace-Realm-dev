using UnityEngine;

public class VisionSource : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<VisionSource> Registry = new System.Collections.Generic.List<VisionSource>();

    private void OnEnable()
    {
        Registry.Add(this);
    }

    private void OnDisable()
    {
        Registry.Remove(this);
    }

    [SerializeField] private float visionRadius = 12f;
    [SerializeField] private bool revealWhileInactive = false;

    public float VisionRadius => visionRadius;

    public bool CanReveal()
    {
        BaseCombatUnitController combatUnit = GetComponent<BaseCombatUnitController>();
        if (combatUnit != null && combatUnit.currentState == CombatState.Dead)
        {
            return false;
        }

        ConstructibleBuilding building = GetComponent<ConstructibleBuilding>();
        if (building != null && !building.IsCompleted)
        {
            return false;
        }

        if (!isActiveAndEnabled)
        {
            return revealWhileInactive;
        }

        return true;
    }
}
