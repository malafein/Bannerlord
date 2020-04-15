using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

using CPSModule = CalradianPostalService.CalradianPostalServiceSubModule;

namespace CalradianPostalService.Models
{
    public class MissiveBase : IMissive
    {
        public CampaignTime CampaignTimeSent { get; set; }
        public CampaignTime CampaignTimeArrival { get; set; }
        public Hero Recipient { get; set; }
        public Hero Sender { get; set; }
        public string Text { get; set; }

        protected MissiveBase() { }
        protected MissiveBase(MissiveSyncData data) 
        {
            CampaignTimeSent = data.CampaignTimeSent;
            CampaignTimeArrival = data.CampaignTimeArrival;
            Recipient = Hero.FindFirst((Hero h) => { return h.StringId == data.RecipientId; });
            Sender = Hero.FindFirst((Hero h) => { return h.StringId == data.SenderId; });
            Text = data.Text;
        }

        public virtual void OnDelivery()
        {
        }

        public virtual void OnReturn()
        {
        }

        public virtual void OnSend()
        {
            int gold = CPSModule.PostalServiceModel.GetCourierFee(this.Sender, this.Recipient);
            GiveGoldAction.ApplyBetweenCharacters(this.Sender, null, gold, false);
        }
    }
}
