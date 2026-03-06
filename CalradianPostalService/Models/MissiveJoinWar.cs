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

            if (Args != null && Args.ContainsKey(Arg.TargetKingdomId))
            {
                Kingdom kingdom = (from k in Kingdom.All where k.StringId == Args[Arg.TargetKingdomId].ToString() select k).First();

                // If the recipient likes you well enough and approves of war, then they will propose it to their faction
                // TODO: For now, just base decision on relationship with sender.  Later, the recipient should also weigh whether it is in their interest.
                // TODO: Consider exposing chance of success (move to PostalServiceModel) to be displayed in menu tooltip.
                // TODO: Influence cost for recipient to propose the war should be taken into consideration when deciding here, and when selecting recipient in courier menu
                int valor       = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Valor);
                int calculating = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Calculating);
                int earnest     = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.PersonaEarnest);
                int curt        = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.PersonaCurt);

                float chanceOfSuccess = MissiveAcceptanceHelper.RelationBase(Sender, Recipient); // 0–0.50
                chanceOfSuccess += valor       *  0.12f;  // eager fighters join wars
                chanceOfSuccess -= calculating *  0.08f;  // cautious lords weigh the cost of another war
                chanceOfSuccess += earnest     *  0.05f;  // sincere lords respond to calls for loyalty
                chanceOfSuccess -= curt        *  0.05f;  // blunt lords don't engage in requests for favours

                chanceOfSuccess = MissiveAcceptanceHelper.Clamp01(
                    chanceOfSuccess * ModuleConfiguration.Instance.Missives.JoinWarDecisionFactor);

                float roll = MBRandom.RandomFloat;
                CpsLogger.Debug(
                    $"[MissiveJoinWar] relation:{Recipient.GetRelation(Sender)} valor:{valor} calc:{calculating} " +
                    $"chanceOfSuccess:{chanceOfSuccess:F2} roll:{roll:F2}");
                if (roll <= chanceOfSuccess)
                {
                    if (Hero.MainHero == Sender)
                    {
                        CpsLogger.Info($"{Recipient} agreed to your request. They will propose that {Recipient.MapFaction.Name} join your war."); // TODO: Inform player in OnReturn
                    }

                    // They appreciate being asked to join your war, so gain some relation and Charm xp
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
            else
            {
                CpsLogger.Error("Target Kingdom arg not provided.");
            }
        }
    }
}
