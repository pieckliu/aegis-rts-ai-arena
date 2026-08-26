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

    [Test]
    public void FindOpenCellNear_SelectsBuildingApproachClosestToUnit()
    {
        GridMapService gridMap = new GridMapService(12, 1f);
        Vector2Int buildingCell = new Vector2Int(5, 5);
        Assert.IsTrue(gridMap.TryOccupy(
            gridMap.GetSquareFootprint(buildingCell, 1)
        ));

        Assert.IsTrue(gridMap.TryFindOpenCellNear(
            buildingCell,
            new Vector2Int(5, 10),
            out Vector2Int approachFromAbove
        ));
        Assert.AreEqual(new Vector2Int(5, 7), approachFromAbove);

        Assert.IsTrue(gridMap.TryFindOpenCellNear(
            buildingCell,
            new Vector2Int(0, 5),
            out Vector2Int approachFromLeft
        ));
        Assert.AreEqual(new Vector2Int(3, 5), approachFromLeft);
    }
}
