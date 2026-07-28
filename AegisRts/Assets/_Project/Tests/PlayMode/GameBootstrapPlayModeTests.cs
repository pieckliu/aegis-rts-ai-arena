using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class GameBootstrapPlayModeTests
{
    [UnityTest]
    public IEnumerator MainScene_CreatesUguiAndStartsGame()
    {
        SceneManager.LoadScene("Main");
        yield return null;

        GameObject ui = GameObject.Find("RtsGameUI");
        Assert.IsNotNull(ui);
        Assert.IsNotNull(ui.GetComponent<Canvas>());

        GameObject startObject = GameObject.Find("Start");
        Assert.IsNotNull(startObject);
        startObject.GetComponent<Button>().onClick.Invoke();
        yield return null;

        Assert.IsNotNull(GameObject.Find("GridRoot"));
        Assert.IsNotNull(GameObject.Find("BuildingRoot"));
        GameObject playerBase = GameObject.Find("Base");
        Assert.IsNotNull(playerBase);
        Assert.IsNotNull(playerBase.GetComponent<SpriteRenderer>()?.sprite);
        Assert.AreEqual("基地", playerBase.GetComponentInChildren<TextMesh>()?.text);
        Assert.IsNull(playerBase.GetComponent<RtsEntityViewAnimator>());
        Assert.IsNotNull(GameObject.Find("FogOfWar"));
        Assert.IsNotNull(GameObject.Find("Minimap"));
        RtsGameConfig config = Resources.Load<RtsGameConfig>("RtsGameConfig");
        Assert.IsNotNull(config);
        Assert.AreEqual(48, config.MapSize);
        Assert.AreEqual(6f, Camera.main.orthographicSize, 0.01f);
        Vector2 requestedFocus = playerBase.transform.position;
        float mapHalfSize = config.MapSize * config.CellSize * 0.5f;
        float cameraMaxX = Mathf.Max(
            0f,
            mapHalfSize - Camera.main.orthographicSize * Camera.main.aspect
        );
        float cameraMaxY = mapHalfSize - Camera.main.orthographicSize;
        Assert.AreEqual(
            Mathf.Clamp(requestedFocus.x, -cameraMaxX, cameraMaxX),
            Camera.main.transform.position.x,
            0.01f
        );
        Assert.AreEqual(
            Mathf.Clamp(requestedFocus.y, -cameraMaxY, cameraMaxY),
            Camera.main.transform.position.y,
            0.01f
        );
        GameObject mapDot = GameObject.Find("PlayerBaseMapDot");
        Assert.IsNotNull(mapDot);
        Assert.AreEqual("MinimapDot", mapDot.GetComponent<Image>()?.sprite?.name);
        GameObject audioFeedback = GameObject.Find("AudioFeedback");
        Assert.IsNull(audioFeedback);
        Texture2D fogTexture = GameObject
            .Find("MinimapFog")
            .GetComponent<RawImage>()
            .texture as Texture2D;
        Assert.IsNotNull(fogTexture);
        int initialRevealedCells = fogTexture
            .GetPixels()
            .Count(color => color.a < 0.9f);
        Assert.Less(
            initialRevealedCells,
            config.MapSize * config.MapSize / 4,
            "The opening view should reveal only the area surrounding the player base."
        );

        yield return null;

        RectTransform[] healthBars = ui
            .GetComponentsInChildren<RectTransform>(true)
            .Where(rect => rect.name == "HealthBar")
            .ToArray();
        Assert.AreEqual(0, healthBars.Length, "Undamaged and unselected buildings should not show health bars.");

        GameBootstrap bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
        ArenaEntityObservation playerBaseObservation = bootstrap
            .GetArenaObservation()
            .Buildings
            .First(building =>
                building.Team == Team.Player.ToString() &&
                building.Kind == BuildingType.Base.ToString()
            );
        ArenaActionResult buildResult = bootstrap.ExecuteArenaAction(new ArenaAction
        {
            Type = "BuildFactory",
            CellX = playerBaseObservation.CellX - 3,
            CellY = playerBaseObservation.CellY - 3
        });
        ArenaActionResult trainResult = bootstrap.ExecuteArenaAction(new ArenaAction
        {
            Type = "TrainInfantry"
        });

        Assert.IsTrue(buildResult.Accepted, buildResult.Message);
        Assert.IsTrue(trainResult.Accepted, trainResult.Message);
        ArenaEntityObservation factoryObservation = bootstrap
            .GetArenaObservation()
            .Buildings
            .First(building => building.Kind == BuildingType.Factory.ToString());
        Assert.AreEqual(
            9,
            factoryObservation.OccupiedCells.Length,
            "A factory should reserve its full 3x3 footprint."
        );
        yield return null;

        GameObject notification = GameObject.Find("Notification");
        GameObject productionProgress = GameObject.Find("ProductionProgress");
        Assert.IsNotNull(notification, "Accepted production should show a short UI notification.");
        Assert.IsNotNull(productionProgress, "Queued infantry should show production progress.");
        Assert.IsTrue(productionProgress.activeSelf);
        Assert.IsNotEmpty(productionProgress.GetComponentInChildren<Text>().text);

        yield return new WaitForSeconds(3.2f);
        yield return null;

        GameObject infantry = GameObject.Find("Infantry");
        Assert.IsNotNull(infantry);
        Assert.IsNull(infantry.GetComponent<RtsEntityViewAnimator>());
        Assert.IsNull(infantry.GetComponentInChildren<TextMesh>());
        healthBars = ui
            .GetComponentsInChildren<RectTransform>(true)
            .Where(rect => rect.name == "HealthBar")
            .ToArray();
        Assert.AreEqual(
            0,
            healthBars.Length,
            "Undamaged and unselected symbolic units should not add UI clutter."
        );
        Assert.IsFalse(productionProgress.activeSelf, "Production progress should hide when the queue is empty.");

        Assert.IsNotNull(GameObject.Find("BuildGarrison"));
        ArenaActionResult buildGarrisonResult = bootstrap.ExecuteArenaAction(
            new ArenaAction
            {
                Type = "BuildGarrison",
                CellX = playerBaseObservation.CellX - 3,
                CellY = playerBaseObservation.CellY + 3
            }
        );
        Assert.IsTrue(buildGarrisonResult.Accepted, buildGarrisonResult.Message);
        ArenaEntityObservation garrisonObservation = bootstrap
            .GetArenaObservation()
            .Buildings
            .First(building => building.Kind == BuildingType.Garrison.ToString());
        Assert.AreEqual(config.GarrisonCapacity, garrisonObservation.GarrisonCapacity);
        Assert.AreEqual(
            config.GarrisonDamageMultiplier,
            garrisonObservation.GarrisonDamageMultiplier
        );
        ArenaEntityObservation infantryObservation = bootstrap
            .GetArenaObservation()
            .Units
            .First(unit => unit.Kind == UnitType.Infantry.ToString());
        ArenaActionResult garrisonResult = bootstrap.ExecuteArenaAction(
            new ArenaAction
            {
                Type = "Garrison",
                UnitIds = new[] { infantryObservation.Id },
                TargetId = garrisonObservation.Id
            }
        );
        Assert.IsTrue(garrisonResult.Accepted, garrisonResult.Message);
        yield return new WaitForSeconds(2f);
        yield return null;

        infantryObservation = bootstrap
            .GetArenaObservation()
            .Units
            .First(unit => unit.Id == infantryObservation.Id);
        Assert.AreEqual(garrisonObservation.Id, infantryObservation.GarrisonBuildingId);
        Assert.IsFalse(infantry.activeSelf);
        Assert.IsNotNull(
            ui.GetComponentsInChildren<Button>(true)
                .First(button => button.name == "EvacuateGarrison")
        );

        ArenaActionResult evacuateResult = bootstrap.ExecuteArenaAction(
            new ArenaAction
            {
                Type = "EvacuateGarrison",
                TargetId = garrisonObservation.Id
            }
        );
        Assert.IsTrue(evacuateResult.Accepted, evacuateResult.Message);
        yield return null;
        Assert.AreEqual(
            0,
            bootstrap.GetArenaObservation().Units
                .First(unit => unit.Id == infantryObservation.Id)
                .GarrisonBuildingId
        );
        Assert.IsTrue(infantry.activeSelf);

        GameObject artilleryButton = GameObject.Find("TrainArtillery");
        Assert.IsNotNull(artilleryButton);
        ArenaActionResult artilleryResult = bootstrap.ExecuteArenaAction(new ArenaAction
        {
            Type = "TrainArtillery"
        });
        Assert.IsTrue(artilleryResult.Accepted, artilleryResult.Message);
        yield return new WaitForSeconds(6.2f);
        yield return null;

        GameObject artillery = GameObject.Find("Artillery");
        Assert.IsNotNull(artillery, "The shared factory queue should produce artillery.");
        ArenaEntityObservation artilleryObservation = bootstrap
            .GetArenaObservation()
            .Units
            .First(unit => unit.Kind == UnitType.Artillery.ToString());
        Assert.IsFalse(
            artilleryObservation.IsDeployed,
            "New artillery should begin mobile and undeployed."
        );
        Button deploymentButton = ui
            .GetComponentsInChildren<Button>(true)
            .First(button => button.name == "ToggleArtilleryDeployment");
        Assert.IsNotNull(deploymentButton);
        ArenaActionResult deployResult = bootstrap.ExecuteArenaAction(new ArenaAction
        {
            Type = "DeployArtillery",
            UnitIds = new[] { artilleryObservation.Id }
        });
        Assert.IsTrue(deployResult.Accepted, deployResult.Message);
        Assert.IsTrue(
            bootstrap.GetArenaObservation().Units
                .First(unit => unit.Id == artilleryObservation.Id)
                .IsDeployed
        );
        Assert.IsNotNull(GameObject.Find("PlayerArtilleryMapDot"));

        GameObject playerUnitMapDot = GameObject.Find("PlayerUnitMapDot");
        Assert.IsNotNull(playerUnitMapDot);
        RectTransform playerUnitMapRect = playerUnitMapDot.GetComponent<RectTransform>();
        Vector2 initialMapPosition = playerUnitMapRect.anchorMin;
        ArenaEntityObservation playerUnit = bootstrap
            .GetArenaObservation()
            .Units
            .First(unit => unit.Team == Team.Player.ToString());
        ArenaActionResult moveResult = bootstrap.ExecuteArenaAction(new ArenaAction
        {
            Type = "Move",
            UnitIds = new[] { playerUnit.Id },
            CellX = Mathf.Max(1, playerUnit.CellX - 6),
            CellY = Mathf.Max(1, playerUnit.CellY - 6)
        });

        Assert.IsTrue(moveResult.Accepted, moveResult.Message);
        yield return new WaitForSeconds(0.5f);
        yield return null;

        Assert.AreNotEqual(
            initialMapPosition,
            playerUnitMapRect.anchorMin,
            "The friendly minimap marker should track the unit's live movement."
        );
        int revealedCellsAfterMovement = fogTexture
            .GetPixels()
            .Count(color => color.a < 0.9f);
        Assert.Greater(
            revealedCellsAfterMovement,
            initialRevealedCells,
            "Moving a friendly unit should expand the explored fog-of-war area."
        );
    }
}
