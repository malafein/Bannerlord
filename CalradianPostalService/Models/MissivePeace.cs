using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;

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

            if (Recipient.Clan?.Kingdom != null)
            {
                // The peace offer arrives: recipient's kingdom council votes on whether to accept
                bool ignoreInfluenceCost = !ModuleConfiguration.Instance.Missives.OfferPeaceCostsInfluence;
                var decision = new MakePeaceKingdomDecision(Recipient.Clan, Sender.MapFaction, 0, 0, false, true);
                Recipient.Clan.Kingdom.AddDecision(decision, ignoreInfluenceCost);
            }
            else
            {
                // Recipient is an independent clan — no council, peace is accepted immediately
                MakePeaceAction.Apply(Sender.MapFaction, Recipient.MapFaction);
            }
        }
    }
}
