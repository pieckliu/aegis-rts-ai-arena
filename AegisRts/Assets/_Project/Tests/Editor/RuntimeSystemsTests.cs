using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class RuntimeSystemsTests
{
    [Test]
    public void Economy_ResetIncomeAndSpending_AreDeterministic()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        RtsEconomyProductionSystem economy = new RtsEconomyProductionSystem(config);

        Assert.AreEqual(config.StartingResources, economy.Resources);
        Assert.IsTrue(economy.TrySpend(config.FactoryCost));
        Assert.AreEqual(config.StartingResources - config.FactoryCost, economy.Resources);

        economy.TickIncome(config.PassiveResourceInterval);
        Assert.AreEqual(
            config.StartingResources - config.FactoryCost + config.PassiveResourceIncome,
            economy.Resources
        );

        Object.DestroyImmediate(config);
    }

    [Test]
    public void Economy_QueuesInfantryAndAdvancesProduction()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        RtsEconomyProductionSystem economy = new RtsEconomyProductionSystem(config);
        BuildingData factory = new BuildingData(
            "Factory",
            BuildingType.Factory,
            null,
            Vector2.zero,
            Vector2Int.zero,
            0.5f,
            string.Empty,
            Team.Player,
            100
        );
        List<BuildingData> buildings = new List<BuildingData> { factory };

        Assert.IsTrue(economy.TryQueueInfantry(factory));
        economy.TickProduction(config.InfantryTrainingTime, buildings, _ => true);

        Assert.AreEqual(0, factory.InfantryQueue);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void ArenaObservation_UsesSystemState()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        RtsEconomyProductionSystem economy = new RtsEconomyProductionSystem(config);
        List<BuildingData> buildings = new List<BuildingData>();
        List<UnitData> units = new List<UnitData>();
        ArenaOrchestrator arena = new ArenaOrchestrator(
            config,
            economy,
            buildings,
            units,
            () => 12.5f,
            () => true,
            () => false,
            () => false,
            (_, _) => { },
            (_, _) => { },
            (_, _) => { },
            _ => false,
            _ => false
        );

        ArenaObservation observation = arena.GetObservation();

        Assert.AreEqual(12.5f, observation.MatchTime);
        Assert.AreEqual(config.StartingResources, observation.PlayerResources);
        Assert.AreEqual("Running", observation.Result);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void CombatAndLifecycle_RemoveDefeatedUnit()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        List<BuildingData> buildings = new List<BuildingData>();
        List<UnitData> units = new List<UnitData>();
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        UnitData attacker = new UnitData(
            "Player",
            UnitType.Infantry,
            null,
            Vector2.zero,
            Vector2Int.zero,
            0.4f,
            string.Empty,
            Team.Player,
            100,
            100,
            2f,
            1f
        );
        UnitData target = new UnitData(
            "Enemy",
            UnitType.Infantry,
            null,
            Vector2.right,
            Vector2Int.right,
            0.4f,
            string.Empty,
            Team.Enemy,
            50,
            1,
            2f,
            1f
        );
        units.Add(attacker);
        units.Add(target);
        occupied.Add(attacker.Cell);
        occupied.Add(target.Cell);
        RtsEntityLifecycle lifecycle = new RtsEntityLifecycle(buildings, units, occupied, null, null);
        RtsCombatSystem combat = new RtsCombatSystem(config, buildings, units, (_, _) => { }, lifecycle);

        combat.Tick(0.1f);

        Assert.AreEqual(1, units.Count);
        Assert.IsFalse(units.Contains(target));
        Object.DestroyImmediate(config);
    }
}
