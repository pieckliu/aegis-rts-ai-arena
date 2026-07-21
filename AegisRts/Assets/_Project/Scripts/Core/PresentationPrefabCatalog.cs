using UnityEngine;

public sealed class PresentationPrefabCatalog : ScriptableObject
{
    public GameObject PlayerBasePrefab;
    public GameObject EnemyBasePrefab;
    public GameObject FactoryPrefab;
    public GameObject PlayerInfantryPrefab;
    public GameObject EnemyInfantryPrefab;
    public GameObject CircleOverlayPrefab;
    public GameObject GridLinePrefab;
}

internal enum PresentationEntityKind
{
    PlayerBase,
    EnemyBase,
    Factory,
    PlayerInfantry,
    EnemyInfantry
}
