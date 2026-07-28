using UnityEngine;
using System.Collections.Generic;

internal sealed class BuildingPlacementSystem
{
    private readonly RtsGameConfig config;
    private readonly RtsEconomyProductionSystem economy;
    private readonly GridMapService gridMap;

    public BuildingPlacementSystem(
        RtsGameConfig gameConfig,
        RtsEconomyProductionSystem economySystem,
        GridMapService mapService
    )
    {
        config = gameConfig;
        economy = economySystem;
        gridMap = mapService;
    }

    public int GetCost(BuildingType buildingType)
    {
        switch (buildingType)
        {
            case BuildingType.Factory:
                return config.FactoryCost;
            case BuildingType.Garrison:
                return config.GarrisonCost;
            default:
                return 0;
        }
    }

    public bool CanAfford(BuildingType buildingType)
    {
        return economy.CanAfford(GetCost(buildingType));
    }

    public bool CanPlace(
        BuildingType buildingType,
        Vector2 playerBasePosition,
        Vector2 worldPosition,
        Vector2Int cell
    )
    {
        List<Vector2Int> footprint = GetFootprint(buildingType, cell);

        bool isConstructible = buildingType == BuildingType.Factory ||
            buildingType == BuildingType.Garrison;

        return isConstructible &&
            gridMap.IsCellInside(cell) &&
            gridMap.IsWorldInside(worldPosition) &&
            CanAfford(buildingType) &&
            Vector2.Distance(playerBasePosition, worldPosition) <= config.BuildRadius &&
            gridMap.CanOccupy(footprint);
    }

    public bool TryReserve(
        BuildingType buildingType,
        Vector2 playerBasePosition,
        Vector2 worldPosition,
        Vector2Int cell
    )
    {
        if (!CanPlace(buildingType, playerBasePosition, worldPosition, cell))
        {
            return false;
        }

        List<Vector2Int> footprint = GetFootprint(buildingType, cell);

        if (!gridMap.TryOccupy(footprint))
        {
            return false;
        }

        if (economy.TrySpend(GetCost(buildingType)))
        {
            return true;
        }

        gridMap.Release(footprint);
        return false;
    }

    public List<Vector2Int> GetFootprint(
        BuildingType buildingType,
        Vector2Int centerCell
    )
    {
        int footprintRadius;

        switch (buildingType)
        {
            case BuildingType.Factory:
                footprintRadius = config.FactoryFootprintRadius;
                break;
            case BuildingType.Garrison:
                footprintRadius = config.GarrisonFootprintRadius;
                break;
            default:
                footprintRadius = config.BaseFootprintRadius;
                break;
        }

        return gridMap.GetSquareFootprint(centerCell, footprintRadius);
    }
}
