using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace CalradianPostalService.Models
{
    public interface IMissive
    {
        CampaignTime CampaignTimeSent { get; set; }

        CampaignTime CampaignTimeArrival { get; set; }

        Hero Recipient { get; set; }

        Hero Sender { get; set; }

        string Text { get; set; }

        void OnSend();

        void OnDelivery();

        void OnReturn();
    }
}
