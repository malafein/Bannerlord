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
    public class MissiveWar : MissiveBase, IMissive
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MissiveWar));

        public MissiveWar() { }
        public MissiveWar(MissiveSyncData data) : base(data) { }

        public override void OnDelivery()
        {
            base.OnDelivery();
        }

        public override void OnSend()
        {
            base.OnSend();

            if (Sender.Clan?.Kingdom != null)
            {
                // Propose war to the sender's kingdom council; the letter is the formal declaration to the recipient
                bool ignoreInfluenceCost = !ModuleConfiguration.Instance.Missives.DeclareWarCostsInfluence;
                var decision = new DeclareWarDecision(Sender.Clan, Recipient.MapFaction);
                Sender.Clan.Kingdom.AddDecision(decision, ignoreInfluenceCost);
            }
            else
            {
                // Sender is an independent clan — no council, war starts immediately
                DeclareWarAction.ApplyByDefault(Sender.MapFaction, Recipient.MapFaction);
            }
        }
    }
}
