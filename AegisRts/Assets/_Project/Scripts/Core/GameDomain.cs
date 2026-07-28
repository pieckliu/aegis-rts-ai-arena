using System.Collections.Generic;
using UnityEngine;

internal enum GameState
{
    MainMenu,
    Playing
}

internal enum BuildingType
{
    None,
    Base,
    Factory,
    Garrison
}

internal enum UnitType
{
    Infantry,
    Artillery
}

internal enum Team
{
    Player,
    Enemy
}

internal sealed class BuildingData
{
    public int Id;
    public string DisplayName;
    public BuildingType Type;
    public GameObject GameObject;
    public Vector2 Position;
    public Vector2Int Cell;
    public float Radius;
    public string Description;
    public Team Team;
    public int MaxHitPoints;
    public int HitPoints;
    public readonly List<Vector2Int> OccupiedCells = new List<Vector2Int>();
    public readonly List<UnitType> ProductionQueue = new List<UnitType>();
    public float ProductionTimer;
    public int InfantryQueue => CountQueued(UnitType.Infantry);
    public int ArtilleryQueue => CountQueued(UnitType.Artillery);
    public int ProductionQueueCount => ProductionQueue.Count;
    public readonly List<UnitData> GarrisonedUnits = new List<UnitData>();
    public int GarrisonCapacity;
    public float GarrisonDamageMultiplier = 1f;
    public UnitType CurrentProductionType => ProductionQueue.Count > 0
        ? ProductionQueue[0]
        : UnitType.Infantry;

    public BuildingData(
        string displayName,
        BuildingType type,
        GameObject gameObject,
        Vector2 position,
        Vector2Int cell,
        float radius,
        string description,
        Team team,
        int maxHitPoints,
        IEnumerable<Vector2Int> occupiedCells = null
    )
    {
        DisplayName = displayName;
        Type = type;
        GameObject = gameObject;
        Position = position;
        Cell = cell;
        Radius = radius;
        Description = description;
        Team = team;
        MaxHitPoints = maxHitPoints;
        HitPoints = maxHitPoints;

        if (occupiedCells != null)
        {
            OccupiedCells.AddRange(occupiedCells);
        }

        if (OccupiedCells.Count == 0)
        {
            OccupiedCells.Add(cell);
        }
    }

    private int CountQueued(UnitType unitType)
    {
        int count = 0;

        foreach (UnitType queuedType in ProductionQueue)
        {
            if (queuedType == unitType)
            {
                count++;
            }
        }

        return count;
    }
}

internal sealed class UnitData
{
    public int Id;
    public string DisplayName;
    public UnitType Type;
    public GameObject GameObject;
    public Vector2 Position;
    public Vector2Int Cell;
    public float Radius;
    public string Description;
    public Team Team;
    public int MaxHitPoints;
    public int HitPoints;
    public bool IsMoving;
    public Vector2 TargetPosition;
    public Vector2Int TargetCell;
    public readonly List<Vector2> Waypoints = new List<Vector2>();
    public int AttackDamage;
    public float AttackRange;
    public float AttackCooldown;
    public float AttackTimer;
    public float MoveSpeed;
    public float BuildingDamageMultiplier;
    public bool IsDeployed;
    public BuildingData GarrisonTarget;
    public BuildingData GarrisonBuilding;
    public BuildingData AttackTarget;
    public UnitData AttackUnitTarget;

    public UnitData(
        string displayName,
        UnitType type,
        GameObject gameObject,
        Vector2 position,
        Vector2Int cell,
        float radius,
        string description,
        Team team,
        int maxHitPoints,
        int attackDamage,
        float attackRange,
        float attackCooldown,
        float moveSpeed = 0f,
        float buildingDamageMultiplier = 1f
    )
    {
        DisplayName = displayName;
        Type = type;
        GameObject = gameObject;
        Position = position;
        Cell = cell;
        Radius = radius;
        Description = description;
        Team = team;
        MaxHitPoints = maxHitPoints;
        HitPoints = maxHitPoints;
        TargetPosition = position;
        TargetCell = cell;
        AttackDamage = attackDamage;
        AttackRange = attackRange;
        AttackCooldown = attackCooldown;
        MoveSpeed = moveSpeed;
        BuildingDamageMultiplier = Mathf.Max(0f, buildingDamageMultiplier);
    }
}
