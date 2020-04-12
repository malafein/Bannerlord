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
    [SaveableClass(555552)]
    public class MissiveThreat : MissiveBase, IMissive
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MissiveThreat));

        public override void OnDelivery()
        {
            CPSModule.DebugMessage("OnDelivery called.", log);
            float relationWithSender = (float)Recipient.GetRelation(Sender);
            float roll = MBRandom.RandomFloat;
            CPSModule.DebugMessage($"relationWithSender: {relationWithSender}, roll: {roll}", log);
            if (roll <= (relationWithSender + 100.0f) / 100.0f)
            {
                if (Hero.MainHero == Sender)
                {
                    CPSModule.InfoMessage($"{Recipient} was angered your letter.");
                }

                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Sender, Recipient, -1);
            }
            else if (Hero.MainHero == Sender)
            {
                CPSModule.InfoMessage($"{Recipient} received your letter, but was not impressed.");
            }
        }

        public override void OnReturn()
        {
            CPSModule.DebugMessage("OnReturn called.", log);
        }
    }
}
