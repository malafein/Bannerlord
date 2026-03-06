using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace CalradianPostalService.Models
{
    internal static class MissiveAcceptanceHelper
    {
        /// <summary>Returns the hero's trait level (-2 to 2), or 0 if the hero or trait is null.</summary>
        internal static int Trait(Hero hero, TraitObject trait)
        {
            if (hero == null || trait == null) return 0;
            return hero.GetTraitLevel(trait);
        }

        /// <summary>Clamps a raw probability to [0, 1].</summary>
        internal static float Clamp01(float value)
            => Math.Max(0f, Math.Min(1f, value));

        /// <summary>Clamps a value to [-1, 1].</summary>
        internal static float Clamp11(float value)
            => Math.Max(-1f, Math.Min(1f, value));

        /// <summary>
        /// Base acceptance chance derived from relationship alone.
        /// Only positive relations contribute — a stranger is a harder sell than a friend.
        /// Returns a value in [0, 0.5].
        /// </summary>
        internal static float RelationBase(Hero sender, Hero recipient)
        {
            float relation = (float)recipient.GetRelation(sender);
            return Clamp01(relation / 100f) * 0.5f;
        }

        /// <summary>
        /// Returns attacker / defender strength ratio. Values above 1.0 favour the attacker.
        /// </summary>
        internal static float StrengthRatio(float attackerStrength, float defenderStrength)
        {
            if (defenderStrength <= 0f) return 2f;
            return attackerStrength / defenderStrength;
        }

        /// <summary>
        /// Returns the combined TotalStrength of all kingdoms currently at war with
        /// <paramref name="targetKingdom"/>, excluding <paramref name="targetKingdom"/> itself.
        /// </summary>
        internal static float AlliesAgainstStrength(Kingdom targetKingdom)
        {
            return Kingdom.All
                .Where(k => k != targetKingdom && k.IsAtWarWith(targetKingdom))
                .Sum(k => k.CurrentTotalStrength);
        }
    }
}
