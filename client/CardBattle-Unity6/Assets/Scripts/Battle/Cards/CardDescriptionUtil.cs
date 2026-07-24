using MyProjectF.Assets.Scripts.Cards;
using MyProjectF.Assets.Scripts.Effects;

/// <summary>
/// Resolves {damage}/{armor}/… placeholders in card descriptions.
/// </summary>
public static class CardDescriptionUtil
{
    public static string Format(Card card)
    {
        if (card == null) return string.Empty;
        var text = card.cardDescription ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return "（暂无描述）";

        int damage = 0, armor = 0, cards = 0, energy = 0, hpLost = 0, aoeDamage = 0, healthSet = 0;
        var effects = card.GetCardEffects();
        if (effects != null)
        {
            foreach (var effect in effects)
            {
                switch (effect)
                {
                    case DamageEffect e: damage = e.damageAmount; break;
                    case ArmorEffect e: armor = e.armorAmount; break;
                    case DrawCardEffect e: cards = e.cardsToDraw; break;
                    case GainEnergyEffect e: energy = e.energyAmount; break;
                    case LoseHealthEffect e: hpLost = e.healthLoss; break;
                    case AOEDamageEffect e: aoeDamage = e.damageAmount; break;
                    case SetHealthEffect e: healthSet = e.newHealth; break;
                }
            }
        }

        text = text.Replace("{damage}", damage > 0 ? damage.ToString() : "-");
        text = text.Replace("{armor}", armor > 0 ? armor.ToString() : "-");
        text = text.Replace("{cards}", cards > 0 ? cards.ToString() : "-");
        text = text.Replace("{energy}", energy > 0 ? energy.ToString() : "-");
        text = text.Replace("{hpLost}", hpLost > 0 ? hpLost.ToString() : "-");
        text = text.Replace("{aoeDamage}", aoeDamage > 0 ? aoeDamage.ToString() : "-");
        text = text.Replace("{healthSet}", healthSet > 0 ? healthSet.ToString() : "-");
        return text;
    }
}
