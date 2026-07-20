using System.Collections.Generic;
using MyProjectF.Assets.Scripts.Cards;
using UnityEngine;

/// <summary>
/// Maps server card ids (1..4) to local Card assets used by PvE visuals.
/// </summary>
public static class OnlineCardCatalog
{
    private static readonly Dictionary<ushort, Card> Cache = new();
    private static bool _loaded;

    public static Card Get(ushort cardId)
    {
        EnsureLoaded();
        return Cache.TryGetValue(cardId, out var card) ? card : null;
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        var strike = Resources.Load<Card>("Cards/Attack Cards/Lashing Out");
        var guard = Resources.Load<Card>("Cards/Guard Cards/Gut Reaction");
        var heavyTemplate = Resources.Load<Card>("Cards/Attack Cards/Blood Rush")
            ?? Resources.Load<Card>("Cards/Attack Cards/Lashing Out");
        var focusTemplate = Resources.Load<Card>("Cards/Tactic Cards/Snap Decision")
            ?? Resources.Load<Card>("Cards/Tactic Cards/Focus Breathing");

        if (strike != null)
        {
            Cache[1] = CloneDisplayCard(strike, "OnlineStrike", "猛击",
                Card.CardType.Attack, 1, "造成 6 点伤害。", Card.TargetType.SingleEnemy);
        }

        if (guard != null)
        {
            Cache[2] = CloneDisplayCard(guard, "OnlineGuard", "防御",
                Card.CardType.Guard, 1, "获得 6 点护甲。", Card.TargetType.Self);
        }

        if (heavyTemplate != null)
        {
            Cache[3] = CloneDisplayCard(heavyTemplate, "OnlineHeavy", "重击",
                Card.CardType.Attack, 2, "造成 10 点伤害。", Card.TargetType.SingleEnemy);
        }

        if (focusTemplate != null)
        {
            Cache[4] = CloneDisplayCard(focusTemplate, "OnlineFocus", "专注",
                Card.CardType.Tactic, 0, "获得 1 点能量并抽 1 张牌。", Card.TargetType.Self);
        }
    }

    private static Card CloneDisplayCard(
        Card template,
        string id,
        string displayName,
        Card.CardType type,
        int cost,
        string description,
        Card.TargetType target)
    {
        var card = ScriptableObject.CreateInstance<Card>();
        card.cardName = id;
        card.displayName = displayName;
        card.cardDescription = description;
        card.cardType = type;
        card.energyCost = cost;
        card.cardRarity = template.cardRarity;
        card.cardSprite = template.cardSprite;
        card.targetType = target;
        card.effects = new List<MyProjectF.Assets.Scripts.Effects.EffectData>();
        return card;
    }
}
