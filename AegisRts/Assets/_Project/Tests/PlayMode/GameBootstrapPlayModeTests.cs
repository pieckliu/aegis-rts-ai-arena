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

        yield return null;

        Canvas canvas = ui.GetComponent<Canvas>();
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
        yield return new WaitForSeconds(3.2f);
        yield return null;

        GameObject infantry = GameObject.Find("Infantry");
        Assert.IsNotNull(infantry);
        healthBars = ui
            .GetComponentsInChildren<RectTransform>(true)
            .Where(rect => rect.name == "HealthBar")
            .ToArray();
        Vector3 expectedScreen = Camera.main.WorldToScreenPoint(
            infantry.transform.position + Vector3.up * 0.58f
        );
        Vector2 expectedCanvas = new Vector2(expectedScreen.x, expectedScreen.y) / canvas.scaleFactor;
        float nearestDistance = healthBars.Min(
            bar => Vector2.Distance(bar.anchoredPosition, expectedCanvas)
        );

        Assert.AreEqual(1, healthBars.Length, "Every infantry unit should have exactly one visible health bar.");
        Assert.Less(nearestDistance, 2f, "The infantry health bar should stay directly above its unit.");
    }
}
