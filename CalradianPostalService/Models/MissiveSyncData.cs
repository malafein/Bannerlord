using CalradianPostalService.Models;
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

        public MissiveSyncData(IMissive missive)
        {
            TypeName = missive.GetType().Name;
            CampaignTimeSent = missive.CampaignTimeSent;
            CampaignTimeArrival = missive.CampaignTimeArrival;
            RecipientId = missive.Recipient.StringId;
            SenderId = missive.Sender.StringId;
            Text = missive.Text;
        }
    }
}