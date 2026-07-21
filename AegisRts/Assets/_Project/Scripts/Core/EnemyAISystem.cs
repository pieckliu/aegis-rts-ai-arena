using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class EnemyAISystem
{
    private readonly RtsGameConfig config;
    private readonly GridMapService gridMap;
    private readonly IList<BuildingData> buildings;
    private readonly Func<Vector2Int, UnitData> createInfantry;
    private float spawnTimer;

    public EnemyAISystem(
        RtsGameConfig gameConfig,
        GridMapService mapService,
        IList<BuildingData> buildingList,
        Func<Vector2Int, UnitData> infantryFactory
    )
    {
        config = gameConfig;
        gridMap = mapService;
        buildings = buildingList;
        createInfantry = infantryFactory;
        Reset();
    }

    public void Reset()
    {
        spawnTimer = config.EnemySpawnInterval;
    }

    public bool Tick(
        float deltaTime,
        BuildingData playerBase,
        BuildingData enemyBase
    )
    {
        if (playerBase == null ||
            enemyBase == null ||
            !buildings.Contains(playerBase) ||
            !buildings.Contains(enemyBase))
        {
            return false;
        }

        spawnTimer -= deltaTime;

        if (spawnTimer > 0f)
        {
            return false;
        }

        spawnTimer = config.EnemySpawnInterval;

        if (!gridMap.TryFindOpenCellNear(enemyBase.Cell, out Vector2Int spawnCell))
        {
            Debug.LogWarning("Enemy AI cannot spawn infantry: no valid spawn cell.");
            return false;
        }

        UnitData infantry = createInfantry?.Invoke(spawnCell);

        if (infantry == null)
        {
            return false;
        }

        infantry.AttackUnitTarget = null;
        infantry.AttackTarget = playerBase;
        Debug.Log($"Enemy infantry spawned at cell {spawnCell} and is attacking player base.");
        return true;
    }
}
