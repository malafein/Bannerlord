using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

using CPSModule = CalradianPostalService.CalradianPostalServiceSubModule;

namespace CalradianPostalService.Models
{
    public class MissiveFriendly : MissiveBase, IMissive
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MissiveFriendly));

        public MissiveFriendly() { }
        public MissiveFriendly(MissiveSyncData data) : base(data) { }

        public override void OnDelivery()
        {
            CPSModule.DebugMessage("OnDelivery called.", log);
            float relationWithSender = (float)Recipient.GetRelation(Sender);
            float roll = MBRandom.RandomFloat;
            CPSModule.DebugMessage($"relationWithSender: {relationWithSender}, roll: {roll}", log);
            if (roll <= (100.0f - relationWithSender) / 100.0f)
            {
                if (Hero.MainHero == Sender)
                {
                    CPSModule.InfoMessage($"{Recipient} appreciated your letter.");
                }

                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Sender, Recipient, 1);
            }
            else if (Hero.MainHero == Sender)
            {
                CPSModule.InfoMessage($"{Recipient} received your letter.");
            }
        }

        public override void OnReturn()
        {
            CPSModule.DebugMessage("OnReturn called.", log);
        }
    }
}
