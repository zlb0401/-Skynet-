using CardBattle.Network;

/// <summary>
/// Holds matched 1v1 session info across scene loads.
/// </summary>
public static class OnlineSession
{
    public static bool Active { get; private set; }
    public static uint RoomId { get; private set; }
    public static uint OpponentUid { get; private set; }
    public static string OpponentName { get; private set; } = string.Empty;

    public static void Begin(MatchResult match)
    {
        Active = match.Ok;
        RoomId = match.RoomId;
        OpponentUid = match.OpponentUid;
        OpponentName = match.OpponentName ?? string.Empty;
    }

    public static void Clear()
    {
        Active = false;
        RoomId = 0;
        OpponentUid = 0;
        OpponentName = string.Empty;
    }
}
