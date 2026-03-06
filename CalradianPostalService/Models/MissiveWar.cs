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
    public class MissiveWar : MissiveBase, IMissive
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MissiveWar));

        public static readonly string TargetKingdomIdArg = "TargetKingdomId";
        public enum Arg : int { TargetKingdomId }

        public MissiveWar() { }
        public MissiveWar(MissiveSyncData data) : base(data) { }

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

            // Nothing to do if already at war
            if (Recipient.MapFaction.IsAtWarWith(targetKingdom)) return;

            bool inKingdom    = Recipient.Clan?.Kingdom != null;
            bool isOwnKingdom = inKingdom && targetKingdom == Recipient.Clan.Kingdom;
            bool isRuler      = inKingdom && Recipient == Recipient.Clan.Kingdom.Leader;

            // A ruler cannot be asked to rebel against their own kingdom
            if (isOwnKingdom && isRuler) return;

            // --- Strength ratio ---
            float alliesStr  = MissiveAcceptanceHelper.AlliesAgainstStrength(targetKingdom);
            float attackerStr, defenderStr;

            if (isOwnKingdom)
            {
                // Branch B: rebel + existing enemies vs weakened target (after clan leaves)
                attackerStr = Recipient.Clan.CurrentTotalStrength + alliesStr;
                defenderStr = Math.Max(1f, targetKingdom.CurrentTotalStrength - Recipient.Clan.CurrentTotalStrength);
            }
            else if (inKingdom)
            {
                // Branch A: recipient's kingdom + existing enemies vs target
                attackerStr = (float)Recipient.MapFaction.CurrentTotalStrength + alliesStr;
                defenderStr = targetKingdom.CurrentTotalStrength;
            }
            else
            {
                // Branch C: independent clan alone vs target
                attackerStr = Recipient.Clan.CurrentTotalStrength;
                defenderStr = targetKingdom.CurrentTotalStrength;
            }

            float strengthRatio = MissiveAcceptanceHelper.StrengthRatio(attackerStr, defenderStr);
            float strengthMod   = MissiveAcceptanceHelper.Clamp11(strengthRatio - 1f);

            // --- Traits ---
            int valor      = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Valor);
            int calculating = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Calculating);
            int honor      = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Honor);

            // --- Base formula (shared by all branches) ---
            float chance = MissiveAcceptanceHelper.RelationBase(Sender, Recipient); // 0–0.50
            chance += valor      *  0.12f;   // brave lords embrace war
            chance -= calculating * 0.10f;   // cautious lords resist
            // Strength: everyone weighs the odds; calculating lords weigh them more
            chance += strengthMod * (0.06f + Math.Max(0f, calculating * 0.06f));

            // Penalty if target is currently allied with recipient's kingdom
            if (inKingdom && !isOwnKingdom
                && Recipient.Clan.Kingdom.AlliedKingdoms.Contains(targetKingdom))
            {
                chance -= 0.25f;
            }

            // --- Branch-specific adjustments ---
            if (isOwnKingdom)
            {
                // Defection is dishonorable
                chance += honor * -0.12f;

                Kingdom joinKingdom = FindJoinKingdom(targetKingdom);
                if (joinKingdom != null)
                    chance += 0.20f; // has a kingdom to join — much more willing
                else
                    chance *= 0.4f;  // going truly alone is extremely risky
            }
            else if (!inKingdom)
            {
                // Independent clan: lower base, traits swing harder
                chance *= 0.4f;
                chance += valor      *  0.12f; // doubled valor contribution
                chance -= calculating * 0.10f; // doubled calculating contribution
            }

            chance = MissiveAcceptanceHelper.Clamp01(
                chance * ModuleConfiguration.Instance.Missives.DeclareWarDecisionFactor);

            float roll     = MBRandom.RandomFloat;
            bool  accepted = roll <= chance;

            CalradianPostalServiceSubModule.DebugMessage(
                $"[MissiveWar] relation:{Recipient.GetRelation(Sender)} valor:{valor} calc:{calculating} honor:{honor} " +
                $"strengthMod:{strengthMod:F2} chance:{chance:F2} roll:{roll:F2} accepted:{accepted}", log);

            if (!accepted)
            {
                if (Hero.MainHero == Sender)
                    CalradianPostalServiceSubModule.InfoMessage(
                        $"{Recipient} has declined your call to war against {targetKingdom.Name}.");
                return;
            }

            if (Hero.MainHero == Sender)
                CalradianPostalServiceSubModule.InfoMessage(
                    $"{Recipient} has agreed to your call to war against {targetKingdom.Name}.");

            if (isOwnKingdom)
            {
                Kingdom joinKingdom = FindJoinKingdom(targetKingdom);
                if (joinKingdom != null)
                {
                    Kingdom oldKingdom = Recipient.Clan.Kingdom;
                    ChangeKingdomAction.ApplyByJoinToKingdomByDefection(
                        Recipient.Clan, oldKingdom, joinKingdom, CampaignTime.Never, true);
                }
                else
                {
                    // Rebellion: leaves the kingdom and auto-declares war on former liege
                    ChangeKingdomAction.ApplyByLeaveWithRebellionAgainstKingdom(Recipient.Clan, true);
                }
            }
            else if (inKingdom)
            {
                // Propose war to recipient's kingdom council
                var decision = new DeclareWarDecision(Recipient.Clan, targetKingdom);
                Recipient.Clan.Kingdom.AddDecision(decision, false);
            }
            else
            {
                // Independent clan — direct declaration
                DeclareWarAction.ApplyByDefault(Recipient.MapFaction, targetKingdom);
            }
        }

        /// <summary>
        /// Finds the best kingdom for the recipient to join after defecting from targetKingdom.
        /// Prefers the sender's kingdom; otherwise picks the highest-relation kingdom at war with the target.
        /// </summary>
        private Kingdom FindJoinKingdom(Kingdom targetKingdom)
        {
            if (Sender.Clan?.Kingdom is Kingdom senderKingdom
                && senderKingdom.IsAtWarWith(targetKingdom)
                && senderKingdom.Leader.GetRelation(Recipient) > 0)
            {
                return senderKingdom;
            }

            return Kingdom.All
                .Where(k => k != targetKingdom
                         && k.IsAtWarWith(targetKingdom)
                         && k.Leader.GetRelation(Recipient) > 0)
                .OrderByDescending(k => k.Leader.GetRelation(Recipient))
                .FirstOrDefault();
        }
    }
}
