using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

using CPSModule = CalradianPostalService.CalradianPostalServiceSubModule;

namespace CalradianPostalService.Models
{
    public class MissiveCommand : MissiveBase, IMissive
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MissiveCommand));

        public override void OnDelivery()
        {
            throw new NotImplementedException();
        }

        public override void OnReturn()
        {
            throw new NotImplementedException();
        }

        public override void OnSend()
        {
            throw new NotImplementedException();
        }
    }
}
