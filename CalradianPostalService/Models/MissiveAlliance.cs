using log4net;
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
        private static readonly ILog log = LogManager.GetLogger(typeof(MissiveAlliance));

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
                CalradianPostalServiceSubModule.ErrorMessage("Target Kingdom arg not provided.", log);
                return;
            }

            Kingdom targetKingdom = Kingdom.All.FirstOrDefault(k => k.StringId == Args[Arg.TargetKingdomId].ToString());
            if (targetKingdom == null)
            {
                CalradianPostalServiceSubModule.ErrorMessage("Target Kingdom not found.", log);
                return;
            }

            // Recipient must be in a kingdom to bring this to council (filtered in UI, guard here too)
            if (Recipient.Clan?.Kingdom == null) return;

            // Already allied — nothing to do
            if (Recipient.Clan.Kingdom.AlliedKingdoms.Contains(targetKingdom)) return;

            // Can't propose alliance with a kingdom they're at war with
            if (Recipient.MapFaction.IsAtWarWith(targetKingdom)) return;

            // --- Traits ---
            int honor      = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Honor);
            int calculating = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Calculating);

            // --- Strength: is the target a strategically valuable ally? ---
            // Ratio of target's strength vs average kingdom strength gives a sense of value
            float avgKingdomStr = Kingdom.All.Where(k => !k.IsBanditFaction).Average(k => k.CurrentTotalStrength);
            float allianceValue = MissiveAcceptanceHelper.Clamp11(
                MissiveAcceptanceHelper.StrengthRatio(targetKingdom.CurrentTotalStrength, avgKingdomStr) - 1f);

            float chance = MissiveAcceptanceHelper.RelationBase(Sender, Recipient); // 0–0.50
            chance += honor      *  0.12f;  // honorable lords value formal commitments
            chance += calculating * 0.10f;  // calculating lords appreciate strategic alliances
            // Strategic value of the target kingdom, weighted by how calculating the lord is
            chance += allianceValue * (0.05f + Math.Max(0f, calculating * 0.06f));

            chance = MissiveAcceptanceHelper.Clamp01(
                chance * ModuleConfiguration.Instance.Missives.AllianceDecisionFactor);

            float roll     = MBRandom.RandomFloat;
            bool  accepted = roll <= chance;

            CalradianPostalServiceSubModule.DebugMessage(
                $"[MissiveAlliance] relation:{Recipient.GetRelation(Sender)} honor:{honor} calc:{calculating} " +
                $"allianceValue:{allianceValue:F2} chance:{chance:F2} roll:{roll:F2} accepted:{accepted}", log);

            if (!accepted)
            {
                if (Hero.MainHero == Sender)
                    CalradianPostalServiceSubModule.InfoMessage(
                        $"{Recipient} has declined your request to seek an alliance with {targetKingdom.Name}.");
                return;
            }

            if (Hero.MainHero == Sender)
                CalradianPostalServiceSubModule.InfoMessage(
                    $"{Recipient} has agreed to propose an alliance with {targetKingdom.Name} to their council.");

            var decision = new StartAllianceDecision(Recipient.Clan, targetKingdom);
            Recipient.Clan.Kingdom.AddDecision(decision, false);
        }
    }
}
