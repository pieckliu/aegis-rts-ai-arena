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
        if (factory == null || factory.Type != BuildingType.Factory)
        {
            return false;
        }

        if (!ArenaGameRules.CanQueue(
                factory.InfantryQueue,
                config.MaxFactoryQueueSize,
                Resources,
                config.InfantryCost
            ))
        {
            return false;
        }

        TrySpend(config.InfantryCost);
        factory.InfantryQueue++;

        if (factory.InfantryQueue == 1)
        {
            factory.ProductionTimer = config.InfantryTrainingTime;
        }

        return true;
    }

    public void TickProduction(
        float deltaTime,
        IList<BuildingData> buildings,
        Func<BuildingData, bool> trySpawnInfantry
    )
    {
        foreach (BuildingData factory in buildings)
        {
            if (factory.Team != Team.Player ||
                factory.Type != BuildingType.Factory ||
                factory.InfantryQueue <= 0)
            {
                continue;
            }

            factory.ProductionTimer -= deltaTime;

            if (factory.ProductionTimer > 0f)
            {
                continue;
            }

            if (!trySpawnInfantry(factory))
            {
                factory.ProductionTimer = 0.5f;
                continue;
            }

            factory.InfantryQueue--;
            factory.ProductionTimer = factory.InfantryQueue > 0
                ? config.InfantryTrainingTime
                : 0f;
        }
    }
}
