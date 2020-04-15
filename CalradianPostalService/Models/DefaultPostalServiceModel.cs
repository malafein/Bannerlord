using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

using CPSModule = CalradianPostalService.CalradianPostalServiceSubModule;

namespace CalradianPostalService.Models
{
    class DefaultPostalServiceModel : PostalServiceModel
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DefaultPostalServiceModel));

        private readonly ModuleConfiguration.PostalServiceModelOptions config = ModuleConfiguration.Instance.PostalService;

        public override MBReadOnlyList<Hero> GetValidMissiveRecipients(Hero sender)
        {
            if (sender.IsHumanPlayerCharacter)
            {
                var validRecipients = (from h in Hero.All
                                       where h.HasMet && h.IsAlive && !h.IsPrisoner
                                       orderby h.Name.ToString()
                                       select h).DefaultIfEmpty().ToList();
                return new MBReadOnlyList<Hero>(validRecipients);
            }

            // TODO: find valid recipients for NPC heroes?
            return new MBReadOnlyList<Hero>(new List<Hero>());
        }

        public override MBReadOnlyList<Hero> GetValidDiplomacyRecipients(Hero sender)
        {
            // TODO: expand this list when more types of diplomacy missives are implemented or create new submenus
            var validRecipients = (from h in Hero.All
                                   where h.HasMet && h.IsAlive && !h.IsPrisoner && sender.MapFaction != h.MapFaction
                                   orderby h.Name.ToString()
                                   select h).DefaultIfEmpty().ToList();
            return new MBReadOnlyList<Hero>(validRecipients);
        }

        public override MBReadOnlyList<IFaction> GetValidJoinWarTargets(Hero sender, Hero recipient)
        {
            var validTargets = (from f in Campaign.Current.Factions
                                where sender.MapFaction.IsAtWarWith(f) && !recipient.MapFaction.IsAtWarWith(f) && !f.IsBanditFaction
                                orderby f.Name.ToString()
                                select f).DefaultIfEmpty().ToList();
            return new MBReadOnlyList<IFaction>(validTargets);
        }

        public override int GetCourierFee(Hero sender, Hero recipient)
        {
            // calculate fee based on renown.
            float multiplier = 1.0f;
            if (config.RenownAffectsCourierFee)
            {
                float renownRate = Math.Max(recipient.Clan.Renown, config.MinimumRenownAffectingFee) / Math.Max(sender.Clan.Renown, config.MinimumRenownAffectingFee);
                multiplier = renownRate * multiplier;
                CPSModule.DebugMessage($"renown rate: {renownRate}", log);
            }

            // calculate fee based on distance.
            if (config.DistanceAffectsCourierFee)
            {
                float distance = sender.GetPosition().Distance(recipient.GetPosition());
                multiplier = multiplier * distance;
                CPSModule.DebugMessage($"distance: {distance}", log);
            }

            CPSModule.DebugMessage($"total fee multiplier: {multiplier}", log);

            int fee = (int)Math.Ceiling(config.CourierRate * multiplier);
            if (config.MaximumCourierFee >= 0)
                fee = Math.Min(fee, config.MaximumCourierFee);

            return Math.Max(fee, config.MinimumCourierFee);
        }

        public override CampaignTime GetMissiveDeliveryTime(Hero sender, Hero recipient)
        {
            float days = 1.0f;
            if (config.DistanceAffectsDeliveryTime)
            {
                days = sender.GetPosition().Distance(recipient.GetPosition()) / config.MissiveDistancePerDay;
            }

            CPSModule.DebugMessage($"Delivery will take {days} days.", log);

            return CampaignTime.DaysFromNow(config.MissiveDeliveryRate * days);
        }

        public override bool IsValidRecipientOfCommand(Hero sender, Hero recipient)
        {
            // Sender is leader of the same clan or faction as recipient
            // Recipient is not a prisoner
            return (sender.MapFaction == recipient.MapFaction && sender.MapFaction.Leader == sender
                || sender.Clan == recipient.Clan && sender.Clan.Leader == sender)
                && !recipient.IsPrisoner;
        }
    }
}
