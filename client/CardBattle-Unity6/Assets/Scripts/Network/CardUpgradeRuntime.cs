using System.Collections.Generic;
using MyProjectF.Assets.Scripts.Cards;
using MyProjectF.Assets.Scripts.Effects;
using UnityEngine;

/// <summary>
/// Builds runtime Card clones with upgrade bonuses so ScriptableObject assets stay untouched.
/// Bonus: +2 damage / +2 armor per level (PvE local battles).
/// </summary>
public static class CardUpgradeRuntime
{
    public static Card CloneWithUpgrades(Card source)
    {
        if (source == null)
        {
            return null;
        }

        var level = CardUpgradeCache.GetLevel(source.cardName);
        if (level <= 0)
        {
            return source;
        }

        var clone = ScriptableObject.CreateInstance<Card>();
        clone.cardName = source.cardName;
        clone.displayName = source.GetDisplayName();
        clone.cardDescription = AppendLevelHint(source.cardDescription, level);
        clone.cardType = source.cardType;
        clone.cardSprite = source.cardSprite;
        clone.energyCost = source.energyCost;
        clone.cardRarity = source.cardRarity;
        clone.exhaustAfterUse = source.exhaustAfterUse;
        clone.targetType = source.targetType;
        clone.effects = CloneEffects(source.GetCardEffects(), level);
        return clone;
    }

    private static string AppendLevelHint(string desc, byte level)
    {
        var baseDesc = string.IsNullOrEmpty(desc) ? string.Empty : desc.TrimEnd();
        return string.IsNullOrEmpty(baseDesc)
            ? $"（升级 Lv.{level}）"
            : $"{baseDesc}\n（升级 Lv.{level}）";
    }

    private static List<EffectData> CloneEffects(List<EffectData> source, byte level)
    {
        var list = new List<EffectData>();
        if (source == null)
        {
            return list;
        }

        var bonus = 2 * level;
        foreach (var e in source)
        {
            if (e == null)
            {
                continue;
            }

            if (e is DamageEffect dmg)
            {
                var c = new DamageEffect
                {
                    targetType = dmg.targetType,
                    damageAmount = dmg.damageAmount + bonus
                };
                list.Add(c);
            }
            else if (e is AOEDamageEffect aoe)
            {
                var c = new AOEDamageEffect
                {
                    targetType = aoe.targetType,
                    damageAmount = aoe.damageAmount + bonus
                };
                list.Add(c);
            }
            else if (e is ArmorEffect armor)
            {
                var c = new ArmorEffect
                {
                    targetType = armor.targetType
                };
                c.SetAmount(armor.armorAmount + bonus);
                list.Add(c);
            }
            else
            {
                // Non-numeric effects: keep shared reference (read-only at play time).
                list.Add(e);
            }
        }

        return list;
    }
}
