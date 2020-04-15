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

            // TODO: should a missive be dispatched, or should the war start now?
            DeclareWarAction.Apply(Sender.MapFaction, Recipient.MapFaction);

            if (ModuleConfiguration.Instance.Missives.DeclareWarCostsInfluence)
            {
                int influenceCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingWar(Hero.MainHero.Clan.Kingdom);
                Sender.Clan.Influence -= influenceCost;
            }
        }
    }
}
