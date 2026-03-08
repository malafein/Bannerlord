using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;

namespace CalradianPostalService.Models
{
    public class MissiveJoinWar : MissiveBase, IMissive
    {

        public static readonly string TargetKingdomIdArg = "TargetKingdomId";
        public enum Arg : int
        {
            TargetKingdomId
        }

        public MissiveJoinWar() { }
        public MissiveJoinWar(MissiveSyncData data) : base(data) { }

        public override void OnDelivery()
        {
            base.OnDelivery();

            if (Args == null || !Args.ContainsKey(Arg.TargetKingdomId))
            {
                CpsLogger.Error("Target Kingdom arg not provided.");
                return;
            }

            Kingdom kingdom = Kingdom.All.FirstOrDefault(k => k.StringId == Args[Arg.TargetKingdomId].ToString());
            if (kingdom == null)
            {
                CpsLogger.Error("Target Kingdom not found.");
                return;
            }

            // TODO: Consider exposing chance of success (move to PostalServiceModel) to be displayed in menu tooltip.
            // TODO: Influence cost for recipient to propose the war should be taken into consideration when deciding here, and when selecting recipient in courier menu
            int valor       = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Valor);
            int calculating = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Calculating);
            int earnest     = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.PersonaEarnest);
            int curt        = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.PersonaCurt);

            // Self-interest: recipient's existing hostility toward the target
            float targetRelation    = kingdom.Leader != null ? (float)Recipient.GetRelation(kingdom.Leader) : 0f;
            float targetHostility   = MissiveAcceptanceHelper.Clamp01(-targetRelation / 100f) * 0.25f; // 0–0.25; hating the target is a strong motivator

            // Strength: favourable odds encourage even reluctant lords
            float attackerStr = (float)Recipient.MapFaction.CurrentTotalStrength
                + MissiveAcceptanceHelper.AlliesAgainstStrength(kingdom);
            float defenderStr = kingdom.CurrentTotalStrength;
            float strengthMod = MissiveAcceptanceHelper.Clamp11(MissiveAcceptanceHelper.StrengthRatio(attackerStr, defenderStr) - 1f)
                * (0.06f + Math.Max(0f, calculating * 0.06f)); // calculating lords weigh the odds more

            // War burden: calculating lords resist overcommitting to new fronts
            int   currentWarCount = Recipient.MapFaction.FactionsAtWarWith.Count;
            float warBurdenMod    = -Math.Max(0, currentWarCount - 1) * 0.04f * Math.Max(0f, (float)calculating);

            // Sender credibility and relationship
            float senderPrestige = MissiveAcceptanceHelper.SenderPrestige(Sender);
            float senderRelMod   = MissiveAcceptanceHelper.Clamp11(Recipient.GetRelation(Sender) / 100f) * 0.15f; // -0.15–+0.15
            float charmBonus     = MissiveAcceptanceHelper.CharmBonus(Sender);

            float chanceOfSuccess = 0.05f               // minimum floor: diplomacy is never truly impossible
                + targetHostility
                + strengthMod
                + warBurdenMod
                + senderPrestige
                + senderRelMod
                + charmBonus
                + valor       *  0.10f   // eager fighters embrace wars
                - calculating *  0.05f   // cautious lords weigh the cost of new commitments
                + earnest     *  0.05f   // sincere lords respond to calls for loyalty
                - curt        *  0.03f;  // blunt lords don't engage in requests for favours

            chanceOfSuccess = MissiveAcceptanceHelper.Clamp01(
                chanceOfSuccess * ModuleConfiguration.Instance.Missives.JoinWarDecisionFactor);

            float roll = MBRandom.RandomFloat;
            CpsLogger.Verbose(
                $"[MissiveJoinWar] relation:{Recipient.GetRelation(Sender)} valor:{valor} calc:{calculating} " +
                $"targetRel:{targetRelation:F0} targetHostility:{targetHostility:F2} strength:{strengthMod:F2} " +
                $"warBurden:{warBurdenMod:F2} prestige:{senderPrestige:F2} senderRel:{senderRelMod:F2} charm:{charmBonus:F3} " +
                $"chanceOfSuccess:{chanceOfSuccess:F2} roll:{roll:F2}");

            if (Sender == Hero.MainHero)
            {
                Sender.HeroDeveloper?.AddSkillXp(DefaultSkills.Charm, 20f);
                CpsLogger.Verbose($"[CharmXP] +20 Charm XP granted to {Sender.Name}.");
            }

            if (roll <= chanceOfSuccess)
            {
                if (Hero.MainHero == Sender)
                    CpsLogger.Info($"{Recipient} agreed to your request. They will propose that {Recipient.MapFaction.Name} join your war."); // TODO: Inform player in OnReturn

                // They appreciate being asked to join your war, so gain some relation
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Sender, Recipient, 1);

                if (Recipient.Clan?.Kingdom != null)
                {
                    // Recipient proposes the war to their own kingdom council
                    var decision = new DeclareWarDecision(Recipient.Clan, kingdom);
                    Recipient.Clan.Kingdom.AddDecision(decision, false);
                }
                else
                {
                    // Recipient is an independent clan — no council, war starts immediately
                    DeclareWarAction.ApplyByDefault(Recipient.MapFaction, kingdom);
                }
            }
            else if (Hero.MainHero == Sender)
            {
                CpsLogger.Info($"{Recipient} declined your request to join the war."); // TODO: Inform player in OnReturn
            }
        }
    }
}
