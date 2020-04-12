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
    [SaveableClass(555552)]
    public class MissiveThreat : MissiveBase, IMissive
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MissiveThreat));

        public override void OnDelivery()
        {
            CPSModule.DebugMessage("OnDelivery called.", log);
        }

        public override void OnReturn()
        {
            CPSModule.DebugMessage("OnReturn called.", log);
        }
    }
}
