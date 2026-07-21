using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class RuntimeSystemsTests
{
    [Test]
    public void EnemyAI_SpawnsOnIntervalAndTargetsPlayerBase()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        GridMapService gridMap = new GridMapService(config.MapSize, config.CellSize);
        BuildingData playerBase = new BuildingData(
            "Player Base",
            BuildingType.Base,
            null,
            gridMap.CellToWorld(new Vector2Int(28, 28)),
            new Vector2Int(28, 28),
            0.5f,
            string.Empty,
            Team.Player,
            500
        );
        BuildingData enemyBase = new BuildingData(
            "Enemy Base",
            BuildingType.Base,
            null,
            gridMap.CellToWorld(new Vector2Int(2, 2)),
            new Vector2Int(2, 2),
            0.5f,
            string.Empty,
            Team.Enemy,
            500
        );
        List<BuildingData> buildings = new List<BuildingData> { playerBase, enemyBase };
        List<UnitData> spawnedUnits = new List<UnitData>();
        EnemyAISystem enemyAI = new EnemyAISystem(
            config,
            gridMap,
            buildings,
            cell =>
            {
                UnitData unit = CreateUnitAt(gridMap, cell, "Enemy", Team.Enemy);
                spawnedUnits.Add(unit);
                return unit;
            }
        );

        Assert.IsFalse(enemyAI.Tick(config.EnemySpawnInterval * 0.5f, playerBase, enemyBase));
        Assert.IsTrue(enemyAI.Tick(config.EnemySpawnInterval * 0.5f, playerBase, enemyBase));
        Assert.AreEqual(1, spawnedUnits.Count);
        Assert.AreSame(playerBase, spawnedUnits[0].AttackTarget);
        Assert.IsNull(spawnedUnits[0].AttackUnitTarget);

        Object.DestroyImmediate(config);
    }

    [Test]
    public void PresentationFactory_CreatesLabeledCircleAndUpdatesColor()
    {
        EntityPresentationFactory presentation = new EntityPresentationFactory(16);
        GameObject root = new GameObject("PresentationRoot");
        PresentationPrefabCatalog catalog = Resources.Load<PresentationPrefabCatalog>(
            "PresentationPrefabCatalog"
        );

        try
        {
            Assert.IsTrue(presentation.UsesPrefabCatalog);
            Assert.IsNotNull(catalog);
            Assert.IsNotNull(catalog.PlayerBasePrefab);
            Assert.IsNotNull(catalog.EnemyBasePrefab);
            Assert.IsNotNull(catalog.FactoryPrefab);
            Assert.IsNotNull(catalog.PlayerInfantryPrefab);
            Assert.IsNotNull(catalog.EnemyInfantryPrefab);
            Assert.IsNotNull(catalog.CircleOverlayPrefab);
            Assert.IsNotNull(catalog.GridLinePrefab);

            GameObject circle = presentation.CreateLabeledCircle(
                PresentationEntityKind.EnemyBase,
                "TestEntity",
                new Vector2(2f, 3f),
                0.5f,
                Color.red,
                20,
                root.transform,
                "AI",
                Color.white
            );
            SpriteRenderer renderer = circle.GetComponent<SpriteRenderer>();
            TextMesh label = circle.GetComponentInChildren<TextMesh>();

            Assert.IsNotNull(renderer);
            Assert.IsNotNull(renderer.sprite);
            Assert.IsNotNull(label);
            Assert.AreEqual("AI", label.text);
            Assert.AreEqual(root.transform, circle.transform.parent);

            presentation.SetCircleColor(circle, Color.green);
            Assert.AreEqual(Color.green, renderer.color);
        }
        finally
        {
            Object.DestroyImmediate(root);
            presentation.Dispose();
        }
    }

    [Test]
    public void GridMap_ConvertsCoordinatesAndFindsOpenSpawnCell()
    {
        GridMapService gridMap = new GridMapService(10, 1f);
        Vector2Int cell = new Vector2Int(3, 7);
        Vector2 worldPosition = gridMap.CellToWorld(cell);

        Assert.AreEqual(cell, gridMap.WorldToCell(worldPosition));
        Assert.IsTrue(gridMap.IsCellInside(cell));
        Assert.IsFalse(gridMap.IsCellInside(new Vector2Int(10, 7)));
        Assert.IsTrue(gridMap.TryOccupy(new Vector2Int(5, 4)));
        Assert.IsTrue(gridMap.TryFindOpenCellNear(new Vector2Int(5, 5), out Vector2Int openCell));
        Assert.AreEqual(new Vector2Int(4, 5), openCell);
    }

    [Test]
    public void Placement_ReservesCellAndSpendsResourcesAtomically()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        GridMapService gridMap = new GridMapService(config.MapSize, config.CellSize);
        RtsEconomyProductionSystem economy = new RtsEconomyProductionSystem(config);
        BuildingPlacementSystem placement = new BuildingPlacementSystem(config, economy, gridMap);
        Vector2Int cell = new Vector2Int(20, 20);
        Vector2 worldPosition = gridMap.CellToWorld(cell);
        Vector2 basePosition = worldPosition;

        Assert.IsTrue(placement.TryReserve(
            BuildingType.Factory,
            basePosition,
            worldPosition,
            cell
        ));
        Assert.IsTrue(gridMap.IsOccupied(cell));
        Assert.AreEqual(config.StartingResources - config.FactoryCost, economy.Resources);
        Assert.IsFalse(placement.TryReserve(
            BuildingType.Factory,
            basePosition,
            worldPosition,
            cell
        ));
        Assert.AreEqual(config.StartingResources - config.FactoryCost, economy.Resources);

        Object.DestroyImmediate(config);
    }

    [Test]
    public void Movement_AssignsDistinctFormationCellsAndReachesTargets()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        config.MapSize = 12;
        config.CellSize = 1f;
        config.UnitMoveSpeed = 20f;
        GridMapService gridMap = new GridMapService(config.MapSize, config.CellSize);
        List<UnitData> units = new List<UnitData>
        {
            CreateUnitAt(gridMap, new Vector2Int(1, 1), "One"),
            CreateUnitAt(gridMap, new Vector2Int(2, 1), "Two")
        };
        UnitMovementSystem movement = new UnitMovementSystem(config, gridMap, units);

        int commanded = movement.CommandGroupMove(units, new Vector2Int(8, 8));

        Assert.AreEqual(2, commanded);
        Assert.AreNotEqual(units[0].TargetCell, units[1].TargetCell);
        Assert.IsTrue(gridMap.IsOccupied(units[0].TargetCell));
        Assert.IsTrue(gridMap.IsOccupied(units[1].TargetCell));

        for (int i = 0; i < 20; i++)
        {
            movement.Tick(1f);
        }

        Assert.IsFalse(units[0].IsMoving);
        Assert.IsFalse(units[1].IsMoving);
        Assert.AreEqual(gridMap.CellToWorld(units[0].TargetCell), units[0].Position);
        Assert.AreEqual(gridMap.CellToWorld(units[1].TargetCell), units[1].Position);

        Object.DestroyImmediate(config);
    }

    [Test]
    public void Movement_CombatPursuitUpdatesOccupiedCell()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        config.MapSize = 8;
        config.UnitMoveSpeed = 20f;
        GridMapService gridMap = new GridMapService(config.MapSize, config.CellSize);
        UnitData unit = CreateUnitAt(gridMap, new Vector2Int(1, 1), "Pursuer");
        UnitMovementSystem movement = new UnitMovementSystem(
            config,
            gridMap,
            new List<UnitData> { unit }
        );
        Vector2Int destination = new Vector2Int(3, 1);

        movement.MoveTowards(unit, gridMap.CellToWorld(destination), 1f);

        Assert.AreEqual(destination, unit.Cell);
        Assert.IsFalse(gridMap.IsOccupied(new Vector2Int(1, 1)));
        Assert.IsTrue(gridMap.IsOccupied(destination));

        Object.DestroyImmediate(config);
    }

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

    private static UnitData CreateUnitAt(
        GridMapService gridMap,
        Vector2Int cell,
        string displayName,
        Team team = Team.Player
    )
    {
        gridMap.TryOccupy(cell);
        Vector2 position = gridMap.CellToWorld(cell);
        return new UnitData(
            displayName,
            UnitType.Infantry,
            null,
            position,
            cell,
            0.4f,
            string.Empty,
            team,
            100,
            10,
            1f,
            1f
        );
    }
}
