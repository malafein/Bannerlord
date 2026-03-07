using CalradianPostalService.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace CalradianPostalService.Behaviors
{
    class PostalServiceBehavior : CampaignBehaviorBase
    {
        private static Models.PostalServiceModel PostalServiceModel => CalradianPostalServiceSubModule.PostalServiceModel;

        private Hero _recipientSelected;
        private IFaction _joinWarTarget;
        private Kingdom _warDeclarationTarget;
        private Kingdom _allianceTarget;

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

        public static void cps_town_courier_diplomacy_on_init(MenuCallbackArgs args)
        {
            args.MenuTitle = new TextObject("Diplomacy", null);
        }

        public void game_menu_cps_town_courier_diplomacy_on_consequence(MenuCallbackArgs args)
        {
            try
            {
                var contacts = PostalServiceModel.GetValidDiplomacyRecipients(Hero.MainHero);
                if (!contacts.Any())
                {
                    CpsLogger.Info("No eligible diplomatic recipients found.");
                    return;
                }
                var elements = (from c in contacts select new InquiryElement(c.StringId, c.Name.ToString(), new CharacterImageIdentifier(CharacterCode.CreateFrom(c.CharacterObject)))).ToList();

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Select Recipient", "To whom should we deliver this missive?", elements, true, 1, 1,
                    "Continue", "Cancel", OnSelectDiplomacyRecipient, _ => { }));
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "Failed to open diplomatic missive dialog");
            }
        }

        public bool game_menu_cps_town_courier_diplomacy_on_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            return true;
        }

        public void game_menu_cps_town_courier_missive_on_consequence(MenuCallbackArgs args)
        {
            try
            {
                var contacts = PostalServiceModel.GetValidMissiveRecipients(Hero.MainHero);
                if (!contacts.Any())
                {
                    CpsLogger.Info("No eligible missive recipients found.");
                    return;
                }
                var elements = (from c in contacts select new InquiryElement(c.StringId, c.Name.ToString(), new CharacterImageIdentifier(CharacterCode.CreateFrom(c.CharacterObject)))).ToList();

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Select Recipient", "To whom should we deliver this missive?", elements, true, 1, 1,
                    "Continue", "Cancel", OnSelectRecipient, _ => { }));
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "Failed to open missive dialog");
            }
        }

        public bool game_menu_cps_town_courier_missive_on_condition(MenuCallbackArgs args)
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
            InformationManager.ShowTextInquiry(new TextInquiryData("Enter a brief message:", "", true, false, "Send", "Cancel",
                (string s) => { SendMissive<MissiveFriendly>(s); }, null));

            GameMenu.SwitchToMenu("cps_town_courier");
        }

        public void game_menu_cps_town_courier_missive_threat_on_consequence(MenuCallbackArgs args)
        {
            InformationManager.ShowTextInquiry(new TextInquiryData("Let them know how you really feel:", "", true, false, "Send", "Cancel",
                (string s) => { SendMissive<MissiveThreat>(s); }, null));

            GameMenu.SwitchToMenu("cps_town_courier");
        }

        public void game_menu_cps_town_courier_missive_command_on_consequence(MenuCallbackArgs args)
        {
            CpsLogger.Error("This feature has not yet been implemented. Sorry, no refunds..");  // TODO: Remove after commands are implemented.

            InformationManager.ShowTextInquiry(new TextInquiryData("Enter a command:", "", true, false, "Send", "Cancel",
                (string s) => { SendMissive<MissiveCommand>(s); }, null));

            GameMenu.SwitchToMenu("cps_town_courier");
        }

        private void game_menu_cps_town_courier_diplomacy_war_on_consequence(MenuCallbackArgs args)
        {
            try
            {
                var targets = PostalServiceModel.GetValidWarDeclarationTargets(Hero.MainHero, _recipientSelected);
                var elements = (from t in targets
                                select new InquiryElement(t.StringId, t.Name.ToString(),
                                    new BannerImageIdentifier(t.Banner, false))).DefaultIfEmpty().ToList();

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "Select Target Kingdom",
                    $"Against which kingdom should {_recipientSelected.Name} propose war?",
                    elements, true, 1, 1, "Continue", "Cancel",
                    OnSelectWarDeclarationTarget, _ => { }));
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "Failed to show war declaration targets");
            }
        }

        private void game_menu_cps_town_courier_diplomacy_peace_on_consequence(MenuCallbackArgs args)
        {
            SendMissive<MissivePeace>("Let's end this war.");
            GameMenu.SwitchToMenu("cps_town_courier");
        }

        private void game_menu_cps_town_courier_diplomacy_join_war_on_consequence(MenuCallbackArgs args)
        {
            try
            {
                var targets = PostalServiceModel.GetValidJoinWarTargets(Hero.MainHero, _recipientSelected);
                var elements = (from t in targets select new InquiryElement(t.StringId, t.Name.ToString(), new BannerImageIdentifier(t.Banner, false))).DefaultIfEmpty().ToList();

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Select Target",
                    "Against which enemy do we want {CPS_MISSIVE_RECIPIENT} to join us?", elements, true, 1, 1,
                    "Continue", "Cancel", OnSelectWarTarget, _ => { }));
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "Failed to show join-war targets");
            }
        }

        private bool game_menu_cps_town_courier_diplomacy_war_on_condition(MenuCallbackArgs args)
        {
            int courierFee = PostalServiceModel.GetCourierFee(Hero.MainHero, _recipientSelected);
            MBTextManager.SetTextVariable("CPS_AMOUNT", courierFee, false);
            args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
            if (Hero.MainHero.Gold < courierFee)
            {
                args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.", null);
                args.IsEnabled = false;
            }
            else if (Hero.MainHero.Clan?.Leader != Hero.MainHero)
            {
                args.Tooltip = new TextObject("Only clan leaders may send declarations of war.");
                args.IsEnabled = false;
            }
            else if (PostalServiceModel.GetValidWarDeclarationTargets(Hero.MainHero, _recipientSelected).Count == 0)
            {
                args.Tooltip = new TextObject($"There are no valid war targets to propose to {_recipientSelected.Name}.");
                args.IsEnabled = false;
            }

            return true;
        }

        private bool game_menu_cps_town_courier_diplomacy_peace_on_condition(MenuCallbackArgs args)
        {
            int courierFee = PostalServiceModel.GetCourierFee(Hero.MainHero, _recipientSelected);
            MBTextManager.SetTextVariable("CPS_AMOUNT", courierFee, false);
            args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
            if (Hero.MainHero.Gold < courierFee)
            {
                args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.", null);
                args.IsEnabled = false;
            }
            else if (!_recipientSelected.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
            {
                args.Tooltip = new TextObject($"You are already at peace with {_recipientSelected.MapFaction.Name}.");
                args.IsEnabled = false;
            }

            return true;
        }

        private bool game_menu_cps_town_courier_diplomacy_join_war_on_condition(MenuCallbackArgs args)
        {
            int courierFee = PostalServiceModel.GetCourierFee(Hero.MainHero, _recipientSelected);
            MBTextManager.SetTextVariable("CPS_AMOUNT", courierFee, false);
            args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
            if (Hero.MainHero.Gold < courierFee)
            {
                args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.", null);
                args.IsEnabled = false;
            }
            else if (_recipientSelected.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
            {
                args.Tooltip = new TextObject($"You're at war with {_recipientSelected.MapFaction.Name}. Make peace with them before you ask for this.");
                args.IsEnabled = false;
            }

            return true;
        }

        private void SendMissive<T>(string missiveText, Dictionary<object, object> args = null) where T : IMissive, new()
        {
            try
            {
                CpsLogger.Info($"Sending missive to {_recipientSelected}: {missiveText}");
                var missive = new T
                {
                    Sender = Hero.MainHero,
                    Recipient = _recipientSelected,
                    CampaignTimeSent = CampaignTime.Now,
                    CampaignTimeArrival = PostalServiceModel.GetMissiveDeliveryTime(Hero.MainHero, _recipientSelected),
                    Text = missiveText,
                    Args = args
                };

                _missives.Add(missive);
                missive.OnSend();
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "SendMissive failed");
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
                CpsLogger.Error(ex, "OnSelectRecipient failed");
            }
        }

        private void game_menu_cps_town_courier_diplomacy_alliance_on_consequence(MenuCallbackArgs args)
        {
            try
            {
                var targets = PostalServiceModel.GetValidAllianceTargets(Hero.MainHero, _recipientSelected);
                var elements = (from t in targets
                                select new InquiryElement(t.StringId, t.Name.ToString(),
                                    new BannerImageIdentifier(t.Banner, false))).DefaultIfEmpty().ToList();

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "Select Alliance Target",
                    $"With which kingdom should {_recipientSelected.Name} propose an alliance?",
                    elements, true, 1, 1, "Continue", "Cancel",
                    OnSelectAllianceTarget, _ => { }));
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "Failed to show alliance targets");
            }
        }

        private bool game_menu_cps_town_courier_diplomacy_alliance_on_condition(MenuCallbackArgs args)
        {
            int courierFee = PostalServiceModel.GetCourierFee(Hero.MainHero, _recipientSelected);
            MBTextManager.SetTextVariable("CPS_AMOUNT", courierFee, false);
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            if (Hero.MainHero.Gold < courierFee)
            {
                args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.", null);
                args.IsEnabled = false;
            }
            else if (_recipientSelected.Clan?.Kingdom == null)
            {
                args.Tooltip = new TextObject($"{_recipientSelected.Name} is not part of a kingdom and cannot seek an alliance.");
                args.IsEnabled = false;
            }
            else if (PostalServiceModel.GetValidAllianceTargets(Hero.MainHero, _recipientSelected).Count == 0)
            {
                args.Tooltip = new TextObject($"There are no valid alliance targets for {_recipientSelected.Name}.");
                args.IsEnabled = false;
            }

            return true;
        }

        private void OnSelectWarTarget(List<InquiryElement> targets)
        {
            try
            {
                var element = targets.First<InquiryElement>();
                _joinWarTarget = (from f in Campaign.Current.Factions where f.StringId == element.Identifier.ToString() select f).First();
                SendMissive<MissiveJoinWar>($"Will you join me in war against {_joinWarTarget.Name}?",
                    new Dictionary<object, object> { { MissiveJoinWar.Arg.TargetKingdomId, _joinWarTarget.StringId } });
                GameMenu.SwitchToMenu("cps_town_courier");
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "OnSelectWarTarget failed");
            }
        }

        private void OnSelectWarDeclarationTarget(List<InquiryElement> targets)
        {
            try
            {
                var element = targets.First();
                _warDeclarationTarget = Kingdom.All.First(k => k.StringId == element.Identifier.ToString());
                SendMissive<MissiveWar>(
                    $"I call upon you to declare war against {_warDeclarationTarget.Name}.",
                    new Dictionary<object, object> { { MissiveWar.Arg.TargetKingdomId, _warDeclarationTarget.StringId } });
                GameMenu.SwitchToMenu("cps_town_courier");
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "OnSelectWarDeclarationTarget failed");
            }
        }

        private void OnSelectAllianceTarget(List<InquiryElement> targets)
        {
            try
            {
                var element = targets.First();
                _allianceTarget = Kingdom.All.First(k => k.StringId == element.Identifier.ToString());
                SendMissive<MissiveAlliance>(
                    $"I urge you to seek an alliance with {_allianceTarget.Name}.",
                    new Dictionary<object, object> { { MissiveAlliance.Arg.TargetKingdomId, _allianceTarget.StringId } });
                GameMenu.SwitchToMenu("cps_town_courier");
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "OnSelectAllianceTarget failed");
            }
        }

        private void OnSelectDiplomacyRecipient(List<InquiryElement> recipients)
        {
            try
            {
                var element = recipients.First<InquiryElement>();
                Hero recipient = Hero.FindFirst((Hero h) => { return h.StringId == element.Identifier.ToString(); });
                MBTextManager.SetTextVariable("CPS_MISSIVE_RECIPIENT", recipient.Name);
                _recipientSelected = recipient;
                GameMenu.SwitchToMenu("cps_town_courier_diplomacy");
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "OnSelectDiplomacyRecipient failed");
            }
        }

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            CpsLogger.Debug("Registering courier menus.");

            campaignGameStarter.AddGameMenuOption("town", "cps_town_courier", "Find a courier", new GameMenuOption.OnConditionDelegate(game_menu_town_find_courier_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_town_find_courier_on_consequence), false, 4, false);

            campaignGameStarter.AddGameMenu("cps_town_courier", "You find a Calradian Postal Service agent.  They can send a missive by raven for a small fee.", new OnInitDelegate(cps_town_courier_on_init), 0, GameMenu.MenuFlags.None, null);
            campaignGameStarter.AddGameMenuOption("cps_town_courier", "cps_town_courier_diplomacy", "Send diplomatic missive", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_diplomacy_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_diplomacy_on_consequence), false, -1, false);
            campaignGameStarter.AddGameMenuOption("cps_town_courier", "cps_town_courier_missive", "Send a personal letter", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_on_consequence), false, -1, false);
            campaignGameStarter.AddGameMenuOption("cps_town_courier", "cps_town_courier_back", "{=qWAmxyYz}Back to town center", new GameMenuOption.OnConditionDelegate(back_on_condition), (MenuCallbackArgs x) => GameMenu.SwitchToMenu("town"), true, -1, false);

            campaignGameStarter.AddGameMenu("cps_town_courier_missive", "With {CPS_MISSIVE_RECIPIENT} declared as the recipient, the agent is now ready to accept your missive.", new OnInitDelegate(cps_town_courier_missive_on_init), 0, GameMenu.MenuFlags.None, null);
            if (ModuleConfiguration.Instance.EnableFriendlyMissives)
                campaignGameStarter.AddGameMenuOption("cps_town_courier_missive", "cps_town_courier_missive_friendly", "Pay a {CPS_AMOUNT}{GOLD_ICON} fee to send a friendly missive", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_simple_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_friendly_on_consequence), false, -1, false);
            if (ModuleConfiguration.Instance.EnableThreateningMissives)
                campaignGameStarter.AddGameMenuOption("cps_town_courier_missive", "cps_town_courier_missive_threat", "Pay a {CPS_AMOUNT}{GOLD_ICON} fee to send a threatening missive", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_simple_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_threat_on_consequence), false, -1, false);
            if (ModuleConfiguration.Instance.EnableCommandMissives)  // TODO: Command Missive should move out of "personal letter" submenu
                campaignGameStarter.AddGameMenuOption("cps_town_courier_missive", "cps_town_courier_missive_command", "Pay a {CPS_AMOUNT}{GOLD_ICON} fee to send a missive containing orders", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_command_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_command_on_consequence), false, -1, false);
            campaignGameStarter.AddGameMenuOption("cps_town_courier_missive", "cps_town_courier_missive_back", "{=qWAmxyYz}Back to town center", new GameMenuOption.OnConditionDelegate(back_on_condition), (MenuCallbackArgs x) => GameMenu.SwitchToMenu("town"), true, -1, false);

            campaignGameStarter.AddGameMenu("cps_town_courier_diplomacy", "With {CPS_MISSIVE_RECIPIENT} declared as the recipient, the agent is now ready to accept your missive. The fee for this service will be {CPS_AMOUNT}{GOLD_ICON}.", new OnInitDelegate(cps_town_courier_diplomacy_on_init), 0, GameMenu.MenuFlags.None, null);
            if (ModuleConfiguration.Instance.EnableDeclareWarMissives)
                campaignGameStarter.AddGameMenuOption("cps_town_courier_diplomacy", "cps_town_courier_diplomacy_war", "Send a declaration of war.", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_diplomacy_war_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_diplomacy_war_on_consequence), false, -1, false);
            if (ModuleConfiguration.Instance.EnablePeaceMissives)
                campaignGameStarter.AddGameMenuOption("cps_town_courier_diplomacy", "cps_town_courier_diplomacy_peace", "Send an offer for peace.", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_diplomacy_peace_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_diplomacy_peace_on_consequence), false, -1, false);
            if (ModuleConfiguration.Instance.EnableRequestWarMissives)
                campaignGameStarter.AddGameMenuOption("cps_town_courier_diplomacy", "cps_town_courier_diplomacy_join_war", "Request {CPS_MISSIVE_RECIPIENT} to join in your war.", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_diplomacy_join_war_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_diplomacy_join_war_on_consequence), false, -1, false);
            if (ModuleConfiguration.Instance.EnableAllianceMissives)
                campaignGameStarter.AddGameMenuOption("cps_town_courier_diplomacy", "cps_town_courier_diplomacy_alliance", "Request {CPS_MISSIVE_RECIPIENT} to seek an alliance.", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_diplomacy_alliance_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_diplomacy_alliance_on_consequence), false, -1, false);
            campaignGameStarter.AddGameMenuOption("cps_town_courier_diplomacy", "cps_town_courier_diplomacy_back", "{=qWAmxyYz}Back to town center", new GameMenuOption.OnConditionDelegate(back_on_condition), (MenuCallbackArgs x) => GameMenu.SwitchToMenu("town"), true, -1, false);

            CpsLogger.Debug("Courier menus registered.");
        }

        private void OnDailyTick()
        {
            try
            {
                CpsLogger.Debug($"{_missives.Count} missives out for delivery.");
                for (int i = _missives.Count - 1; i >= 0; --i)
                {
                    if (_missives[i].CampaignTimeArrival <= CampaignTime.Now)
                    {
                        _missives[i].OnDelivery();
                        CpsLogger.Log($"Missive delivered from {_missives[i].Sender.Name} to {_missives[i].Recipient.Name}: {_missives[i].Text}");
                        _missives.RemoveAt(i);
                    }
                }
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "OnDailyTick failed");
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(this.OnDailyTick));
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                JsonSerializerSettings settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

                if (dataStore.IsSaving)
                {
                    List<MissiveSyncData> sync = (from m in _missives select new MissiveSyncData(m)).ToList();
                    _missiveSyncData = JsonConvert.SerializeObject(sync, settings);
                    dataStore.SyncData("_missiveSyncData", ref _missiveSyncData);
                    CpsLogger.Debug($"Saved {sync.Count} missives.");
                }
                else if (dataStore.IsLoading)
                {
                    dataStore.SyncData("_missiveSyncData", ref _missiveSyncData);
                    List<MissiveSyncData> sync = JsonConvert.DeserializeObject(_missiveSyncData, settings) as List<MissiveSyncData>;
                    _missives = (from m in sync where m.TypeName == "MissiveFriendly" select new MissiveFriendly(m)).ToList<IMissive>();
                    _missives.AddRange((from m in sync where m.TypeName == "MissiveThreat" select new MissiveThreat(m)));
                    _missives.AddRange((from m in sync where m.TypeName == "MissiveCommand" select new MissiveCommand(m)));
                    CpsLogger.Debug($"Loaded {_missives.Count} missives.");
                }
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "SyncData failed");
            }
        }
    }
}
