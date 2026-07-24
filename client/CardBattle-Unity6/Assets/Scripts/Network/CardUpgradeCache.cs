using System;
using System.Collections.Generic;
using CardBattle.Network;

/// <summary>
/// Client-side cache of C++ Auth card upgrade levels (authoritative on server).
/// </summary>
public static class CardUpgradeCache
{
    public const byte MaxLevel = 3;

    private static readonly Dictionary<string, byte> Levels = new();
    private static readonly HashSet<string> OwnedCards = new();
    public static int LastGold { get; private set; }
    public static int LastDust { get; private set; }
    public static int LastDiamond { get; private set; }
    public static int LastTicket { get; private set; }

    public static void Clear()
    {
        Levels.Clear();
        OwnedCards.Clear();
        LastGold = 0;
        LastDust = 0;
        LastDiamond = 0;
        LastTicket = 0;
    }

    public static byte GetLevel(string cardKey)
    {
        if (string.IsNullOrEmpty(cardKey))
        {
            return 0;
        }

        return Levels.TryGetValue(cardKey, out var lv) ? lv : (byte)0;
    }

    public static int MaxCopiesAllowed(byte level) => Math.Min(1 + level, 3);

    public static int NextUpgradeCost(byte currentLevel)
    {
        if (currentLevel >= MaxLevel)
        {
            return 0;
        }

        return 20 * (currentLevel + 1);
    }

    public static void SetWallet(int gold, int dust)
    {
        LastGold = gold;
        LastDust = dust;
    }

    public static void SetWallet(int gold, int dust, int diamond, int ticket)
    {
        LastGold = gold;
        LastDust = dust;
        LastDiamond = diamond;
        LastTicket = ticket;
    }

    public static void ApplyList(ListUpgradesResult result)
    {
        if (!result.Ok)
        {
            return;
        }

        Levels.Clear();
        LastGold = result.Gold;
        LastDust = result.Dust;
        LastDiamond = result.Diamond;
        LastTicket = result.Ticket;
        foreach (var e in result.Upgrades)
        {
            if (!string.IsNullOrEmpty(e.CardKey) && e.Level > 0)
            {
                Levels[e.CardKey] = e.Level;
            }
        }
    }

    public static void ApplyUpgrade(UpgradeCardResult result)
    {
        if (!result.Ok)
        {
            return;
        }

        LastGold = result.Gold;
        LastDust = result.Dust;
        LastDiamond = result.Diamond;
        LastTicket = result.Ticket;
        if (!string.IsNullOrEmpty(result.CardKey))
        {
            Levels[result.CardKey] = result.Level;
        }
    }

    public static void SetOwned(IEnumerable<string> keys)
    {
        OwnedCards.Clear();
        if (keys == null) return;
        foreach (var k in keys)
        {
            if (!string.IsNullOrEmpty(k)) OwnedCards.Add(k);
        }
    }

    public static void AddOwned(string key)
    {
        if (!string.IsNullOrEmpty(key)) OwnedCards.Add(key);
    }

    public static bool IsOwned(string cardKey)
    {
        if (string.IsNullOrEmpty(cardKey)) return false;
        return OwnedCards.Contains(cardKey);
    }

    public static bool HasOwnedData => OwnedCards.Count > 0;
}
