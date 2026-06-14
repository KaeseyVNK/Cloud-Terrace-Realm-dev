using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyRoster", menuName = "Enemy/Roster Data")]
public class EnemyRosterData : ScriptableObject
{
    [Tooltip("Enemies available to the automatic wave progression. unlockDay controls when an enemy enters the pool; weight controls how often it is selected.")]
    public List<EnemyRosterEntry> roster = new List<EnemyRosterEntry>();
}
