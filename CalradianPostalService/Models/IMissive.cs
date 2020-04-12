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
    [SaveableInterface(555550)]
    public interface IMissive 
    {
        [SaveableProperty(100)]
        CampaignTime CampaignTimeSent { get; set; }

        [SaveableProperty(105)]
        CampaignTime CampaignTimeArrival { get; set; }

        [SaveableProperty(110)]
        Hero Recipient { get; set; }

        [SaveableProperty(115)]
        Hero Sender { get; set; }

        [SaveableProperty(120)]
        string Text { get; set; }

        void OnSend();

        void OnDelivery();

        void OnReturn();
    }
}
