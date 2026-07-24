using System.Collections.Generic;
using UnityEngine;

internal sealed class RtsVisibilitySystem
{
    private static readonly Color UnexploredColor = new Color(0.015f, 0.02f, 0.03f, 0.96f);
    private static readonly Color ExploredColor = new Color(0.02f, 0.035f, 0.05f, 0.48f);
    private static readonly Color VisibleColor = new Color(0f, 0f, 0f, 0f);

    private readonly GridMapService gridMap;
    private readonly IList<BuildingData> buildings;
    private readonly IList<UnitData> units;
    private readonly float buildingSightRange;
    private readonly float unitSightRange;
    private readonly bool[,] visibleCells;
    private readonly bool[,] exploredCells;
    private readonly Color[] fogPixels;
    private readonly GameObject fogObject;
    private readonly Texture2D fogTexture;
    private readonly Sprite fogSprite;

    public Texture FogTexture => fogTexture;

    public RtsVisibilitySystem(
        GridMapService map,
        IList<BuildingData> buildingList,
        IList<UnitData> unitList,
        float buildingSight,
        float unitSight,
        Transform parent
    )
    {
        gridMap = map;
        buildings = buildingList;
        units = unitList;
        buildingSightRange = Mathf.Max(0.1f, buildingSight);
        unitSightRange = Mathf.Max(0.1f, unitSight);
        visibleCells = new bool[gridMap.MapSize, gridMap.MapSize];
        exploredCells = new bool[gridMap.MapSize, gridMap.MapSize];
        fogPixels = new Color[gridMap.MapSize * gridMap.MapSize];

        fogTexture = new Texture2D(
            gridMap.MapSize,
            gridMap.MapSize,
            TextureFormat.RGBA32,
            false
        )
        {
            name = "FogOfWarTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int i = 0; i < fogPixels.Length; i++)
        {
            fogPixels[i] = UnexploredColor;
        }

        fogTexture.SetPixels(fogPixels);
        fogTexture.Apply(false);
        fogSprite = Sprite.Create(
            fogTexture,
            new Rect(0f, 0f, gridMap.MapSize, gridMap.MapSize),
            new Vector2(0.5f, 0.5f),
            1f / gridMap.CellSize
        );
        fogSprite.name = "FogOfWarSprite";

        fogObject = new GameObject("FogOfWar", typeof(SpriteRenderer));
        fogObject.transform.SetParent(parent, false);
        fogObject.transform.position = new Vector3(0f, 0f, -0.5f);
        SpriteRenderer renderer = fogObject.GetComponent<SpriteRenderer>();
        renderer.sprite = fogSprite;
        renderer.sortingOrder = 80;
    }

    public void Tick()
    {
        ClearVisibleCells();

        foreach (BuildingData building in buildings)
        {
            if (building != null && building.Team == Team.Player)
            {
                Reveal(building.Position, buildingSightRange);
            }
        }

        foreach (UnitData unit in units)
        {
            if (unit != null && unit.Team == Team.Player)
            {
                Reveal(unit.Position, unitSightRange);
            }
        }

        UpdateFogTexture();
        UpdateEnemyPresentation();
    }

    public bool IsVisible(Vector2 worldPosition)
    {
        Vector2Int cell = gridMap.WorldToCell(worldPosition);
        return gridMap.IsCellInside(cell) && visibleCells[cell.x, cell.y];
    }

    public bool IsExplored(Vector2 worldPosition)
    {
        Vector2Int cell = gridMap.WorldToCell(worldPosition);
        return gridMap.IsCellInside(cell) && exploredCells[cell.x, cell.y];
    }

    public void Destroy()
    {
        if (fogObject != null)
        {
            Release(fogObject);
        }

        Release(fogSprite);
        Release(fogTexture);
    }

    private void ClearVisibleCells()
    {
        for (int y = 0; y < gridMap.MapSize; y++)
        {
            for (int x = 0; x < gridMap.MapSize; x++)
            {
                visibleCells[x, y] = false;
            }
        }
    }

    private void Reveal(Vector2 origin, float sightRange)
    {
        Vector2Int center = gridMap.WorldToCell(origin);
        int cellRadius = Mathf.CeilToInt(sightRange / gridMap.CellSize);
        float sightRangeSquared = sightRange * sightRange;

        for (int y = center.y - cellRadius; y <= center.y + cellRadius; y++)
        {
            for (int x = center.x - cellRadius; x <= center.x + cellRadius; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                if (!gridMap.IsCellInside(cell))
                {
                    continue;
                }

                Vector2 cellPosition = gridMap.CellToWorld(cell);

                if ((cellPosition - origin).sqrMagnitude > sightRangeSquared)
                {
                    continue;
                }

                visibleCells[x, y] = true;
                exploredCells[x, y] = true;
            }
        }
    }

    private void UpdateFogTexture()
    {
        for (int y = 0; y < gridMap.MapSize; y++)
        {
            for (int x = 0; x < gridMap.MapSize; x++)
            {
                int index = y * gridMap.MapSize + x;
                fogPixels[index] = visibleCells[x, y]
                    ? VisibleColor
                    : exploredCells[x, y]
                        ? ExploredColor
                        : UnexploredColor;
            }
        }

        fogTexture.SetPixels(fogPixels);
        fogTexture.Apply(false);
    }

    private void UpdateEnemyPresentation()
    {
        foreach (BuildingData building in buildings)
        {
            if (building != null && building.Team == Team.Enemy && building.GameObject != null)
            {
                building.GameObject.SetActive(IsVisible(building.Position));
            }
        }

        foreach (UnitData unit in units)
        {
            if (unit != null && unit.Team == Team.Enemy && unit.GameObject != null)
            {
                unit.GameObject.SetActive(IsVisible(unit.Position));
            }
        }
    }

    private static void Release(Object value)
    {
        if (value == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(value);
        }
        else
        {
            Object.DestroyImmediate(value);
        }
    }
}
