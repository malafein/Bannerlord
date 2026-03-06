using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

namespace CalradianPostalService.Models
{
    public class MissiveJoinWar : MissiveBase, IMissive
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MissiveJoinWar));

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
                float relationWithSender = (float)Recipient.GetRelation(Sender);
                float roll = MBRandom.RandomFloat;
                float chanceOfSuccess = relationWithSender / 100.0f * ModuleConfiguration.Instance.Missives.JoinWarDecisionFactor;
                CalradianPostalServiceSubModule.DebugMessage($"relationWithSender: {relationWithSender}, chanceOfSuccess:{chanceOfSuccess}, roll: {roll}", log);
                if (roll <= chanceOfSuccess)
                {
                    if (Hero.MainHero == Sender)
                    {
                        CalradianPostalServiceSubModule.InfoMessage($"{Recipient} agreed to your request. They will propose that {Recipient.MapFaction.Name} join your war."); // TODO: Inform player in OnReturn
                    }

                    // They appreciate being asked to join your war, so gain some relation and Charm xp
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Sender, Recipient, 1);

                    // TODO: Original design was to have Recipient propose war to their kingdom council via DeclareWarKingdomDecision.
                    // DeclareWarKingdomDecision and Campaign.AddDecision were removed in v1.3.x.
                    // For now, directly declare war as the simplified fallback.
                    DeclareWarAction.ApplyByDefault(Recipient.MapFaction, kingdom);
                }
                else if (Hero.MainHero == Sender)
                {
                    CalradianPostalServiceSubModule.InfoMessage($"{Recipient} declined your request to join the war."); // TODO: Inform player in OnReturn
                }
            }
            else
            {
                CalradianPostalServiceSubModule.ErrorMessage("Target Kingdom arg not provided.", log);
            }
        }
    }
}
