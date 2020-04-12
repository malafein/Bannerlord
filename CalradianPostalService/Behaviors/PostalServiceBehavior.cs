using CalradianPostalService.Models;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

using CPSModule = CalradianPostalService.CalradianPostalServiceSubModule;

namespace CalradianPostalService.Behaviors
{
    class PostalServiceBehavior : CampaignBehaviorBase
    {
        private static Models.PostalServiceModel PostalServiceModel => CPSModule.PostalServiceModel;
        private static readonly ILog log = LogManager.GetLogger(typeof(PostalServiceBehavior));

        private Hero _recipientSelected;

        private List<IMissive> _missives = new List<IMissive>();
        private string _missiveSyncData;

        private static bool back_on_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        public static bool game_menu_town_find_courier_on_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return true;
        }

        public static void game_menu_town_find_courier_on_consequence(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu("cps_town_courier");
        }

        public static void cps_town_courier_on_init(MenuCallbackArgs args)
        {
            args.MenuTitle = new TextObject("Courier", null);
        }

        public static void cps_town_courier_missive_on_init(MenuCallbackArgs args)
        {
            args.MenuTitle = new TextObject("Courier", null);
        }

        public void game_menu_cps_town_courier_missive_on_consequence(MenuCallbackArgs args)
        {
            var contacts = PostalServiceModel.GetValidMissiveRecipients(Hero.MainHero);
            var elements = (from c in contacts select new InquiryElement(c.StringId, c.Name.ToString(), new ImageIdentifier(CharacterCode.CreateFrom(c.CharacterObject)))).DefaultIfEmpty().ToList();

            InformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Select Recipient", "To whom should we deliver this missive?", elements, true, true,
                "Continue", "Cancel", OnSelectRecipient, (List<InquiryElement> r) => { }));
        }

        public bool game_menu_cps_town_courier_missive_select_recipient_on_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            return true;
        }

        public bool game_menu_cps_town_courier_missive_simple_on_condition(MenuCallbackArgs args)
        {
            int courierFee = PostalServiceModel.GetCourierFee(Hero.MainHero, _recipientSelected);
            MBTextManager.SetTextVariable("CPS_AMOUNT", courierFee, false);
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            if (Hero.MainHero.Gold < courierFee)
            {
                args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.", null);
                args.IsEnabled = false;
            }

            return true;
        }

        public bool game_menu_cps_town_courier_missive_command_on_condition(MenuCallbackArgs args)
        {
            int courierFee = PostalServiceModel.GetCourierFee(Hero.MainHero, _recipientSelected);
            MBTextManager.SetTextVariable("CPS_AMOUNT", courierFee, false);
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            if (Hero.MainHero.Gold < courierFee)
            {
                args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.", null);
                args.IsEnabled = false;
            }
            else if (!PostalServiceModel.IsValidRecipientOfCommand(Hero.MainHero, _recipientSelected))
            {
                args.Tooltip = new TextObject("You cannot issue commands to this recipient.", null);
                args.IsEnabled = false;
            }
            else // TODO: remove after feature implemented
            {
                args.Tooltip = new TextObject("This feature hasn't been implemented yet.", null);
                args.IsEnabled = false;
            }

            return true;
        }

        public void game_menu_cps_town_courier_missive_friendly_on_consequence(MenuCallbackArgs args)
        {
            // create and send the missive
            CPSModule.DebugMessage("Friendly Missive selected.", log);

            InformationManager.ShowTextInquiry(new TextInquiryData("Enter a brief message:", "", true, false, "Send", "Cancel",
                (string s) => { SendMissive<MissiveFriendly>(s); }, null));

            GameMenu.SwitchToMenu("cps_town_courier");
        }

        public void game_menu_cps_town_courier_missive_threat_on_consequence(MenuCallbackArgs args)
        {
            // create and send the missive
            CPSModule.DebugMessage("Threatening Missive selected.", log);

            InformationManager.ShowTextInquiry(new TextInquiryData("Let them know how you really feel:", "", true, false, "Send", "Cancel",
                (string s) => { SendMissive<MissiveThreat>(s); }, null));

            GameMenu.SwitchToMenu("cps_town_courier");
        }
        public void game_menu_cps_town_courier_missive_command_on_consequence(MenuCallbackArgs args)
        {
            // create and send the missive
            CPSModule.ErrorMessage("This feature has not yet been implmented.  Sorry, no refunds..");  // TODO: Remove after commands are implemented.

            InformationManager.ShowTextInquiry(new TextInquiryData("Enter a command:", "", true, false, "Send", "Cancel",
                (string s) => { SendMissive<MissiveCommand>(s); }, null));

            GameMenu.SwitchToMenu("cps_town_courier");
        }

        private void SendMissive<T>(string s) where T : IMissive, new()
        {
            try
            {
                CPSModule.DebugMessage($"You entered: {s}", log);
                var missive = new T
                {
                    Sender = Hero.MainHero,
                    Recipient = _recipientSelected,
                    CampaignTimeSent = CampaignTime.Now,
                    CampaignTimeArrival = PostalServiceModel.GetMissiveDeliveryTime(Hero.MainHero, _recipientSelected),
                    Text = s
                };

                _missives.Add(missive);

                missive.OnSend();
            }
            catch (Exception ex)
            {
                CPSModule.DebugMessage(ex, log);
            }
        }

        private void OnSelectRecipient(List<InquiryElement> recipients)
        {
            try
            {
                var element = recipients.First<InquiryElement>();
                Hero recipient = Hero.FindFirst((Hero h) => { return h.StringId == element.Identifier.ToString(); });
                MBTextManager.SetTextVariable("CPS_MISSIVE_RECIPIENT", recipient.Name);
                _recipientSelected = recipient;
                GameMenu.SwitchToMenu("cps_town_courier_missive");
            }
            catch (Exception ex)
            {
                CPSModule.DebugMessage(ex, log);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            campaignGameStarter.AddGameMenuOption("town", "cps_town_courier", "Find a courier", new GameMenuOption.OnConditionDelegate(game_menu_town_find_courier_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_town_find_courier_on_consequence), false, 4, false);
            
            campaignGameStarter.AddGameMenu("cps_town_courier", "You find a Calradian Postal Service agent.  They can send a missive by raven for a small fee.", new OnInitDelegate(cps_town_courier_on_init), 0, GameMenu.MenuFlags.none, null);
            campaignGameStarter.AddGameMenuOption("cps_town_courier", "cps_town_courier_missive", "Choose a recipient", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_select_recipient_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_on_consequence), false, -1, false);
            campaignGameStarter.AddGameMenuOption("cps_town_courier", "cps_town_courier_back", "{=qWAmxyYz}Back to town center", new GameMenuOption.OnConditionDelegate(back_on_condition), (MenuCallbackArgs x) => GameMenu.SwitchToMenu("town"), true, -1, false);
            
            campaignGameStarter.AddGameMenu("cps_town_courier_missive", "With {CPS_MISSIVE_RECIPIENT} declared as the recipient, the agent is now ready to accept your missive.", new OnInitDelegate(cps_town_courier_missive_on_init), 0, GameMenu.MenuFlags.none, null);
            campaignGameStarter.AddGameMenuOption("cps_town_courier_missive", "cps_town_courier_missive_friendly", "Pay a {CPS_AMOUNT}{GOLD_ICON} fee to send a friendly missive", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_simple_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_friendly_on_consequence), false, -1, false);
            campaignGameStarter.AddGameMenuOption("cps_town_courier_missive", "cps_town_courier_missive_threat", "Pay a {CPS_AMOUNT}{GOLD_ICON} fee to send a threatening missive", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_simple_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_threat_on_consequence), false, -1, false);
            campaignGameStarter.AddGameMenuOption("cps_town_courier_missive", "cps_town_courier_missive_command", "Pay a {CPS_AMOUNT}{GOLD_ICON} fee to send a missive containing orders", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_command_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_command_on_consequence), false, -1, false);
            campaignGameStarter.AddGameMenuOption("cps_town_courier_missive", "cps_town_courier_missive_back", "{=qWAmxyYz}Back to town center", new GameMenuOption.OnConditionDelegate(back_on_condition), (MenuCallbackArgs x) => GameMenu.SwitchToMenu("town"), true, -1, false);

        }
        private void OnDailyTick()
        {
            try
            {
                CPSModule.DebugMessage($"{_missives.Count} missives out for delivery..", log);
                for (int i = _missives.Count - 1; i >= 0; --i)
                {
                    if (_missives[i].CampaignTimeArrival <= CampaignTime.Now)
                    {
                        // missive delivered
                        _missives[i].OnDelivery();
                        CPSModule.DebugMessage($"missive delivered from {_missives[i].Sender.Name} to {_missives[i].Recipient.Name}: {_missives[i].Text}", log);
                        _missives.RemoveAt(i);
                    }
                }
            }
            catch (Exception ex)
            {
                CPSModule.DebugMessage(ex, log);
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(this.OnDailyTick));
        }

        public override void SyncData(IDataStore dataStore)
        {
            var serializer = new JsonSerializer();

            if (dataStore.IsLoading)
            {
                dataStore.SyncData("_missiveSyncData", ref _missiveSyncData);
                _missives = JsonConvert.DeserializeObject(_missiveSyncData) as List<IMissive>;
            }
            else if (dataStore.IsSaving)
            {
                _missiveSyncData = JsonConvert.SerializeObject(_missives);
                dataStore.SyncData("_missiveSyncData", ref _missiveSyncData);
            }
        }
    }
}
