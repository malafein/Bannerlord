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

        public abstract MBReadOnlyList<Hero> GetValidMissiveRecipients(Hero hero);

        public abstract int GetCourierFee(Hero sender, Hero recipient);

        public abstract CampaignTime GetMissiveDeliveryTime(Hero sender, Hero recipient);

        public abstract bool IsValidRecipientOfCommand(Hero sender, Hero recipient);
    }
}
