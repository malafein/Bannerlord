using log4net;
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
        private static readonly ILog log = LogManager.GetLogger(typeof(MissivePeace));

        public MissivePeace() { }
        public MissivePeace(MissiveSyncData data) : base(data) { }

        public override void OnSend()
        {
            base.OnSend(); // pays courier fee; influence is spent by the recipient's council
        }

        public override void OnDelivery()
        {
            base.OnDelivery();

            if (!Recipient.MapFaction.IsAtWarWith(Sender.MapFaction)) return;

            // --- Traits ---
            int mercy      = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Mercy);
            int calculating = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Calculating);

            // Calculating lords weigh their current position: more wars or fewer fiefs = more receptive
            int  warCount    = Recipient.MapFaction.FactionsAtWarWith.Count;
            int  fiefsOwned  = Recipient.Clan?.Fiefs?.Count ?? 0;
            // A lord under pressure (many wars, few fiefs) gets a bonus for calculating lords
            float pressureScore = MissiveAcceptanceHelper.Clamp11((warCount - 1) * 0.2f - fiefsOwned * 0.05f);

            float chance = MissiveAcceptanceHelper.RelationBase(Sender, Recipient); // 0–0.50
            chance += mercy      * 0.12f;  // merciful lords want to end suffering
            chance += calculating * (0.08f + pressureScore * 0.08f); // pragmatic lords favour peace when losing

            chance = MissiveAcceptanceHelper.Clamp01(
                chance * ModuleConfiguration.Instance.Missives.OfferPeaceDecisionFactor);

            float roll     = MBRandom.RandomFloat;
            bool  accepted = roll <= chance;

            CalradianPostalServiceSubModule.DebugMessage(
                $"[MissivePeace] relation:{Recipient.GetRelation(Sender)} mercy:{mercy} calc:{calculating} " +
                $"pressure:{pressureScore:F2} chance:{chance:F2} roll:{roll:F2} accepted:{accepted}", log);

            if (!accepted)
            {
                if (Hero.MainHero == Sender)
                    CalradianPostalServiceSubModule.InfoMessage(
                        $"{Recipient} has rejected your offer of peace.");
                return;
            }

            if (Recipient.Clan?.Kingdom != null)
            {
                if (Hero.MainHero == Sender)
                    CalradianPostalServiceSubModule.InfoMessage(
                        $"{Recipient} has accepted your offer and will bring it before their council.");

                // Recipient personally agrees — now brings it to their kingdom council to vote
                var decision = new MakePeaceKingdomDecision(Recipient.Clan, Sender.MapFaction, 0, 0, false, true);
                Recipient.Clan.Kingdom.AddDecision(decision, false);
            }
            else
            {
                // Independent clan — no council, peace accepted directly
                if (Hero.MainHero == Sender)
                    CalradianPostalServiceSubModule.InfoMessage(
                        $"{Recipient} has accepted your offer of peace.");

                MakePeaceAction.Apply(Sender.MapFaction, Recipient.MapFaction);
            }
        }
    }
}
