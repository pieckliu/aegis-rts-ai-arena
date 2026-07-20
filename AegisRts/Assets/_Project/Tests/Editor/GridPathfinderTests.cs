using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class GridPathfinderTests
{
    [Test]
    public void FindPath_RoutesAroundBlockedCells()
    {
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>
        {
            new Vector2Int(1, 0),
            new Vector2Int(1, 1)
        };

        List<Vector2Int> path = GridPathfinder.FindPath(
            new Vector2Int(0, 0),
            new Vector2Int(2, 0),
            4,
            4,
            blocked
        );

        Assert.IsNotEmpty(path);
        Assert.AreEqual(new Vector2Int(0, 0), path[0]);
        Assert.AreEqual(new Vector2Int(2, 0), path[path.Count - 1]);
        Assert.IsFalse(path.Contains(new Vector2Int(1, 0)));
    }

    [Test]
    public void FindPath_ReturnsEmpty_WhenGoalIsUnreachable()
    {
        HashSet<Vector2Int> blocked = new HashSet<Vector2Int>
        {
            new Vector2Int(1, 0),
            new Vector2Int(0, 1)
        };

        List<Vector2Int> path = GridPathfinder.FindPath(
            new Vector2Int(0, 0),
            new Vector2Int(2, 2),
            3,
            3,
            blocked
        );

        Assert.IsEmpty(path);
    }
}
