using System.Collections.Generic;
using UnityEngine;

internal sealed class UnitMovementSystem
{
    private const int FormationSearchRadius = 6;
    private const int SeparationIterations = 3;
    private static readonly Vector2[] ClearanceDirections =
    {
        Vector2.zero,
        Vector2.right,
        Vector2.left,
        Vector2.up,
        Vector2.down,
        new Vector2(0.7071f, 0.7071f),
        new Vector2(-0.7071f, 0.7071f),
        new Vector2(0.7071f, -0.7071f),
        new Vector2(-0.7071f, -0.7071f)
    };

    private readonly RtsGameConfig config;
    private readonly GridMapService gridMap;
    private readonly IList<UnitData> units;
    private readonly HashSet<Vector2Int> collisionObstacleCells =
        new HashSet<Vector2Int>();

    public UnitMovementSystem(
        RtsGameConfig gameConfig,
        GridMapService mapService,
        IList<UnitData> unitList
    )
    {
        config = gameConfig;
        gridMap = mapService;
        units = unitList;
    }

    public int CommandGroupMove(IList<UnitData> actors, Vector2Int centerCell)
    {
        return CommandGroupMove(actors, centerCell, gridMap.CellToWorld(centerCell));
    }

    public int CommandGroupMove(
        IList<UnitData> actors,
        Vector2Int centerCell,
        Vector2 centerWorldPosition
    )
    {
        List<UnitData> movableUnits = new List<UnitData>();
        HashSet<Vector2Int> actorCells = new HashSet<Vector2Int>();

        foreach (UnitData unit in actors)
        {
            if (unit == null || unit.Team != Team.Player)
            {
                continue;
            }

            movableUnits.Add(unit);
            actorCells.Add(unit.Cell);
        }

        if (movableUnits.Count == 0)
        {
            return 0;
        }

        List<Vector2Int> targetCells = FindFormationCells(
            centerCell,
            movableUnits.Count,
            actorCells
        );

        if (targetCells.Count == 0)
        {
            return 0;
        }

        HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>(gridMap.OccupiedCells);
        HashSet<Vector2Int> obstacleCells = new HashSet<Vector2Int>(blockedCells);

        foreach (Vector2Int actorCell in actorCells)
        {
            blockedCells.Remove(actorCell);
        }

        foreach (UnitData unit in units)
        {
            if (unit != null)
            {
                obstacleCells.Remove(unit.Cell);
            }
        }

        foreach (UnitData unit in movableUnits)
        {
            gridMap.Release(unit.Cell);
        }

        Vector2 formationOffset = ClampTargetPosition(centerWorldPosition) -
            gridMap.CellToWorld(centerCell);
        int commandedCount = Mathf.Min(movableUnits.Count, targetCells.Count);
        int acceptedCount = 0;

        for (int i = 0; i < commandedCount; i++)
        {
            UnitData unit = movableUnits[i];
            Vector2Int targetCell = targetCells[i];
            Vector2 targetPosition = ClampTargetPosition(
                gridMap.CellToWorld(targetCell) + formationOffset
            );

            if (!IsPositionClear(targetPosition, unit.Radius, obstacleCells))
            {
                targetPosition = gridMap.CellToWorld(targetCell);
            }

            bool canMoveDirectly = IsDirectPathClear(
                unit.Position,
                targetPosition,
                unit.Radius,
                obstacleCells
            );
            List<Vector2Int> path = canMoveDirectly
                ? null
                : GridPathfinder.FindPath(
                    unit.Cell,
                    targetCell,
                    gridMap.MapSize,
                    gridMap.MapSize,
                    blockedCells
                );

            if ((!canMoveDirectly && path.Count == 0) || !gridMap.TryOccupy(targetCell))
            {
                gridMap.TryOccupy(unit.Cell);
                unit.IsMoving = false;
                continue;
            }

            unit.AttackTarget = null;
            unit.AttackUnitTarget = null;
            unit.Cell = targetCell;
            unit.TargetCell = targetCell;
            unit.TargetPosition = targetPosition;
            unit.Waypoints.Clear();

            if (canMoveDirectly)
            {
                AddWaypointIfDistinct(unit, targetPosition);
            }
            else
            {
                for (int pathIndex = 1; pathIndex < path.Count; pathIndex++)
                {
                    Vector2 waypoint = pathIndex == path.Count - 1
                        ? targetPosition
                        : gridMap.CellToWorld(path[pathIndex]);
                    AddWaypointIfDistinct(unit, waypoint);
                }

                AddWaypointIfDistinct(unit, targetPosition);
            }

            unit.IsMoving = unit.Waypoints.Count > 0;
            blockedCells.Add(targetCell);
            acceptedCount++;
        }

        return acceptedCount;
    }

    private Vector2 ClampTargetPosition(Vector2 position)
    {
        return ClampUnitPosition(position, config.InfantryRadius);
    }

    private Vector2 ClampUnitPosition(Vector2 position, float radius)
    {
        float margin = Mathf.Min(Mathf.Max(0f, radius), gridMap.HalfSize);
        return new Vector2(
            Mathf.Clamp(position.x, -gridMap.HalfSize + margin, gridMap.HalfSize - margin),
            Mathf.Clamp(position.y, -gridMap.HalfSize + margin, gridMap.HalfSize - margin)
        );
    }

    private bool IsDirectPathClear(
        Vector2 start,
        Vector2 end,
        float unitRadius,
        ISet<Vector2Int> obstacleCells
    )
    {
        if (obstacleCells == null || obstacleCells.Count == 0)
        {
            return true;
        }

        float distance = Vector2.Distance(start, end);
        int sampleCount = Mathf.Max(
            1,
            Mathf.CeilToInt(distance / Mathf.Max(0.05f, gridMap.CellSize * 0.2f))
        );

        for (int sample = 0; sample <= sampleCount; sample++)
        {
            Vector2 point = Vector2.Lerp(start, end, sample / (float)sampleCount);

            if (!IsPositionClear(point, unitRadius, obstacleCells))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsPositionClear(
        Vector2 position,
        float unitRadius,
        ISet<Vector2Int> obstacleCells
    )
    {
        if (obstacleCells == null || obstacleCells.Count == 0)
        {
            return true;
        }

        float clearance = Mathf.Max(0f, unitRadius) * 0.9f;

        foreach (Vector2 direction in ClearanceDirections)
        {
            if (obstacleCells.Contains(
                gridMap.WorldToCell(position + direction * clearance)
            ))
            {
                return false;
            }
        }

        return true;
    }

    private static void AddWaypointIfDistinct(UnitData unit, Vector2 waypoint)
    {
        Vector2 previous = unit.Waypoints.Count > 0
            ? unit.Waypoints[unit.Waypoints.Count - 1]
            : unit.Position;

        if (Vector2.Distance(previous, waypoint) > 0.01f)
        {
            unit.Waypoints.Add(waypoint);
        }
    }

    public List<Vector2Int> FindFormationCells(
        Vector2Int centerCell,
        int requiredCount,
        ISet<Vector2Int> actorCells
    )
    {
        List<Vector2Int> result = new List<Vector2Int>();
        HashSet<Vector2Int> reservedCells = new HashSet<Vector2Int>();

        for (int radius = 0; radius <= FormationSearchRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    Vector2Int candidate = new Vector2Int(centerCell.x + dx, centerCell.y + dy);

                    if (!gridMap.IsCellInside(candidate) || reservedCells.Contains(candidate))
                    {
                        continue;
                    }

                    bool blockedByOtherEntity =
                        gridMap.IsOccupied(candidate) &&
                        (actorCells == null || !actorCells.Contains(candidate));

                    if (blockedByOtherEntity)
                    {
                        continue;
                    }

                    result.Add(candidate);
                    reservedCells.Add(candidate);

                    if (result.Count >= requiredCount)
                    {
                        return result;
                    }
                }
            }
        }

        return result;
    }

    public void MoveTowards(UnitData unit, Vector2 targetPosition, float deltaTime)
    {
        Vector2 nextPosition = Vector2.MoveTowards(
            unit.Position,
            targetPosition,
            config.UnitMoveSpeed * deltaTime
        );

        ApplyPosition(unit, nextPosition);
        SyncCombatCell(unit);
    }

    public void Tick(float deltaTime)
    {
        foreach (UnitData unit in units)
        {
            if (unit.AttackTarget != null || unit.AttackUnitTarget != null || !unit.IsMoving)
            {
                continue;
            }

            Vector2 movementTarget = unit.Waypoints.Count > 0
                ? unit.Waypoints[0]
                : unit.TargetPosition;
            Vector2 nextPosition = Vector2.MoveTowards(
                unit.Position,
                movementTarget,
                config.UnitMoveSpeed * deltaTime
            );

            ApplyPosition(unit, nextPosition);

            if (Vector2.Distance(nextPosition, movementTarget) >= 0.01f)
            {
                continue;
            }

            ApplyPosition(unit, movementTarget);

            if (unit.Waypoints.Count > 0)
            {
                unit.Waypoints.RemoveAt(0);
            }

            unit.IsMoving = unit.Waypoints.Count > 0;

            if (!unit.IsMoving)
            {
                Debug.Log($"{unit.DisplayName} arrived at cell {unit.Cell}");
            }
        }

        ResolveUnitCollisions();
    }

    private void ResolveUnitCollisions()
    {
        RebuildCollisionObstacleCells();

        for (int iteration = 0; iteration < SeparationIterations; iteration++)
        {
            bool foundOverlap = false;

            for (int firstIndex = 0; firstIndex < units.Count; firstIndex++)
            {
                UnitData first = units[firstIndex];

                if (first == null)
                {
                    continue;
                }

                for (int secondIndex = firstIndex + 1; secondIndex < units.Count; secondIndex++)
                {
                    UnitData second = units[secondIndex];

                    if (second == null)
                    {
                        continue;
                    }

                    float minimumDistance = Mathf.Max(
                        0.01f,
                        first.Radius + second.Radius + config.UnitCollisionPadding
                    );
                    Vector2 delta = second.Position - first.Position;
                    float distance = delta.magnitude;

                    if (distance >= minimumDistance - 0.001f)
                    {
                        continue;
                    }

                    foundOverlap = true;
                    Vector2 direction = distance > 0.0001f
                        ? delta / distance
                        : ((firstIndex + secondIndex) & 1) == 0
                            ? Vector2.right
                            : Vector2.up;
                    float overlap = minimumDistance - distance;
                    Vector2 firstCandidate = ClampUnitPosition(
                        first.Position - direction * (overlap * 0.5f),
                        first.Radius
                    );
                    Vector2 secondCandidate = ClampUnitPosition(
                        second.Position + direction * (overlap * 0.5f),
                        second.Radius
                    );
                    bool canMoveFirst = IsPositionClear(
                        firstCandidate,
                        first.Radius,
                        collisionObstacleCells
                    );
                    bool canMoveSecond = IsPositionClear(
                        secondCandidate,
                        second.Radius,
                        collisionObstacleCells
                    );

                    if (canMoveFirst && canMoveSecond)
                    {
                        ApplyPosition(first, firstCandidate);
                        ApplyPosition(second, secondCandidate);
                    }
                    else if (canMoveFirst)
                    {
                        Vector2 fullCandidate = ClampUnitPosition(
                            first.Position - direction * overlap,
                            first.Radius
                        );

                        if (IsPositionClear(
                            fullCandidate,
                            first.Radius,
                            collisionObstacleCells
                        ))
                        {
                            ApplyPosition(first, fullCandidate);
                        }
                    }
                    else if (canMoveSecond)
                    {
                        Vector2 fullCandidate = ClampUnitPosition(
                            second.Position + direction * overlap,
                            second.Radius
                        );

                        if (IsPositionClear(
                            fullCandidate,
                            second.Radius,
                            collisionObstacleCells
                        ))
                        {
                            ApplyPosition(second, fullCandidate);
                        }
                    }
                }
            }

            if (!foundOverlap)
            {
                break;
            }
        }
    }

    private void RebuildCollisionObstacleCells()
    {
        collisionObstacleCells.Clear();

        foreach (Vector2Int occupiedCell in gridMap.OccupiedCells)
        {
            collisionObstacleCells.Add(occupiedCell);
        }

        foreach (UnitData unit in units)
        {
            if (unit != null)
            {
                collisionObstacleCells.Remove(unit.Cell);
            }
        }
    }

    private void SyncCombatCell(UnitData unit)
    {
        Vector2Int currentCell = gridMap.WorldToCell(unit.Position);

        if (currentCell == unit.Cell)
        {
            return;
        }

        gridMap.Release(unit.Cell);
        unit.Cell = currentCell;
        unit.TargetCell = currentCell;
        gridMap.TryOccupy(currentCell);
    }

    private static void ApplyPosition(UnitData unit, Vector2 position)
    {
        if (unit.GameObject != null)
        {
            unit.GameObject.transform.position = new Vector3(position.x, position.y, 0f);
        }

        unit.Position = position;
    }
}
