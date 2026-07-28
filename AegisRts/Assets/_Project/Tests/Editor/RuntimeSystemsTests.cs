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
    public void PresentationFactory_CreatesSymbolicCircleAndUpdatesColor()
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
            RtsEntityViewAnimator animator = circle.GetComponent<RtsEntityViewAnimator>();

            Assert.IsNotNull(renderer);
            Assert.IsNotNull(renderer.sprite);
            Assert.IsNotNull(label);
            Assert.AreEqual("AI", label.text);
            Assert.IsNull(animator);
            Assert.AreEqual(Color.red, renderer.color);
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
    public void Visibility_RevealsNearbyEnemiesAndKeepsExploredCells()
    {
        GridMapService gridMap = new GridMapService(10, 1f);
        GameObject root = new GameObject("VisibilityRoot");
        GameObject playerObject = new GameObject("PlayerBase");
        GameObject enemyObject = new GameObject("Enemy");
        Vector2 playerPosition = gridMap.CellToWorld(new Vector2Int(1, 1));
        Vector2 enemyPosition = gridMap.CellToWorld(new Vector2Int(2, 1));
        BuildingData playerBase = new BuildingData(
            "Player Base",
            BuildingType.Base,
            playerObject,
            playerPosition,
            new Vector2Int(1, 1),
            0.5f,
            string.Empty,
            Team.Player,
            100
        );
        UnitData enemy = new UnitData(
            "Enemy",
            UnitType.Infantry,
            enemyObject,
            enemyPosition,
            new Vector2Int(2, 1),
            0.4f,
            string.Empty,
            Team.Enemy,
            100,
            10,
            1f,
            1f
        );
        List<BuildingData> buildings = new List<BuildingData> { playerBase };
        List<UnitData> units = new List<UnitData> { enemy };
        RtsVisibilitySystem visibility = new RtsVisibilitySystem(
            gridMap,
            buildings,
            units,
            2.5f,
            2f,
            8f,
            root.transform
        );

        try
        {
            visibility.Tick(0f);

            Assert.IsTrue(visibility.IsVisible(enemyPosition));
            Assert.IsTrue(visibility.IsExplored(enemyPosition));
            Assert.IsTrue(enemyObject.activeSelf);
            Assert.IsTrue(visibility.TryGetLastKnownContact(
                enemy,
                out Vector2 observedPosition,
                out float initialFreshness
            ));
            Assert.AreEqual(enemyPosition, observedPosition);
            Assert.AreEqual(1f, initialFreshness);

            enemy.Position = gridMap.CellToWorld(new Vector2Int(8, 8));
            visibility.Tick(2f);

            Assert.IsFalse(visibility.IsVisible(enemy.Position));
            Assert.IsTrue(visibility.IsExplored(enemyPosition));
            Assert.IsFalse(enemyObject.activeSelf);
            Assert.IsTrue(visibility.TryGetLastKnownContact(
                enemy,
                out Vector2 lastKnownPosition,
                out float fadingFreshness
            ));
            Assert.AreEqual(enemyPosition, lastKnownPosition);
            Assert.AreEqual(0.75f, fadingFreshness, 0.001f);

            enemy.Position = gridMap.CellToWorld(new Vector2Int(9, 9));
            visibility.Tick(6.1f);

            Assert.IsFalse(
                visibility.TryGetLastKnownContact(enemy, out _, out _),
                "Hidden movement must not update the snapshot, and stale unit intel should expire."
            );
        }
        finally
        {
            visibility.Destroy();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(enemyObject);
        }
    }

    [Test]
    public void WorldFeedback_PlaysAndExpiresCombatEffects()
    {
        EntityPresentationFactory presentation = new EntityPresentationFactory(16);
        GameObject root = new GameObject("FeedbackRoot");
        GameObject target = presentation.CreateCircle(
            "Target",
            Vector2.right,
            0.4f,
            Color.red,
            20,
            root.transform
        );
        RtsWorldFeedbackSystem feedback = new RtsWorldFeedbackSystem(
            presentation,
            root.transform
        );

        try
        {
            feedback.PlayCombatFeedback(new CombatFeedbackEvent(
                Vector2.zero,
                Vector2.right,
                null,
                target,
                Team.Player,
                20,
                true
            ));

            Assert.AreEqual(3, feedback.ActiveEffectCount);
            Assert.AreEqual(new Color(1f, 0.3f, 0.3f, 1f), target.GetComponent<SpriteRenderer>().color);

            feedback.Tick(1f);

            Assert.AreEqual(0, feedback.ActiveEffectCount);
            Assert.AreEqual(Color.red, target.GetComponent<SpriteRenderer>().color);
        }
        finally
        {
            feedback.Clear();
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
    public void GridMap_FindsSpawnOutsideMultiCellBuildingFootprint()
    {
        GridMapService gridMap = new GridMapService(12, 1f);
        Vector2Int center = new Vector2Int(6, 6);
        List<Vector2Int> footprint = gridMap.GetSquareFootprint(center, 1);

        Assert.IsTrue(gridMap.TryOccupy(footprint));
        Assert.IsTrue(gridMap.TryFindOpenCellNear(center, out Vector2Int openCell));
        Assert.IsFalse(footprint.Contains(openCell));
        Assert.AreEqual(2, Mathf.Max(
            Mathf.Abs(openCell.x - center.x),
            Mathf.Abs(openCell.y - center.y)
        ));
    }

    [Test]
    public void Placement_ReservesFullBuildingFootprintAndSpendsResourcesAtomically()
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
        List<Vector2Int> footprint = placement.GetFootprint(
            BuildingType.Factory,
            cell
        );
        Assert.AreEqual(9, footprint.Count);
        Assert.IsTrue(footprint.TrueForAll(gridMap.IsOccupied));
        Assert.AreEqual(config.StartingResources - config.FactoryCost, economy.Resources);
        Assert.IsFalse(placement.TryReserve(
            BuildingType.Factory,
            basePosition,
            worldPosition,
            cell + Vector2Int.right
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
    public void Movement_UnblockedCommandUsesExactWorldPointAndSingleStraightWaypoint()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        config.MapSize = 12;
        config.CellSize = 1f;
        config.UnitMoveSpeed = 2f;
        GridMapService gridMap = new GridMapService(config.MapSize, config.CellSize);
        UnitData unit = CreateUnitAt(gridMap, new Vector2Int(1, 1), "Direct");
        UnitMovementSystem movement = new UnitMovementSystem(
            config,
            gridMap,
            new List<UnitData> { unit }
        );
        Vector2 startPosition = unit.Position;
        Vector2Int targetCell = new Vector2Int(8, 7);
        Vector2 exactTarget = gridMap.CellToWorld(targetCell) + new Vector2(0.3f, -0.2f);

        int commanded = movement.CommandGroupMove(
            new List<UnitData> { unit },
            targetCell,
            exactTarget
        );

        Assert.AreEqual(1, commanded);
        Assert.AreEqual(exactTarget, unit.TargetPosition);
        Assert.AreEqual(1, unit.Waypoints.Count);
        Assert.AreEqual(exactTarget, unit.Waypoints[0]);

        movement.Tick(0.5f);

        Assert.AreEqual(
            Vector2.MoveTowards(startPosition, exactTarget, config.UnitMoveSpeed * 0.5f),
            unit.Position
        );

        for (int i = 0; i < 20; i++)
        {
            movement.Tick(1f);
        }

        Assert.IsFalse(unit.IsMoving);
        Assert.AreEqual(exactTarget, unit.Position);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void MovementCommand_AllowsPlayerInfantryToRetreatFromEnemyUnit()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        config.MapSize = 12;
        config.CellSize = 1f;
        config.UnitMoveSpeed = 4f;
        config.UnitAggroRange = 10f;
        GridMapService gridMap = new GridMapService(config.MapSize, config.CellSize);
        UnitData player = CreateUnitAt(gridMap, new Vector2Int(3, 3), "Player");
        UnitData enemy = CreateUnitAt(gridMap, new Vector2Int(4, 3), "Enemy");
        enemy.Team = Team.Enemy;
        player.AttackUnitTarget = enemy;
        List<UnitData> units = new List<UnitData> { player, enemy };
        List<BuildingData> buildings = new List<BuildingData>();
        UnitMovementSystem movement = new UnitMovementSystem(config, gridMap, units);
        RtsEntityLifecycle lifecycle = new RtsEntityLifecycle(
            buildings,
            units,
            gridMap.OccupiedCells,
            null,
            null
        );
        RtsCombatSystem combat = new RtsCombatSystem(
            config,
            buildings,
            units,
            (unit, target) => movement.MoveTowards(unit, target, 0.1f),
            lifecycle
        );
        Vector2 startPosition = player.Position;

        Assert.AreEqual(1, movement.CommandGroupMove(
            new List<UnitData> { player },
            new Vector2Int(9, 9)
        ));
        Assert.IsNull(player.AttackUnitTarget);
        Assert.IsTrue(player.IsMoving);

        combat.Tick(0.1f);
        movement.Tick(0.25f);

        Assert.IsNull(
            player.AttackUnitTarget,
            "A retreating player unit must not immediately reacquire the nearby enemy."
        );
        Assert.IsTrue(player.IsMoving);
        Assert.AreNotEqual(startPosition, player.Position);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void MovementCommand_AllowsPlayerInfantryToRetreatFromEnemyBuilding()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        config.MapSize = 12;
        config.CellSize = 1f;
        config.UnitMoveSpeed = 4f;
        config.UnitAggroRange = 10f;
        GridMapService gridMap = new GridMapService(config.MapSize, config.CellSize);
        UnitData player = CreateUnitAt(gridMap, new Vector2Int(3, 3), "Player");
        BuildingData enemyBase = new BuildingData(
            "Enemy Base",
            BuildingType.Base,
            null,
            gridMap.CellToWorld(new Vector2Int(4, 3)),
            new Vector2Int(4, 3),
            1f,
            string.Empty,
            Team.Enemy,
            100
        );
        player.AttackTarget = enemyBase;
        List<UnitData> units = new List<UnitData> { player };
        List<BuildingData> buildings = new List<BuildingData> { enemyBase };
        UnitMovementSystem movement = new UnitMovementSystem(config, gridMap, units);
        RtsEntityLifecycle lifecycle = new RtsEntityLifecycle(
            buildings,
            units,
            gridMap.OccupiedCells,
            null,
            null
        );
        RtsCombatSystem combat = new RtsCombatSystem(
            config,
            buildings,
            units,
            (unit, target) => movement.MoveTowards(unit, target, 0.1f),
            lifecycle
        );
        Vector2 startPosition = player.Position;

        Assert.AreEqual(1, movement.CommandGroupMove(
            new List<UnitData> { player },
            new Vector2Int(9, 9)
        ));
        Assert.IsNull(player.AttackTarget);
        Assert.IsTrue(player.IsMoving);

        combat.Tick(0.1f);
        movement.Tick(0.25f);

        Assert.IsNull(player.AttackTarget);
        Assert.IsTrue(player.IsMoving);
        Assert.AreNotEqual(startPosition, player.Position);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void Movement_BuildingOnDirectRouteUsesGridWaypoints()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        config.MapSize = 10;
        config.CellSize = 1f;
        GridMapService gridMap = new GridMapService(config.MapSize, config.CellSize);
        UnitData unit = CreateUnitAt(gridMap, new Vector2Int(1, 1), "Pathfinder");
        Vector2Int buildingCell = new Vector2Int(3, 1);
        Assert.IsTrue(gridMap.TryOccupy(buildingCell));
        UnitMovementSystem movement = new UnitMovementSystem(
            config,
            gridMap,
            new List<UnitData> { unit }
        );
        Vector2Int targetCell = new Vector2Int(6, 1);

        int commanded = movement.CommandGroupMove(
            new List<UnitData> { unit },
            targetCell,
            gridMap.CellToWorld(targetCell)
        );

        Assert.AreEqual(1, commanded);
        Assert.Greater(
            unit.Waypoints.Count,
            1,
            "A building intersecting the direct route should activate grid-based detouring."
        );
        Assert.AreNotEqual(unit.TargetPosition, unit.Waypoints[0]);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void Movement_DetourDoesNotEnterMultiCellBuildingFootprint()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        config.MapSize = 12;
        GridMapService gridMap = new GridMapService(config.MapSize, config.CellSize);
        UnitData unit = CreateUnitAt(gridMap, new Vector2Int(1, 5), "Detour");
        List<Vector2Int> footprint = gridMap.GetSquareFootprint(
            new Vector2Int(5, 5),
            1
        );
        Assert.IsTrue(gridMap.TryOccupy(footprint));
        UnitMovementSystem movement = new UnitMovementSystem(
            config,
            gridMap,
            new List<UnitData> { unit }
        );
        Vector2Int targetCell = new Vector2Int(9, 5);

        Assert.AreEqual(1, movement.CommandGroupMove(
            new List<UnitData> { unit },
            targetCell,
            gridMap.CellToWorld(targetCell)
        ));
        Assert.Greater(unit.Waypoints.Count, 1);

        foreach (Vector2 waypoint in unit.Waypoints)
        {
            Assert.IsFalse(
                footprint.Contains(gridMap.WorldToCell(waypoint)),
                "Pathfinding must keep every waypoint outside the building footprint."
            );
        }

        Object.DestroyImmediate(config);
    }

    [Test]
    public void Movement_SeparatesOverlappingUnitVolumes()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        config.MapSize = 10;
        config.CellSize = 1f;
        GridMapService gridMap = new GridMapService(config.MapSize, config.CellSize);
        UnitData first = CreateUnitAt(gridMap, new Vector2Int(4, 4), "First");
        UnitData second = CreateUnitAt(gridMap, new Vector2Int(5, 4), "Second");
        Vector2 overlapPosition = gridMap.CellToWorld(new Vector2Int(4, 4));
        first.Position = overlapPosition;
        second.Position = overlapPosition;
        UnitMovementSystem movement = new UnitMovementSystem(
            config,
            gridMap,
            new List<UnitData> { first, second }
        );

        movement.Tick(0f);

        float minimumDistance =
            first.Radius + second.Radius + config.UnitCollisionPadding;
        Assert.GreaterOrEqual(
            Vector2.Distance(first.Position, second.Position),
            minimumDistance - 0.001f
        );
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
    public void Economy_UsesSharedOrderedQueueForInfantryAndArtillery()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        RtsEconomyProductionSystem economy = new RtsEconomyProductionSystem(config);
        BuildingData factory = new BuildingData(
            "Factory",
            BuildingType.Factory,
            null,
            Vector2.zero,
            Vector2Int.zero,
            1f,
            string.Empty,
            Team.Player,
            100
        );
        List<BuildingData> buildings = new List<BuildingData> { factory };
        List<UnitType> spawned = new List<UnitType>();

        Assert.IsTrue(economy.TryQueueArtillery(factory));
        Assert.IsTrue(economy.TryQueueInfantry(factory));
        Assert.AreEqual(2, factory.ProductionQueueCount);
        Assert.AreEqual(UnitType.Artillery, factory.CurrentProductionType);
        Assert.AreEqual(1, factory.ArtilleryQueue);
        Assert.AreEqual(1, factory.InfantryQueue);
        Assert.AreEqual(
            config.StartingResources - config.ArtilleryCost - config.InfantryCost,
            economy.Resources
        );

        economy.TickProduction(
            config.ArtilleryTrainingTime,
            buildings,
            (_, unitType) =>
            {
                spawned.Add(unitType);
                return true;
            }
        );

        Assert.AreEqual(UnitType.Artillery, spawned[0]);
        Assert.AreEqual(UnitType.Infantry, factory.CurrentProductionType);
        Assert.AreEqual(config.InfantryTrainingTime, factory.ProductionTimer);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void CommandPanel_ShowsIndependentInfantryAndArtilleryQueueCounts()
    {
        BuildingData factory = new BuildingData(
            "Factory",
            BuildingType.Factory,
            null,
            Vector2.zero,
            Vector2Int.zero,
            1f,
            string.Empty,
            Team.Player,
            100
        );
        factory.ProductionQueue.Add(UnitType.Infantry);

        Assert.AreEqual(
            "生产步兵 (1/5)",
            RtsGameUIController.GetProductionButtonText(
                factory,
                UnitType.Infantry,
                5
            )
        );
        Assert.AreEqual(
            "生产火炮 (0/5)",
            RtsGameUIController.GetProductionButtonText(
                factory,
                UnitType.Artillery,
                5
            )
        );
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
            _ => false,
            (_, _) => { },
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
        List<CombatFeedbackEvent> feedbackEvents = new List<CombatFeedbackEvent>();
        RtsEntityLifecycle lifecycle = new RtsEntityLifecycle(buildings, units, occupied, null, null);
        RtsCombatSystem combat = new RtsCombatSystem(
            config,
            buildings,
            units,
            (_, _) => { },
            lifecycle,
            feedbackEvents.Add
        );

        combat.Tick(0.1f);

        Assert.AreEqual(1, units.Count);
        Assert.IsFalse(units.Contains(target));
        Assert.AreEqual(1, feedbackEvents.Count);
        Assert.AreEqual(attacker.AttackDamage, feedbackEvents[0].Damage);
        Assert.AreEqual(Team.Player, feedbackEvents[0].SourceTeam);
        Assert.IsTrue(feedbackEvents[0].IsLethal);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void Artillery_DealsBonusDamageToMultiCellBuildingAndReleasesFootprint()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        List<BuildingData> buildings = new List<BuildingData>();
        List<UnitData> units = new List<UnitData>();
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        List<Vector2Int> footprint = new List<Vector2Int>
        {
            new Vector2Int(4, 4),
            new Vector2Int(4, 5),
            new Vector2Int(5, 4),
            new Vector2Int(5, 5)
        };
        BuildingData target = new BuildingData(
            "Target",
            BuildingType.Factory,
            null,
            new Vector2(4f, 4f),
            new Vector2Int(4, 4),
            1f,
            string.Empty,
            Team.Enemy,
            60,
            footprint
        );
        UnitData artillery = new UnitData(
            "Artillery",
            UnitType.Artillery,
            null,
            Vector2.zero,
            Vector2Int.zero,
            config.ArtilleryRadius,
            string.Empty,
            Team.Player,
            config.PlayerArtilleryHitPoints,
            config.ArtilleryAttackDamage,
            config.ArtilleryAttackRange,
            config.ArtilleryAttackCooldown,
            config.ArtilleryMoveSpeed,
            config.ArtilleryBuildingDamageMultiplier
        );
        artillery.AttackTarget = target;
        buildings.Add(target);
        units.Add(artillery);

        foreach (Vector2Int cell in footprint)
        {
            occupied.Add(cell);
        }

        RtsEntityLifecycle lifecycle = new RtsEntityLifecycle(
            buildings,
            units,
            occupied,
            null,
            null
        );
        RtsCombatSystem combat = new RtsCombatSystem(
            config,
            buildings,
            units,
            (_, _) => { },
            lifecycle
        );

        combat.Tick(0.1f);

        Assert.IsTrue(
            buildings.Contains(target),
            "Undeployed artillery must not fire."
        );

        artillery.IsDeployed = true;
        combat.Tick(0.1f);

        Assert.IsFalse(buildings.Contains(target));
        Assert.IsTrue(footprint.TrueForAll(cell => !occupied.Contains(cell)));
        Object.DestroyImmediate(config);
    }

    [Test]
    public void Movement_RejectsCommandsForDeployedArtillery()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        GridMapService gridMap = new GridMapService(10, 1f);
        Vector2Int startCell = new Vector2Int(2, 2);
        UnitData artillery = new UnitData(
            "Artillery",
            UnitType.Artillery,
            null,
            gridMap.CellToWorld(startCell),
            startCell,
            config.ArtilleryRadius,
            string.Empty,
            Team.Player,
            config.PlayerArtilleryHitPoints,
            config.ArtilleryAttackDamage,
            config.ArtilleryAttackRange,
            config.ArtilleryAttackCooldown,
            config.ArtilleryMoveSpeed,
            config.ArtilleryBuildingDamageMultiplier
        )
        {
            IsDeployed = true
        };
        gridMap.TryOccupy(startCell);
        UnitMovementSystem movement = new UnitMovementSystem(
            config,
            gridMap,
            new List<UnitData> { artillery }
        );
        Vector2 startPosition = artillery.Position;

        Assert.AreEqual(0, movement.CommandGroupMove(
            new List<UnitData> { artillery },
            new Vector2Int(7, 7)
        ));
        movement.MoveTowards(artillery, Vector2.one * 4f, 1f);
        Assert.AreEqual(startPosition, artillery.Position);
        Assert.IsFalse(artillery.IsMoving);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void Movement_StopCancelsArtilleryRouteAndReleasesDestination()
    {
        RtsGameConfig config = ScriptableObject.CreateInstance<RtsGameConfig>();
        GridMapService gridMap = new GridMapService(10, 1f);
        Vector2Int startCell = new Vector2Int(2, 2);
        UnitData artillery = new UnitData(
            "Artillery",
            UnitType.Artillery,
            null,
            gridMap.CellToWorld(startCell),
            startCell,
            config.ArtilleryRadius,
            string.Empty,
            Team.Player,
            config.PlayerArtilleryHitPoints,
            config.ArtilleryAttackDamage,
            config.ArtilleryAttackRange,
            config.ArtilleryAttackCooldown,
            config.ArtilleryMoveSpeed,
            config.ArtilleryBuildingDamageMultiplier
        );
        gridMap.TryOccupy(startCell);
        UnitMovementSystem movement = new UnitMovementSystem(
            config,
            gridMap,
            new List<UnitData> { artillery }
        );
        Vector2Int reservedDestination = new Vector2Int(7, 7);

        Assert.AreEqual(1, movement.CommandGroupMove(
            new List<UnitData> { artillery },
            reservedDestination
        ));
        movement.Tick(0.2f);
        movement.Stop(artillery);

        Assert.IsFalse(artillery.IsMoving);
        Assert.AreEqual(0, artillery.Waypoints.Count);
        Assert.IsTrue(gridMap.IsOccupied(artillery.Cell));
        Assert.IsFalse(gridMap.IsOccupied(reservedDestination));
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
