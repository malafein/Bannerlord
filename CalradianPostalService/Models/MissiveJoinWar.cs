using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalradianPostalService.Models
{
    public class MissiveJoinWar : MissiveBase, IMissive
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MissiveJoinWar));

        public MissiveJoinWar() { }
        public MissiveJoinWar(MissiveSyncData data) : base(data) { }

        public override void OnDelivery()
        {
            base.OnDelivery();

            // If the recipient likes you well enough and approves of war, then they will propose it to their faction
            CalradianPostalServiceSubModule.DebugMessage("TODO: implement MissiveJoinWar.OnDelivery", log);
        }
    }
}
