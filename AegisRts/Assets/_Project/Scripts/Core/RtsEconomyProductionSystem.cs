using System;
using System.Collections.Generic;

internal sealed class RtsEconomyProductionSystem
{
    private readonly RtsGameConfig config;
    private float incomeTimer;

    public int Resources { get; private set; }

    public RtsEconomyProductionSystem(RtsGameConfig gameConfig)
    {
        config = gameConfig;
        Reset();
    }

    public void Reset()
    {
        Resources = config.StartingResources;
        incomeTimer = config.PassiveResourceInterval;
    }

    public void TickIncome(float deltaTime)
    {
        incomeTimer -= deltaTime;

        if (incomeTimer > 0f)
        {
            return;
        }

        incomeTimer = config.PassiveResourceInterval;
        Resources = ArenaGameRules.ApplyIncome(Resources, config.PassiveResourceIncome);
    }

    public bool CanAfford(int cost)
    {
        return ArenaGameRules.CanAfford(Resources, cost);
    }

    public bool TrySpend(int cost)
    {
        if (!CanAfford(cost))
        {
            return false;
        }

        Resources = ArenaGameRules.Spend(Resources, cost);
        return true;
    }

    public bool TryQueueInfantry(BuildingData factory)
    {
        return TryQueueUnit(factory, UnitType.Infantry);
    }

    public bool TryQueueArtillery(BuildingData factory)
    {
        return TryQueueUnit(factory, UnitType.Artillery);
    }

    public bool TryQueueUnit(BuildingData factory, UnitType unitType)
    {
        if (factory == null || factory.Type != BuildingType.Factory)
        {
            return false;
        }

        int cost = GetUnitCost(unitType);

        if (!ArenaGameRules.CanQueue(
                factory.ProductionQueueCount,
                config.MaxFactoryQueueSize,
                Resources,
                cost
            ))
        {
            return false;
        }

        TrySpend(cost);
        factory.ProductionQueue.Add(unitType);

        if (factory.ProductionQueueCount == 1)
        {
            factory.ProductionTimer = GetTrainingTime(unitType);
        }

        return true;
    }

    public void TickProduction(
        float deltaTime,
        IList<BuildingData> buildings,
        Func<BuildingData, bool> trySpawnInfantry
    )
    {
        TickProduction(
            deltaTime,
            buildings,
            (factory, unitType) =>
                unitType == UnitType.Infantry && trySpawnInfantry(factory)
        );
    }

    public void TickProduction(
        float deltaTime,
        IList<BuildingData> buildings,
        Func<BuildingData, UnitType, bool> trySpawnUnit
    )
    {
        foreach (BuildingData factory in buildings)
        {
            if (factory.Team != Team.Player ||
                factory.Type != BuildingType.Factory ||
                factory.ProductionQueueCount <= 0)
            {
                continue;
            }

            factory.ProductionTimer -= deltaTime;

            if (factory.ProductionTimer > 0f)
            {
                continue;
            }

            UnitType completedType = factory.CurrentProductionType;

            if (!trySpawnUnit(factory, completedType))
            {
                factory.ProductionTimer = 0.5f;
                continue;
            }

            factory.ProductionQueue.RemoveAt(0);
            factory.ProductionTimer = factory.ProductionQueueCount > 0
                ? GetTrainingTime(factory.CurrentProductionType)
                : 0f;
        }
    }

    public int GetUnitCost(UnitType unitType)
    {
        return unitType == UnitType.Artillery
            ? config.ArtilleryCost
            : config.InfantryCost;
    }

    public float GetTrainingTime(UnitType unitType)
    {
        return unitType == UnitType.Artillery
            ? config.ArtilleryTrainingTime
            : config.InfantryTrainingTime;
    }
}
