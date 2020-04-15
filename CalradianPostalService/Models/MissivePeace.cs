using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CalradianPostalService.Models
{
    public class MissivePeace : MissiveBase, IMissive
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MissivePeace));

        public MissivePeace() { }
        public MissivePeace(MissiveSyncData data) : base(data) { }

        public override void OnDelivery()
        {
            base.OnDelivery();

            // TODO: This shouldn't automatically succeed
            // One option would be to add the cost of bartering for peace to the courier fee, and have it delivered to the recipient
            MakePeaceAction.Apply(Sender.MapFaction, Recipient.MapFaction);
        }

        public override void OnSend()
        {
            base.OnSend();

            if (ModuleConfiguration.Instance.Missives.DeclareWarCostsInfluence)
            {
                int influenceCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingPeace();
                Sender.Clan.Influence -= influenceCost;
            }
        }
    }
}
