using System.Collections.Generic;
using UnityEngine;

public static class GridPathfinder
{
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 0),
        new Vector2Int(0, -1)
    };

    public static List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal,
        int width,
        int height,
        ISet<Vector2Int> blocked
    )
    {
        List<Vector2Int> empty = new List<Vector2Int>();

        if (!IsInside(start, width, height) || !IsInside(goal, width, height))
        {
            return empty;
        }

        if (start == goal)
        {
            return new List<Vector2Int> { start };
        }

        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        frontier.Enqueue(start);
        cameFrom[start] = start;

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;

                if (!IsInside(next, width, height) || cameFrom.ContainsKey(next))
                {
                    continue;
                }

                if (next != goal && blocked != null && blocked.Contains(next))
                {
                    continue;
                }

                cameFrom[next] = current;

                if (next == goal)
                {
                    return Reconstruct(cameFrom, start, goal);
                }

                frontier.Enqueue(next);
            }
        }

        return empty;
    }

    private static List<Vector2Int> Reconstruct(
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Vector2Int start,
        Vector2Int goal
    )
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int current = goal;
        path.Add(current);

        while (current != start)
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static bool IsInside(Vector2Int cell, int width, int height)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }
}
