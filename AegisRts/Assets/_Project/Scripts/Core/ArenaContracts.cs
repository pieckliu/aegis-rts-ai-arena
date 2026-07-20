using System;

[Serializable]
public sealed class ArenaObservation
{
    public float MatchTime;
    public int PlayerResources;
    public bool IsTerminal;
    public string Result;
    public ArenaEntityObservation[] Buildings;
    public ArenaEntityObservation[] Units;
}

[Serializable]
public sealed class ArenaEntityObservation
{
    public int Id;
    public string Kind;
    public string Team;
    public float X;
    public float Y;
    public int CellX;
    public int CellY;
    public int HitPoints;
    public int MaxHitPoints;
}

[Serializable]
public sealed class ArenaAction
{
    public string Type;
    public int[] UnitIds;
    public int TargetId;
    public int CellX;
    public int CellY;
}

[Serializable]
public sealed class ArenaActionResult
{
    public bool Accepted;
    public string Message;

    public static ArenaActionResult Success(string message)
    {
        return new ArenaActionResult { Accepted = true, Message = message };
    }

    public static ArenaActionResult Reject(string message)
    {
        return new ArenaActionResult { Accepted = false, Message = message };
    }
}
