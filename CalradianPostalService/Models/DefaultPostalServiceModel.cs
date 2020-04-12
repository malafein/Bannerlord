using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace CalradianPostalService.Models
{
    class DefaultPostalServiceModel : PostalServiceModel
    {
        const float CourierRate = 1.0f;
        public override MBReadOnlyList<Hero> GetValidMissiveRecipients(Hero hero)
        {
            if (hero.IsHumanPlayerCharacter)
            {
                var validRecipients = (from h in Hero.All
                                       where h.HasMet && h.IsAlive && !h.IsPrisoner
                                       orderby h.Name.ToString()
                                       select h).DefaultIfEmpty().ToList();
                return new MBReadOnlyList<Hero>(validRecipients);
            }

            // TODO: find valid recipients for NPC heroes.
            return new MBReadOnlyList<Hero>(new List<Hero>());
        }

        public override int GetCourierFee(Hero sender, Hero recipient)
        {
            // calculate fee based on distance.
            float distance = sender.GetPosition().Distance(recipient.GetPosition());

            return (int)Math.Ceiling(CourierRate * distance);
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
