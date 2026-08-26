using NUnit.Framework;
using UnityEngine;

public sealed class ArenaTelemetrySystemTests
{
    [Test]
    public void RecordsCombatOrdersAndReadableRecentLog()
    {
        float matchTime = 12.5f;
        ArenaTelemetrySystem telemetry = new ArenaTelemetrySystem(() => matchTime);
        telemetry.Reset();
        telemetry.RecordPlayerOrder("ORDER MOVE  UNITS 03 -> CELL 20,20");
        telemetry.RecordDamage(new CombatFeedbackEvent(
            Vector2.zero,
            Vector2.one,
            null,
            null,
            7,
            11,
            Team.Player,
            Team.Enemy,
            20,
            true
        ));

        Assert.AreEqual(1, telemetry.PlayerOrders);
        Assert.AreEqual(20, telemetry.PlayerDamage);
        Assert.AreEqual(1, telemetry.PlayerKills);
        StringAssert.Contains("ORDER MOVE", telemetry.GetRecentLog(6));
        StringAssert.Contains("P#7 -> E#11", telemetry.GetRecentLog(6));
        StringAssert.Contains("KILL", telemetry.GetRecentLog(6));
    }

    [Test]
    public void ResetClearsMetricsAndRestoresStandardPhase()
    {
        ArenaTelemetrySystem telemetry = new ArenaTelemetrySystem(() => 1f);
        telemetry.RecordWave(4);
        telemetry.SetDemoPhase("LIVE ENGAGEMENT");

        telemetry.Reset();

        Assert.AreEqual(0, telemetry.WavesStarted);
        Assert.AreEqual(0, telemetry.Events.Count);
        Assert.AreEqual("STANDARD MATCH", telemetry.DemoPhase);
    }
}
