using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;

namespace CalradianPostalService.Models
{
    public class MissivePeace : MissiveBase, IMissive
    {
        public enum Arg : int { TargetFactionId }

        public MissivePeace() { }
        public MissivePeace(MissiveSyncData data) : base(data) { }

        public override void OnSend()
        {
            base.OnSend(); // pays courier fee; influence is spent by the recipient's council
        }

        public override void OnDelivery()
        {
            base.OnDelivery();

            if (Args == null || !Args.ContainsKey(Arg.TargetFactionId))
            {
                CpsLogger.Error("Target Faction arg not provided.");
                return;
            }

            IFaction targetFaction = Campaign.Current.Factions
                .FirstOrDefault(f => f.StringId == Args[Arg.TargetFactionId].ToString());
            if (targetFaction == null)
            {
                CpsLogger.Error("Target Faction not found.");
                return;
            }

            // Nothing to do if they're no longer at war with the target
            if (!Recipient.MapFaction.IsAtWarWith(targetFaction)) return;

            // --- Traits ---
            int mercy       = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Mercy);
            int calculating = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Calculating);
            int valor       = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Valor);

            // Calculating lords weigh their current position: more wars or fewer fiefs = more receptive
            int   warCount       = Recipient.MapFaction.FactionsAtWarWith.Count;
            int   fiefsOwned     = Recipient.Clan?.Fiefs?.Count ?? 0;
            float pressureScore  = MissiveAcceptanceHelper.Clamp11((warCount - 1) * 0.2f - fiefsOwned * 0.05f);

            // Military balance of this specific war: is the recipient losing to the target?
            float targetStr    = targetFaction.CurrentTotalStrength;
            float recipientStr = (float)Recipient.MapFaction.CurrentTotalStrength;
            float warStrengthMod = MissiveAcceptanceHelper.Clamp11(
                MissiveAcceptanceHelper.StrengthRatio(targetStr, recipientStr) - 1f);

            // Sender credibility and relationship
            float senderPrestige = MissiveAcceptanceHelper.SenderPrestige(Sender);
            float senderRelMod   = MissiveAcceptanceHelper.Clamp11(Recipient.GetRelation(Sender) / 100f) * 0.15f;
            float charmBonus     = MissiveAcceptanceHelper.CharmBonus(Sender);

            // When brokering peace with a third party, what matters is how the recipient
            // feels about the target faction's leader — not the sender's faction leader.
            // When the target IS the sender's faction, use the sender's faction leader relation instead.
            float relationMod;
            bool targetIsSenderFaction = targetFaction == Sender.MapFaction;
            if (targetIsSenderFaction)
            {
                // Recipient's relation with sender's faction leader (original behaviour)
                Hero senderFactionLeader = Sender.MapFaction?.Leader;
                relationMod = (senderFactionLeader != null && senderFactionLeader != Sender)
                    ? MissiveAcceptanceHelper.Clamp11(Recipient.GetRelation(senderFactionLeader) / 100f) * 0.10f
                    : 0f;
            }
            else
            {
                // Recipient's relation with the target faction's leader — negative means they hate them
                // and are less willing to make peace; positive means they're open to reconciliation
                Hero targetLeader = targetFaction.Leader;
                relationMod = targetLeader != null
                    ? MissiveAcceptanceHelper.Clamp11(Recipient.GetRelation(targetLeader) / 100f) * 0.15f
                    : 0f;
            }

            float chance = 0.05f              // minimum floor
                + senderPrestige
                + senderRelMod
                + relationMod
                + charmBonus
                + (-valor)    * 0.05f         // cowards want out of war; brave lords are reluctant to yield
                + mercy       * 0.12f         // merciful lords want to end suffering
                + calculating * (0.08f + pressureScore * 0.08f)
                + warStrengthMod * (0.05f + Math.Max(0f, calculating * 0.05f));

            chance = MissiveAcceptanceHelper.Clamp01(
                chance * ModuleConfiguration.Instance.Missives.OfferPeaceDecisionFactor);

            float roll     = MBRandom.RandomFloat;
            bool  accepted = roll <= chance;

            CpsLogger.Debug(
                $"[MissivePeace] target:{targetFaction.Name} relation:{Recipient.GetRelation(Sender)} " +
                $"mercy:{mercy} calc:{calculating} valor:{valor} " +
                $"pressure:{pressureScore:F2} warStrength:{warStrengthMod:F2} prestige:{senderPrestige:F2} " +
                $"senderRel:{senderRelMod:F2} relationMod:{relationMod:F2} charm:{charmBonus:F3} " +
                $"chance:{chance:F2} roll:{roll:F2} accepted:{accepted}");

            if (Sender == Hero.MainHero)
                Sender.HeroDeveloper?.AddSkillXp(DefaultSkills.Charm, 20f);

            if (!accepted)
            {
                if (Hero.MainHero == Sender)
                    CpsLogger.Info(
                        $"{Recipient} has rejected your request for peace with {targetFaction.Name}.");
                return;
            }

            if (Recipient.Clan?.Kingdom != null)
            {
                if (Hero.MainHero == Sender)
                    CpsLogger.Info(
                        $"{Recipient} has agreed and will bring a peace proposal before their council.");

                var decision = new MakePeaceKingdomDecision(Recipient.Clan, targetFaction, 0, 0, false, true);
                Recipient.Clan.Kingdom.AddDecision(decision, false);
            }
            else
            {
                if (Hero.MainHero == Sender)
                    CpsLogger.Info(
                        $"{Recipient} has agreed to make peace with {targetFaction.Name}.");

                MakePeaceAction.Apply(Recipient.MapFaction, targetFaction);
            }
        }
    }
}
