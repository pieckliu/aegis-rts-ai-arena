using UnityEngine;

internal readonly struct CombatFeedbackEvent
{
    public Vector2 SourcePosition { get; }
    public Vector2 TargetPosition { get; }
    public GameObject SourceObject { get; }
    public GameObject TargetObject { get; }
    public int SourceId { get; }
    public int TargetId { get; }
    public Team SourceTeam { get; }
    public Team TargetTeam { get; }
    public int Damage { get; }
    public bool IsLethal { get; }

    public CombatFeedbackEvent(
        Vector2 sourcePosition,
        Vector2 targetPosition,
        GameObject sourceObject,
        GameObject targetObject,
        Team sourceTeam,
        int damage,
        bool isLethal
    ) : this(
        sourcePosition,
        targetPosition,
        sourceObject,
        targetObject,
        0,
        0,
        sourceTeam,
        sourceTeam == Team.Player ? Team.Enemy : Team.Player,
        damage,
        isLethal
    )
    {
    }

    public CombatFeedbackEvent(
        Vector2 sourcePosition,
        Vector2 targetPosition,
        GameObject sourceObject,
        GameObject targetObject,
        int sourceId,
        int targetId,
        Team sourceTeam,
        Team targetTeam,
        int damage,
        bool isLethal
    )
    {
        SourcePosition = sourcePosition;
        TargetPosition = targetPosition;
        SourceObject = sourceObject;
        TargetObject = targetObject;
        SourceId = sourceId;
        TargetId = targetId;
        SourceTeam = sourceTeam;
        TargetTeam = targetTeam;
        Damage = damage;
        IsLethal = isLethal;
    }
}
