using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

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
        /// A -0.10 to +0.15 modifier based on the hero's Charm skill, on an upward curve.
        /// Skill ~85 = 0 (breakeven). Low Charm actively hurts — a clumsy letter does damage.
        /// Higher levels accelerate: 150 ≈ +5%, 200 ≈ +9%, 300 = +15%.
        /// </summary>
        internal static float CharmBonus(Hero hero)
        {
            if (hero == null) return 0f;
            int charm = hero.GetSkillValue(DefaultSkills.Charm);
            float t       = Math.Min(charm, 300) / 300f;
            float curved  = (float)Math.Pow(t, 0.7); // convex — higher levels hit harder
            return -0.10f + curved * 0.25f;           // -0.10 at skill 0, +0.15 at skill 300
        }

        /// <summary>
        /// A 0–0.08 prestige bonus based on the sender's clan tier (capped at 6).
        /// Represents the weight a well-known, high-status lord carries in diplomacy.
        /// </summary>
        internal static float SenderPrestige(Hero sender)
        {
            int tier = Math.Min(sender.Clan?.Tier ?? 0, 6);
            return tier / 6f * 0.08f;
        }

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
