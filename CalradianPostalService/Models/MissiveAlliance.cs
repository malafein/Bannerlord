using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;

namespace CalradianPostalService.Models
{
    public class MissiveAlliance : MissiveBase, IMissive
    {

        public static readonly string TargetKingdomIdArg = "TargetKingdomId";
        public enum Arg : int { TargetKingdomId }

        public MissiveAlliance() { }
        public MissiveAlliance(MissiveSyncData data) : base(data) { }

        public override void OnSend()
        {
            base.OnSend(); // pays courier fee
        }

        public override void OnDelivery()
        {
            base.OnDelivery();

            if (Args == null || !Args.ContainsKey(Arg.TargetKingdomId))
            {
                CpsLogger.Error("Target Kingdom arg not provided.");
                return;
            }

            Kingdom targetKingdom = Kingdom.All.FirstOrDefault(k => k.StringId == Args[Arg.TargetKingdomId].ToString());
            if (targetKingdom == null)
            {
                CpsLogger.Error("Target Kingdom not found.");
                return;
            }

            // Recipient must be in a kingdom to bring this to council (filtered in UI, guard here too)
            if (Recipient.Clan?.Kingdom == null) return;

            // Already allied — nothing to do
            if (Recipient.Clan.Kingdom.AlliedKingdoms.Contains(targetKingdom)) return;

            // Can't propose alliance with a kingdom they're at war with
            if (Recipient.MapFaction.IsAtWarWith(targetKingdom)) return;

            // --- Traits ---
            int honor       = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Honor);
            int calculating = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Calculating);

            // --- Strength: is the target a strategically valuable ally? ---
            // Ratio of target's strength vs average kingdom strength gives a sense of value
            float avgKingdomStr = Kingdom.All.Where(k => !k.IsBanditFaction).Average(k => k.CurrentTotalStrength);
            float allianceValue = MissiveAcceptanceHelper.Clamp11(
                MissiveAcceptanceHelper.StrengthRatio(targetKingdom.CurrentTotalStrength, avgKingdomStr) - 1f);

            // --- Recipient's existing relationship with the target kingdom ---
            // A lord won't warmly embrace an alliance with a kingdom they distrust
            float targetRelation = targetKingdom.Leader != null
                ? (float)Recipient.GetRelation(targetKingdom.Leader) : 0f;
            float targetRelMod = MissiveAcceptanceHelper.Clamp11(targetRelation / 100f) * 0.20f; // -0.20–+0.20

            // --- Shared enemies: fighting the same foe makes alliance obviously valuable ---
            Kingdom recipientKingdom = Recipient.Clan?.Kingdom;
            bool sharedEnemy = recipientKingdom != null
                && Kingdom.All.Any(k => !k.IsBanditFaction
                    && k.IsAtWarWith(recipientKingdom) && k.IsAtWarWith(targetKingdom));
            float sharedEnemyBonus = sharedEnemy ? 0.10f : 0f;

            // --- Sender credibility and relationship ---
            float senderPrestige = MissiveAcceptanceHelper.SenderPrestige(Sender);
            float senderRelMod   = MissiveAcceptanceHelper.Clamp11(Recipient.GetRelation(Sender) / 100f) * 0.15f; // -0.15–+0.15
            float charmBonus     = MissiveAcceptanceHelper.CharmBonus(Sender);

            float chance = 0.05f              // minimum floor
                + targetRelMod
                + sharedEnemyBonus
                + senderPrestige
                + senderRelMod
                + charmBonus
                + honor       *  0.12f        // honorable lords value formal commitments
                + calculating *  0.10f        // calculating lords appreciate strategic alliances
                + allianceValue * (0.05f + Math.Max(0f, calculating * 0.06f)); // strategic weight of the target

            chance = MissiveAcceptanceHelper.Clamp01(
                chance * ModuleConfiguration.Instance.Missives.AllianceDecisionFactor);

            float roll     = MBRandom.RandomFloat;
            bool  accepted = roll <= chance;

            CpsLogger.Debug(
                $"[MissiveAlliance] relation:{Recipient.GetRelation(Sender)} honor:{honor} calc:{calculating} " +
                $"targetRel:{targetRelation:F0} targetRelMod:{targetRelMod:F2} sharedEnemy:{sharedEnemy} " +
                $"allianceValue:{allianceValue:F2} prestige:{senderPrestige:F2} senderRel:{senderRelMod:F2} charm:{charmBonus:F3} " +
                $"chance:{chance:F2} roll:{roll:F2} accepted:{accepted}");

            if (Sender == Hero.MainHero)
                Sender.HeroDeveloper?.AddSkillXp(DefaultSkills.Charm, 20f);

            if (!accepted)
            {
                if (Hero.MainHero == Sender)
                    CpsLogger.Info(
                        $"{Recipient} has declined your request to seek an alliance with {targetKingdom.Name}.");
                return;
            }

            if (Hero.MainHero == Sender)
                CpsLogger.Info(
                    $"{Recipient} has agreed to propose an alliance with {targetKingdom.Name} to their council.");

            var decision = new StartAllianceDecision(Recipient.Clan, targetKingdom);
            Recipient.Clan.Kingdom.AddDecision(decision, false);
        }
    }
}
