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
    private readonly Func<Vector2Int, bool> buildFactory;

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
        Func<BuildingData, bool> train,
        Func<Vector2Int, bool> build
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
        trainInfantry = train;
        buildFactory = build;
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
                building.InfantryQueue,
                building.InfantryQueue > 0
                    ? 1f - Mathf.Clamp01(building.ProductionTimer / config.InfantryTrainingTime)
                    : 0f
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
                unit.MaxHitPoints
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

        if (action.Type == "BuildFactory")
        {
            return buildFactory(new Vector2Int(action.CellX, action.CellY))
                ? ArenaActionResult.Success("Factory construction accepted.")
                : ArenaActionResult.Reject("Factory cannot be built at that cell.");
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
                if (unit.Id == id && unit.Team == Team.Player && !result.Contains(unit))
                {
                    result.Add(unit);
                    break;
                }
            }
        }

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
        float productionProgress = 0f
    )
    {
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
            ProductionProgress = productionProgress
        };
    }
}
