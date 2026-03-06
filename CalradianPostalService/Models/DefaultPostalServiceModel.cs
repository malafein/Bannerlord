using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace CalradianPostalService.Models
{
    class DefaultPostalServiceModel : PostalServiceModel
    {
        private readonly ModuleConfiguration.PostalServiceModelOptions config = ModuleConfiguration.Instance.PostalService;

        public override MBReadOnlyList<Hero> GetValidMissiveRecipients(Hero sender)
        {
            if (sender.IsHumanPlayerCharacter)
            {
                var validRecipients = (from h in Hero.AllAliveHeroes
                                       where h.HasMet && !h.IsPrisoner
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
            var validRecipients = (from h in Hero.AllAliveHeroes
                                   where h.HasMet && !h.IsPrisoner && sender.MapFaction != h.MapFaction && h.Clan.Leader == h
                                   orderby h.Name.ToString()
                                   select h).DefaultIfEmpty().ToList();
            return new MBReadOnlyList<Hero>(validRecipients);
        }

        public override MBReadOnlyList<Kingdom> GetValidWarDeclarationTargets(Hero sender, Hero recipient)
        {
            bool recipientIsRuler = recipient.Clan?.Kingdom != null
                                 && recipient == recipient.Clan.Kingdom.Leader;

            var validTargets = (from k in Kingdom.All
                                where !k.IsBanditFaction
                                   && !recipient.MapFaction.IsAtWarWith(k)
                                   && (k != recipient.Clan?.Kingdom || !recipientIsRuler)
                                orderby k.Name.ToString()
                                select k).ToList();
            return new MBReadOnlyList<Kingdom>(validTargets);
        }

        public override MBReadOnlyList<Kingdom> GetValidAllianceTargets(Hero sender, Hero recipient)
        {
            if (recipient.Clan?.Kingdom == null)
                return new MBReadOnlyList<Kingdom>(new List<Kingdom>());

            var validTargets = (from k in Kingdom.All
                                where !k.IsBanditFaction
                                   && k != recipient.Clan.Kingdom
                                   && !recipient.MapFaction.IsAtWarWith(k)
                                   && !recipient.Clan.Kingdom.AlliedKingdoms.Contains(k)
                                orderby k.Name.ToString()
                                select k).ToList();
            return new MBReadOnlyList<Kingdom>(validTargets);
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
            float multiplier = 1.0f;
            if (config.RenownAffectsCourierFee)
            {
                float renownRate = Math.Max(recipient.Clan.Renown, config.MinimumRenownAffectingFee) / Math.Max(sender.Clan.Renown, config.MinimumRenownAffectingFee);
                multiplier = renownRate * multiplier;
                CpsLogger.Debug($"Courier fee renown rate: {renownRate}");
            }

            if (config.DistanceAffectsCourierFee)
            {
                float distance = sender.GetCampaignPosition().Distance(recipient.GetCampaignPosition());
                multiplier = multiplier * distance;
                CpsLogger.Debug($"Courier fee distance: {distance}");
            }

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
                days = sender.GetCampaignPosition().Distance(recipient.GetCampaignPosition()) / config.MissiveDistancePerDay;
            }

            CpsLogger.Debug($"Missive delivery will take {days} days.");

            return CampaignTime.DaysFromNow(config.MissiveDeliveryRate * days);
        }

        public override bool IsValidRecipientOfCommand(Hero sender, Hero recipient)
        {
            return (sender.MapFaction == recipient.MapFaction && sender.MapFaction.Leader == sender
                || sender.Clan == recipient.Clan && sender.Clan.Leader == sender)
                && !recipient.IsPrisoner;
        }
    }
}
