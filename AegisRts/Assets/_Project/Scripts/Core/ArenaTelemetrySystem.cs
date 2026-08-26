using System;
using System.Collections.Generic;
using System.Text;

internal enum ArenaTelemetryKind
{
    Match,
    Economy,
    Production,
    Movement,
    Targeting,
    Combat,
    Lifecycle,
    Deployment,
    Garrison,
    Wave,
    Demo
}

internal readonly struct ArenaTelemetryEvent
{
    public int Sequence { get; }
    public float MatchTime { get; }
    public ArenaTelemetryKind Kind { get; }
    public Team? Team { get; }
    public string Message { get; }

    public ArenaTelemetryEvent(
        int sequence,
        float matchTime,
        ArenaTelemetryKind kind,
        Team? team,
        string message
    )
    {
        Sequence = sequence;
        MatchTime = matchTime;
        Kind = kind;
        Team = team;
        Message = message ?? string.Empty;
    }
}

internal sealed class ArenaTelemetrySystem
{
    private const int MaxEvents = 80;

    private readonly Func<float> getMatchTime;
    private readonly List<ArenaTelemetryEvent> events =
        new List<ArenaTelemetryEvent>();
    private int nextSequence;

    public IReadOnlyList<ArenaTelemetryEvent> Events => events;
    public int PlayerDamage { get; private set; }
    public int EnemyDamage { get; private set; }
    public int PlayerKills { get; private set; }
    public int EnemyKills { get; private set; }
    public int PlayerOrders { get; private set; }
    public int UnitsProduced { get; private set; }
    public int BuildingsConstructed { get; private set; }
    public int WavesStarted { get; private set; }
    public string CurrentObjective { get; private set; } = "Establish battlefield control";
    public string DemoPhase { get; private set; } = "STANDARD MATCH";

    public ArenaTelemetrySystem(Func<float> matchTime)
    {
        getMatchTime = matchTime;
    }

    public void Reset()
    {
        events.Clear();
        nextSequence = 0;
        PlayerDamage = 0;
        EnemyDamage = 0;
        PlayerKills = 0;
        EnemyKills = 0;
        PlayerOrders = 0;
        UnitsProduced = 0;
        BuildingsConstructed = 0;
        WavesStarted = 0;
        CurrentObjective = "Establish battlefield control";
        DemoPhase = "STANDARD MATCH";
    }

    public void Record(
        ArenaTelemetryKind kind,
        string message,
        Team? team = null
    )
    {
        events.Add(new ArenaTelemetryEvent(
            ++nextSequence,
            getMatchTime?.Invoke() ?? 0f,
            kind,
            team,
            message
        ));

        if (events.Count > MaxEvents)
        {
            events.RemoveAt(0);
        }
    }

    public void RecordPlayerOrder(string message)
    {
        PlayerOrders++;
        Record(ArenaTelemetryKind.Movement, message, Team.Player);
    }

    public void RecordProduced(UnitData unit)
    {
        if (unit.Team == Team.Player)
        {
            UnitsProduced++;
        }

        Record(
            ArenaTelemetryKind.Production,
            $"{ShortTeam(unit.Team)}#{unit.Id} spawned {unit.Type} @ {unit.Cell.x},{unit.Cell.y}",
            unit.Team
        );
    }

    public void RecordBuilding(BuildingData building)
    {
        if (building.Team == Team.Player && building.Type != BuildingType.Base)
        {
            BuildingsConstructed++;
        }

        Record(
            ArenaTelemetryKind.Economy,
            $"{ShortTeam(building.Team)}#{building.Id} constructed {building.Type}",
            building.Team
        );
    }

    public void RecordDamage(CombatFeedbackEvent feedback)
    {
        if (feedback.SourceTeam == Team.Player)
        {
            PlayerDamage += feedback.Damage;

            if (feedback.IsLethal)
            {
                PlayerKills++;
            }
        }
        else
        {
            EnemyDamage += feedback.Damage;

            if (feedback.IsLethal)
            {
                EnemyKills++;
            }
        }

        string suffix = feedback.IsLethal ? "  KILL" : string.Empty;
        Record(
            ArenaTelemetryKind.Combat,
            $"{ShortTeam(feedback.SourceTeam)}#{feedback.SourceId} -> " +
            $"{ShortTeam(feedback.TargetTeam)}#{feedback.TargetId}  " +
            $"DMG {feedback.Damage}{suffix}",
            feedback.SourceTeam
        );
    }

    public void RecordWave(int activeEnemyCount)
    {
        WavesStarted++;
        Record(
            ArenaTelemetryKind.Wave,
            $"AI wave {WavesStarted:00} deployed  ACTIVE {activeEnemyCount:00}",
            Team.Enemy
        );
    }

    public void SetObjective(string objective)
    {
        CurrentObjective = string.IsNullOrEmpty(objective)
            ? "No active objective"
            : objective;
    }

    public void SetDemoPhase(string phase)
    {
        DemoPhase = string.IsNullOrEmpty(phase) ? "SHOWCASE" : phase;
        Record(ArenaTelemetryKind.Demo, $"PHASE // {DemoPhase}");
    }

    public string GetRecentLog(int maximumLines)
    {
        int count = Math.Max(0, maximumLines);
        int start = Math.Max(0, events.Count - count);
        StringBuilder builder = new StringBuilder();

        for (int index = start; index < events.Count; index++)
        {
            ArenaTelemetryEvent item = events[index];
            string color = item.Team == Team.Player
                ? "#FFE61F"
                : item.Team == Team.Enemy
                    ? "#FF5A32"
                    : "#72DDF7";
            builder.Append("<color=");
            builder.Append(color);
            builder.Append(">[");
            builder.Append(item.MatchTime.ToString("000.0"));
            builder.Append("] ");
            builder.Append(item.Kind.ToString().ToUpperInvariant().PadRight(10));
            builder.Append("</color>  ");
            builder.Append(item.Message);

            if (index < events.Count - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string ShortTeam(Team team)
    {
        return team == Team.Player ? "P" : "E";
    }
}
