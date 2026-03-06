using log4net;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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
            base.OnDelivery();

            int generosity = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Generosity);
            int earnest    = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.PersonaEarnest);
            int curt       = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.PersonaCurt);

            // Base: diminishing returns — harder to improve an already warm relationship
            float relation = (float)Recipient.GetRelation(Sender);
            float chance   = (100f - relation) / 100f;

            // Trait modifiers
            chance += generosity * 0.05f;  // warm-hearted lords enjoy correspondence
            chance += earnest    * 0.05f;  // sincere lords appreciate genuine letters
            chance -= curt       * 0.05f;  // blunt lords don't care for pleasantries

            chance = MissiveAcceptanceHelper.Clamp01(chance);

            float roll = MBRandom.RandomFloat;
            CPSModule.DebugMessage(
                $"[MissiveFriendly] relation:{relation} generosity:{generosity} earnest:{earnest} curt:{curt} " +
                $"chance:{chance:F2} roll:{roll:F2}", log);

            if (roll <= chance)
            {
                if (Hero.MainHero == Sender)
                    CPSModule.InfoMessage($"{Recipient} appreciated your letter.");

                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Sender, Recipient, 1);
            }
            else if (Hero.MainHero == Sender)
            {
                CPSModule.InfoMessage($"{Recipient} received your letter.");
            }
        }
    }
}
