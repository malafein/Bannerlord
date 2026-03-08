using CalradianPostalService.Models;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace CalradianPostalService
{
    public struct MissiveSyncData
    {
        public string TypeName;

        public CampaignTime CampaignTimeSent;

        public CampaignTime CampaignTimeArrival;

        public string RecipientId;

        public string SenderId;

        public string Text;

        public Dictionary<object, object> Args;

        public MissiveSyncData(IMissive missive)
        {
            TypeName = missive.GetType().Name;
            CampaignTimeSent = missive.CampaignTimeSent;
            CampaignTimeArrival = missive.CampaignTimeArrival;
            RecipientId = missive.Recipient.StringId;
            SenderId = missive.Sender.StringId;
            Text = missive.Text;
            Args = missive.Args;
        }
    }
}