using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CalradianPostalService.Models
{
    public abstract class PostalServiceModel : GameModel
    {
        protected PostalServiceModel() { }

        public abstract MBReadOnlyList<Hero> GetValidMissiveRecipients(Hero sender);

        public abstract MBReadOnlyList<Hero> GetValidDiplomacyRecipients(Hero sender);

        public abstract MBReadOnlyList<IFaction> GetValidJoinWarTargets(Hero sender, Hero recipient);

        /// <summary>Returns kingdoms the sender can ask the recipient to declare war against.</summary>
        public abstract MBReadOnlyList<Kingdom> GetValidWarDeclarationTargets(Hero sender, Hero recipient);

        /// <summary>Returns kingdoms the sender can ask the recipient to seek an alliance with.</summary>
        public abstract MBReadOnlyList<Kingdom> GetValidAllianceTargets(Hero sender, Hero recipient);

        public abstract int GetCourierFee(Hero sender, Hero recipient);

        public abstract CampaignTime GetMissiveDeliveryTime(Hero sender, Hero recipient);

        public abstract bool IsValidRecipientOfCommand(Hero sender, Hero recipient);
    }
}
