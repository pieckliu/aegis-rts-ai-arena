using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class RtsEntityLifecycle
{
    private readonly IList<BuildingData> buildings;
    private readonly IList<UnitData> units;
    private readonly ISet<Vector2Int> occupiedCells;
    private readonly Action<UnitData> onUnitRemoved;
    private readonly Action<BuildingData> onBuildingRemoved;

    public RtsEntityLifecycle(
        IList<BuildingData> buildingList,
        IList<UnitData> unitList,
        ISet<Vector2Int> occupied,
        Action<UnitData> unitRemoved,
        Action<BuildingData> buildingRemoved
    )
    {
        buildings = buildingList;
        units = unitList;
        occupiedCells = occupied;
        onUnitRemoved = unitRemoved;
        onBuildingRemoved = buildingRemoved;
    }

    public void DestroyUnit(UnitData unit)
    {
        if (unit == null || !units.Remove(unit))
        {
            return;
        }

        occupiedCells.Remove(unit.Cell);

        foreach (UnitData other in units)
        {
            if (other.AttackUnitTarget == unit)
            {
                other.AttackUnitTarget = null;
            }
        }

        onUnitRemoved?.Invoke(unit);

        if (unit.GameObject != null)
        {
            UnityEngine.Object.Destroy(unit.GameObject);
        }
    }

    public void DestroyBuilding(BuildingData building)
    {
        if (building == null || !buildings.Remove(building))
        {
            return;
        }

        occupiedCells.Remove(building.Cell);

        foreach (UnitData unit in units)
        {
            if (unit.AttackTarget == building)
            {
                unit.AttackTarget = null;
            }
        }

        onBuildingRemoved?.Invoke(building);

        if (building.GameObject != null)
        {
            UnityEngine.Object.Destroy(building.GameObject);
        }
    }
}
