using System.Collections.Generic;
using UnityEngine;

internal sealed class GridMapService
{
    private const int SpawnSearchRadius = 4;

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right,
        Vector2Int.up
    };

    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

    public int MapSize { get; }
    public float CellSize { get; }
    public float HalfSize => MapSize * CellSize / 2f;
    public ISet<Vector2Int> OccupiedCells => occupiedCells;

    public GridMapService(int mapSize, float cellSize)
    {
        MapSize = Mathf.Max(1, mapSize);
        CellSize = Mathf.Max(0.01f, cellSize);
    }

    public bool IsCellInside(Vector2Int cell)
    {
        return cell.x >= 0 &&
            cell.x < MapSize &&
            cell.y >= 0 &&
            cell.y < MapSize;
    }

    public bool IsWorldInside(Vector2 worldPosition)
    {
        return worldPosition.x >= -HalfSize &&
            worldPosition.x <= HalfSize &&
            worldPosition.y >= -HalfSize &&
            worldPosition.y <= HalfSize;
    }

    public Vector2Int WorldToCell(Vector2 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition.x + HalfSize) / CellSize);
        int y = Mathf.FloorToInt((worldPosition.y + HalfSize) / CellSize);

        return new Vector2Int(
            Mathf.Clamp(x, 0, MapSize - 1),
            Mathf.Clamp(y, 0, MapSize - 1)
        );
    }

    public Vector2 CellToWorld(Vector2Int cell)
    {
        return new Vector2(
            -HalfSize + cell.x * CellSize + CellSize / 2f,
            -HalfSize + cell.y * CellSize + CellSize / 2f
        );
    }

    public bool IsOccupied(Vector2Int cell)
    {
        return occupiedCells.Contains(cell);
    }

    public bool TryOccupy(Vector2Int cell)
    {
        return IsCellInside(cell) && occupiedCells.Add(cell);
    }

    public bool CanOccupy(IEnumerable<Vector2Int> cells)
    {
        if (cells == null)
        {
            return false;
        }

        HashSet<Vector2Int> requestedCells = new HashSet<Vector2Int>();

        foreach (Vector2Int cell in cells)
        {
            if (!requestedCells.Add(cell) ||
                !IsCellInside(cell) ||
                IsOccupied(cell))
            {
                return false;
            }
        }

        return requestedCells.Count > 0;
    }

    public bool TryOccupy(IEnumerable<Vector2Int> cells)
    {
        if (!CanOccupy(cells))
        {
            return false;
        }

        foreach (Vector2Int cell in cells)
        {
            occupiedCells.Add(cell);
        }

        return true;
    }

    public void Release(Vector2Int cell)
    {
        occupiedCells.Remove(cell);
    }

    public void Release(IEnumerable<Vector2Int> cells)
    {
        if (cells == null)
        {
            return;
        }

        foreach (Vector2Int cell in cells)
        {
            occupiedCells.Remove(cell);
        }
    }

    public List<Vector2Int> GetSquareFootprint(Vector2Int centerCell, int radius)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        int footprintRadius = Mathf.Max(0, radius);

        for (int x = centerCell.x - footprintRadius;
             x <= centerCell.x + footprintRadius;
             x++)
        {
            for (int y = centerCell.y - footprintRadius;
                 y <= centerCell.y + footprintRadius;
                 y++)
            {
                cells.Add(new Vector2Int(x, y));
            }
        }

        return cells;
    }

    public void Clear()
    {
        occupiedCells.Clear();
    }

    public bool TryFindOpenCellNear(Vector2Int originCell, out Vector2Int openCell)
    {
        int maximumRadius = Mathf.Min(SpawnSearchRadius, MapSize - 1);

        for (int radius = 1; radius <= maximumRadius; radius++)
        {
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int cardinalCandidate = originCell + direction * radius;

                if (IsCellInside(cardinalCandidate) &&
                    !IsOccupied(cardinalCandidate))
                {
                    openCell = cardinalCandidate;
                    return true;
                }
            }

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius ||
                        x == 0 ||
                        y == 0)
                    {
                        continue;
                    }

                    Vector2Int candidate = originCell + new Vector2Int(x, y);

                    if (IsCellInside(candidate) && !IsOccupied(candidate))
                    {
                        openCell = candidate;
                        return true;
                    }
                }
            }
        }

        openCell = originCell;
        return false;
    }
}
