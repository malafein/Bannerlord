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
    public class MissiveCommand : MissiveBase, IMissive
    {

        public MissiveCommand() { }
        public MissiveCommand(MissiveSyncData data) : base(data) { }

        public override void OnDelivery()
        {
            base.OnDelivery();
            throw new NotImplementedException();
        }

        public override void OnReturn()
        {
            base.OnReturn();
            throw new NotImplementedException();
        }

        public override void OnSend()
        {
            base.OnSend();
            throw new NotImplementedException();
        }
    }
}
