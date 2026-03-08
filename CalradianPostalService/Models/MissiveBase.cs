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
    public class MissiveBase : IMissive
    {
        public CampaignTime CampaignTimeSent { get; set; }
        public CampaignTime CampaignTimeArrival { get; set; }

        // Hero references are resolved lazily: Hero.All is null during SyncData loading,
        // so we store the StringId and look up on first access.
        private Hero _recipient;
        private string _recipientId;
        public Hero Recipient
        {
            get => _recipient ?? (_recipient = !string.IsNullOrEmpty(_recipientId) ? Hero.FindFirst(h => h.StringId == _recipientId) : null);
            set { _recipient = value; _recipientId = value?.StringId; }
        }

        private Hero _sender;
        private string _senderId;
        public Hero Sender
        {
            get => _sender ?? (_sender = !string.IsNullOrEmpty(_senderId) ? Hero.FindFirst(h => h.StringId == _senderId) : null);
            set { _sender = value; _senderId = value?.StringId; }
        }

        public string Text { get; set; }

        public Dictionary<object, object> Args { get; set; }

        protected MissiveBase() { }
        protected MissiveBase(MissiveSyncData data)
        {
            CampaignTimeSent    = data.CampaignTimeSent;
            CampaignTimeArrival = data.CampaignTimeArrival;
            _recipientId        = data.RecipientId;
            _senderId           = data.SenderId;
            Text                = data.Text;
            Args                = data.Args;
        }

        public virtual void OnDelivery()
        {
        }

        public virtual void OnReturn()
        {
        }

        public virtual void OnSend()
        {
            int gold = CalradianPostalServiceSubModule.PostalServiceModel.GetCourierFee(this.Sender, this.Recipient);
            GiveGoldAction.ApplyBetweenCharacters(this.Sender, null, gold, false);
        }
    }
}
