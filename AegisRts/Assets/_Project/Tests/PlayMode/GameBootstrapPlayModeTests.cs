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
        Assert.AreEqual(6f, Camera.main.orthographicSize, 0.01f);
        Vector2 requestedFocus = Vector2.Lerp(
            playerBase.transform.position,
            Vector2.zero,
            0.25f
        );
        float cameraMaxX = Mathf.Max(
            0f,
            16f - Camera.main.orthographicSize * Camera.main.aspect
        );
        float cameraMaxY = 16f - Camera.main.orthographicSize;
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

        yield return null;

        RectTransform[] healthBars = ui
            .GetComponentsInChildren<RectTransform>(true)
            .Where(rect => rect.name == "HealthBar")
            .ToArray();
        Assert.AreEqual(0, healthBars.Length, "Undamaged and unselected buildings should not show health bars.");

        GameBootstrap bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
        ArenaActionResult buildResult = bootstrap.ExecuteArenaAction(new ArenaAction
        {
            Type = "BuildFactory",
            CellX = 28,
            CellY = 28
        });
        ArenaActionResult trainResult = bootstrap.ExecuteArenaAction(new ArenaAction
        {
            Type = "TrainInfantry"
        });

        Assert.IsTrue(buildResult.Accepted, buildResult.Message);
        Assert.IsTrue(trainResult.Accepted, trainResult.Message);
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
            CellX = 20,
            CellY = 20
        });

        Assert.IsTrue(moveResult.Accepted, moveResult.Message);
        yield return new WaitForSeconds(0.5f);
        yield return null;

        Assert.AreNotEqual(
            initialMapPosition,
            playerUnitMapRect.anchorMin,
            "The friendly minimap marker should track the unit's live movement."
        );
    }
}
