using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class ArenaOrchestrator
{
    private readonly RtsGameConfig config;
    private readonly RtsEconomyProductionSystem economy;
    private readonly IList<BuildingData> buildings;
    private readonly IList<UnitData> units;
    private readonly Func<float> getMatchTime;
    private readonly Func<bool> acceptsActions;
    private readonly Func<bool> isWon;
    private readonly Func<bool> isLost;
    private readonly Action<List<UnitData>, Vector2Int> moveUnits;
    private readonly Action<List<UnitData>, UnitData> attackUnit;
    private readonly Action<List<UnitData>, BuildingData> attackBuilding;
    private readonly Func<BuildingData, bool> trainInfantry;
    private readonly Func<BuildingData, bool> trainArtillery;
    private readonly Action<List<UnitData>, bool> setArtilleryDeployment;
    private readonly Func<List<UnitData>, BuildingData, int> garrisonUnits;
    private readonly Action<BuildingData> evacuateGarrison;
    private readonly Func<Vector2Int, bool> buildFactory;
    private readonly Func<Vector2Int, bool> buildGarrison;

    public ArenaOrchestrator(
        RtsGameConfig gameConfig,
        RtsEconomyProductionSystem economySystem,
        IList<BuildingData> buildingList,
        IList<UnitData> unitList,
        Func<float> matchTime,
        Func<bool> canAcceptActions,
        Func<bool> hasWon,
        Func<bool> hasLost,
        Action<List<UnitData>, Vector2Int> move,
        Action<List<UnitData>, UnitData> attackUnitAction,
        Action<List<UnitData>, BuildingData> attackBuildingAction,
        Func<BuildingData, bool> trainInfantryAction,
        Func<BuildingData, bool> trainArtilleryAction,
        Action<List<UnitData>, bool> deploymentAction,
        Func<List<UnitData>, BuildingData, int> garrisonAction,
        Action<BuildingData> evacuationAction,
        Func<Vector2Int, bool> build,
        Func<Vector2Int, bool> buildGarrisonAction
    )
    {
        config = gameConfig;
        economy = economySystem;
        buildings = buildingList;
        units = unitList;
        getMatchTime = matchTime;
        acceptsActions = canAcceptActions;
        isWon = hasWon;
        isLost = hasLost;
        moveUnits = move;
        attackUnit = attackUnitAction;
        attackBuilding = attackBuildingAction;
        trainInfantry = trainInfantryAction;
        trainArtillery = trainArtilleryAction;
        setArtilleryDeployment = deploymentAction;
        garrisonUnits = garrisonAction;
        evacuateGarrison = evacuationAction;
        buildFactory = build;
        buildGarrison = buildGarrisonAction;
    }

    public ArenaObservation GetObservation()
    {
        List<ArenaEntityObservation> buildingObservations = new List<ArenaEntityObservation>();
        List<ArenaEntityObservation> unitObservations = new List<ArenaEntityObservation>();

        foreach (BuildingData building in buildings)
        {
            buildingObservations.Add(ToObservation(
                building.Id,
                building.Type.ToString(),
                building.Team,
                building.Position,
                building.Cell,
                building.HitPoints,
                building.MaxHitPoints,
                building.ProductionQueueCount,
                building.ProductionQueueCount > 0
                    ? 1f - Mathf.Clamp01(
                        building.ProductionTimer /
                        economy.GetTrainingTime(building.CurrentProductionType)
                    )
                    : 0f,
                building.ProductionQueueCount > 0
                    ? building.CurrentProductionType.ToString()
                    : string.Empty,
                building.OccupiedCells,
                garrisonCount: building.GarrisonedUnits.Count,
                garrisonCapacity: building.GarrisonCapacity,
                garrisonDamageMultiplier: building.GarrisonDamageMultiplier
            ));
        }

        foreach (UnitData unit in units)
        {
            unitObservations.Add(ToObservation(
                unit.Id,
                unit.Type.ToString(),
                unit.Team,
                unit.Position,
                unit.Cell,
                unit.HitPoints,
                unit.MaxHitPoints,
                isDeployed: unit.IsDeployed,
                garrisonBuildingId: unit.GarrisonBuilding?.Id ?? 0
            ));
        }

        return new ArenaObservation
        {
            MatchTime = getMatchTime(),
            PlayerResources = economy.Resources,
            IsTerminal = isWon() || isLost(),
            Result = isWon() ? "PlayerWon" : isLost() ? "PlayerLost" : "Running",
            Buildings = buildingObservations.ToArray(),
            Units = unitObservations.ToArray()
        };
    }

    public string GetObservationJson()
    {
        return JsonUtility.ToJson(GetObservation());
    }

    public ArenaActionResult Execute(ArenaAction action)
    {
        if (!acceptsActions())
        {
            return ArenaActionResult.Reject("The match is not accepting actions.");
        }

        if (action == null || string.IsNullOrEmpty(action.Type))
        {
            return ArenaActionResult.Reject("Action type is required.");
        }

        if (action.Type == "Move")
        {
            List<UnitData> actors = FindPlayerUnits(action.UnitIds);

            if (actors.Count == 0)
            {
                return ArenaActionResult.Reject("No valid player units.");
            }

            moveUnits(actors, new Vector2Int(action.CellX, action.CellY));
            return ArenaActionResult.Success("Move command accepted.");
        }

        if (action.Type == "Attack")
        {
            List<UnitData> actors = FindPlayerUnits(action.UnitIds);

            if (actors.Count == 0)
            {
                return ArenaActionResult.Reject("No valid player units.");
            }

            foreach (UnitData target in units)
            {
                if (target.Id == action.TargetId && target.Team == Team.Enemy)
                {
                    attackUnit(actors, target);
                    return ArenaActionResult.Success("Unit attack command accepted.");
                }
            }

            foreach (BuildingData target in buildings)
            {
                if (target.Id == action.TargetId && target.Team == Team.Enemy)
                {
                    attackBuilding(actors, target);
                    return ArenaActionResult.Success("Building attack command accepted.");
                }
            }

            return ArenaActionResult.Reject("Enemy target was not found.");
        }

        if (action.Type == "TrainInfantry")
        {
            foreach (BuildingData building in buildings)
            {
                if (building.Team == Team.Player && building.Type == BuildingType.Factory)
                {
                    return trainInfantry(building)
                        ? ArenaActionResult.Success("Infantry training accepted.")
                        : ArenaActionResult.Reject("Insufficient resources or the queue is full.");
                }
            }

            return ArenaActionResult.Reject("No player factory exists.");
        }

        if (action.Type == "TrainArtillery")
        {
            foreach (BuildingData building in buildings)
            {
                if (building.Team == Team.Player && building.Type == BuildingType.Factory)
                {
                    return trainArtillery(building)
                        ? ArenaActionResult.Success("Artillery training accepted.")
                        : ArenaActionResult.Reject("Insufficient resources or the queue is full.");
                }
            }

            return ArenaActionResult.Reject("No player factory exists.");
        }

        if (action.Type == "DeployArtillery" ||
            action.Type == "UndeployArtillery")
        {
            List<UnitData> artilleryUnits = FindPlayerArtillery(action.UnitIds);

            if (artilleryUnits.Count == 0)
            {
                return ArenaActionResult.Reject("No valid player artillery units.");
            }

            bool deploy = action.Type == "DeployArtillery";
            setArtilleryDeployment(artilleryUnits, deploy);
            return ArenaActionResult.Success(
                deploy
                    ? "Artillery deployment accepted."
                    : "Artillery undeployment accepted."
            );
        }

        if (action.Type == "BuildFactory")
        {
            return buildFactory(new Vector2Int(action.CellX, action.CellY))
                ? ArenaActionResult.Success("Factory construction accepted.")
                : ArenaActionResult.Reject("Factory cannot be built at that cell.");
        }

        if (action.Type == "BuildGarrison")
        {
            return buildGarrison(new Vector2Int(action.CellX, action.CellY))
                ? ArenaActionResult.Success("Garrison construction accepted.")
                : ArenaActionResult.Reject("Garrison cannot be built at that cell.");
        }

        if (action.Type == "Garrison")
        {
            List<UnitData> actors = FindPlayerUnits(action.UnitIds);
            actors.RemoveAll(unit => unit.Type != UnitType.Infantry);

            foreach (BuildingData building in buildings)
            {
                if (building.Id == action.TargetId &&
                    building.Team == Team.Player &&
                    building.Type == BuildingType.Garrison)
                {
                    int orderedCount = garrisonUnits(actors, building);
                    return orderedCount > 0
                        ? ArenaActionResult.Success("Garrison command accepted.")
                        : ArenaActionResult.Reject(
                            "No infantry could enter this garrison."
                        );
                }
            }

            return ArenaActionResult.Reject("Player garrison was not found.");
        }

        if (action.Type == "EvacuateGarrison")
        {
            foreach (BuildingData building in buildings)
            {
                if (building.Id == action.TargetId &&
                    building.Team == Team.Player &&
                    building.Type == BuildingType.Garrison)
                {
                    evacuateGarrison(building);
                    return ArenaActionResult.Success("Garrison evacuation accepted.");
                }
            }

            return ArenaActionResult.Reject("Player garrison was not found.");
        }

        return ArenaActionResult.Reject("Unknown action type.");
    }

    private List<UnitData> FindPlayerUnits(int[] ids)
    {
        List<UnitData> result = new List<UnitData>();

        if (ids == null)
        {
            return result;
        }

        foreach (int id in ids)
        {
            foreach (UnitData unit in units)
            {
                if (unit.Id == id &&
                    unit.Team == Team.Player &&
                    unit.GarrisonBuilding == null &&
                    !result.Contains(unit))
                {
                    result.Add(unit);
                    break;
                }
            }
        }

        return result;
    }

    private List<UnitData> FindPlayerArtillery(int[] ids)
    {
        List<UnitData> result = FindPlayerUnits(ids);
        result.RemoveAll(unit => unit.Type != UnitType.Artillery);
        return result;
    }

    private static ArenaEntityObservation ToObservation(
        int id,
        string kind,
        Team team,
        Vector2 position,
        Vector2Int cell,
        int hitPoints,
        int maxHitPoints,
        int queueCount = 0,
        float productionProgress = 0f,
        string productionKind = "",
        IList<Vector2Int> occupiedCells = null,
        bool isDeployed = false,
        int garrisonCount = 0,
        int garrisonCapacity = 0,
        float garrisonDamageMultiplier = 1f,
        int garrisonBuildingId = 0
    )
    {
        ArenaCellObservation[] footprint = null;

        if (occupiedCells != null)
        {
            footprint = new ArenaCellObservation[occupiedCells.Count];

            for (int i = 0; i < occupiedCells.Count; i++)
            {
                footprint[i] = new ArenaCellObservation
                {
                    X = occupiedCells[i].x,
                    Y = occupiedCells[i].y
                };
            }
        }

        return new ArenaEntityObservation
        {
            Id = id,
            Kind = kind,
            Team = team.ToString(),
            X = position.x,
            Y = position.y,
            CellX = cell.x,
            CellY = cell.y,
            HitPoints = hitPoints,
            MaxHitPoints = maxHitPoints,
            QueueCount = queueCount,
            ProductionKind = productionKind,
            ProductionProgress = productionProgress,
            OccupiedCells = footprint,
            IsDeployed = isDeployed,
            GarrisonCount = garrisonCount,
            GarrisonCapacity = garrisonCapacity,
            GarrisonDamageMultiplier = garrisonDamageMultiplier,
            GarrisonBuildingId = garrisonBuildingId
        };
    }
}
