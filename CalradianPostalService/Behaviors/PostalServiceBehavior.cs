using CalradianPostalService.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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
        private IFaction _peaceTarget;
        private Kingdom _warDeclarationTarget;
        private Kingdom _allianceTarget;

        private List<IMissive> _missives = new List<IMissive>();
        private string _missiveSyncData;

        // Per-recipient cooldown for personal missives (friendly/threat)
        private Dictionary<string, CampaignTime> _personalMissiveCooldowns = new Dictionary<string, CampaignTime>();
        private string _personalMissiveCooldownData;

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
                var elements = (from c in contacts select new InquiryElement(c.StringId, FormatRecipientLabel(c), new CharacterImageIdentifier(CharacterCode.CreateFrom(c.CharacterObject)))).ToList();

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
                var elements = (from c in contacts select new InquiryElement(c.StringId, FormatRecipientLabel(c), new CharacterImageIdentifier(CharacterCode.CreateFrom(c.CharacterObject)))).ToList();

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
            int fee = PostalServiceModel.GetPersonalMissiveFee(Hero.MainHero, _recipientSelected);
            MBTextManager.SetTextVariable("CPS_COURIER_FEE", new TextObject(fee.ToString()));
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;

            if (_recipientSelected != null
                && _personalMissiveCooldowns.TryGetValue(_recipientSelected.StringId, out CampaignTime cooldownEnd)
                && CampaignTime.Now < cooldownEnd)
            {
                float daysLeft = (float)(cooldownEnd - CampaignTime.Now).ToDays;
                args.Tooltip = new TextObject($"You have already written to {_recipientSelected.Name} recently. Wait {daysLeft:F0} more day(s).");
                args.IsEnabled = false;
            }
            else if (Hero.MainHero.Gold < fee)
            {
                args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.", null);
                args.IsEnabled = false;
            }

            return true;
        }

        public bool game_menu_cps_town_courier_missive_command_on_condition(MenuCallbackArgs args)
        {
            int courierFee = PostalServiceModel.GetCourierFee(Hero.MainHero, _recipientSelected);
            MBTextManager.SetTextVariable("CPS_COURIER_FEE", new TextObject(courierFee.ToString()));
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
            if (!CanSendPersonalMissive()) return;
            SendMissive<MissiveFriendly>("");
            RecordPersonalMissiveCooldown(_recipientSelected);
            GameMenu.SwitchToMenu("cps_town_courier");
        }

        public void game_menu_cps_town_courier_missive_threat_on_consequence(MenuCallbackArgs args)
        {
            if (!CanSendPersonalMissive()) return;
            SendMissive<MissiveThreat>("");
            RecordPersonalMissiveCooldown(_recipientSelected);
            GameMenu.SwitchToMenu("cps_town_courier");
        }

        private bool CanSendPersonalMissive()
        {
            int fee = PostalServiceModel.GetPersonalMissiveFee(Hero.MainHero, _recipientSelected);
            if (Hero.MainHero.Gold < fee) return false;
            if (_recipientSelected != null
                && _personalMissiveCooldowns.TryGetValue(_recipientSelected.StringId, out CampaignTime cooldownEnd)
                && CampaignTime.Now < cooldownEnd) return false;
            return true;
        }

        private void RecordPersonalMissiveCooldown(Hero recipient)
        {
            if (recipient == null) return;
            int cooldownDays = ModuleConfiguration.Instance.PostalService.PersonalMissiveCooldownDays;
            _personalMissiveCooldowns[recipient.StringId] = CampaignTime.DaysFromNow(cooldownDays);
            CpsLogger.Verbose($"[Cooldown] {recipient.Name} cooldown set, expires in {cooldownDays} day(s).");
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
            try
            {
                var targets = PostalServiceModel.GetValidPeaceTargets(Hero.MainHero, _recipientSelected);
                var elements = (from t in targets
                                select new InquiryElement(t.StringId, t.Name.ToString(),
                                    new BannerImageIdentifier(t.Banner, false))).DefaultIfEmpty().ToList();

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "Select Target Faction",
                    $"With which faction should {_recipientSelected.Name} seek peace?",
                    elements, true, 1, 1, "Continue", "Cancel",
                    OnSelectPeaceTarget, _ => { }));
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "Failed to show peace targets");
            }
        }

        private void OnSelectPeaceTarget(List<InquiryElement> targets)
        {
            try
            {
                var element = targets.First();
                _peaceTarget = Campaign.Current.Factions.First(f => f.StringId == element.Identifier.ToString());
                SendMissive<MissivePeace>(
                    $"I urge you to seek peace with {_peaceTarget.Name}.",
                    new Dictionary<object, object> { { MissivePeace.Arg.TargetFactionId, _peaceTarget.StringId } });
                GameMenu.SwitchToMenu("cps_town_courier");
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "OnSelectPeaceTarget failed");
            }
        }

        private void game_menu_cps_town_courier_diplomacy_join_war_on_consequence(MenuCallbackArgs args)
        {
            try
            {
                var targets = PostalServiceModel.GetValidJoinWarTargets(Hero.MainHero, _recipientSelected);
                var elements = (from t in targets select new InquiryElement(t.StringId, t.Name.ToString(), new BannerImageIdentifier(t.Banner, false))).DefaultIfEmpty().ToList();

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Select Target",
                    $"Against which enemy do we want {_recipientSelected.Name} to join us?", elements, true, 1, 1,
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
            MBTextManager.SetTextVariable("CPS_COURIER_FEE", new TextObject(courierFee.ToString()));
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
            MBTextManager.SetTextVariable("CPS_COURIER_FEE", new TextObject(courierFee.ToString()));
            args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
            if (Hero.MainHero.Gold < courierFee)
            {
                args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.", null);
                args.IsEnabled = false;
            }
            else if (PostalServiceModel.GetValidPeaceTargets(Hero.MainHero, _recipientSelected).Count == 0)
            {
                args.Tooltip = new TextObject($"{_recipientSelected.Name} is not currently at war with anyone.");
                args.IsEnabled = false;
            }

            return true;
        }

        private bool game_menu_cps_town_courier_diplomacy_join_war_on_condition(MenuCallbackArgs args)
        {
            int courierFee = PostalServiceModel.GetCourierFee(Hero.MainHero, _recipientSelected);
            MBTextManager.SetTextVariable("CPS_COURIER_FEE", new TextObject(courierFee.ToString()));
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
                var arrivalTime = PostalServiceModel.GetMissiveDeliveryTime(Hero.MainHero, _recipientSelected);
                CpsLogger.Info($"[{typeof(T).Name}] Missive sent to {_recipientSelected.Name}. Arrives in {(arrivalTime - CampaignTime.Now).ToDays:F1} day(s).");
                var missive = new T
                {
                    Sender = Hero.MainHero,
                    Recipient = _recipientSelected,
                    CampaignTimeSent = CampaignTime.Now,
                    CampaignTimeArrival = arrivalTime,
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
                _recipientSelected = recipient;
                MBTextManager.SetTextVariable("CPS_MISSIVE_RECIPIENT", recipient.Name);
                MBTextManager.SetTextVariable("CPS_COURIER_FEE", new TextObject(PostalServiceModel.GetPersonalMissiveFee(Hero.MainHero, recipient).ToString()));
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
            MBTextManager.SetTextVariable("CPS_COURIER_FEE", new TextObject(courierFee.ToString()));
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
                _recipientSelected = recipient;
                MBTextManager.SetTextVariable("CPS_MISSIVE_RECIPIENT", recipient.Name);
                MBTextManager.SetTextVariable("CPS_COURIER_FEE", new TextObject(PostalServiceModel.GetCourierFee(Hero.MainHero, recipient).ToString()));
                GameMenu.SwitchToMenu("cps_town_courier_diplomacy");
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "OnSelectDiplomacyRecipient failed");
            }
        }

        private static string FormatTraitValue(int v) => v >= 0 ? $"+{v}" : v.ToString();

        private static string FormatRecipientLabel(Hero hero)
        {
            int gen = hero.GetTraitLevel(DefaultTraits.Generosity);
            int val = hero.GetTraitLevel(DefaultTraits.Valor);
            int hon = hero.GetTraitLevel(DefaultTraits.Honor);
            int iro = hero.GetTraitLevel(DefaultTraits.PersonaIronic);
            int rel = hero.GetRelation(Hero.MainHero);

            var parts = new List<string>();
            if (gen != 0) parts.Add($"G{FormatTraitValue(gen)}");
            if (val != 0) parts.Add($"V{FormatTraitValue(val)}");
            if (hon != 0) parts.Add($"H{FormatTraitValue(hon)}");
            if (iro != 0) parts.Add($"I{FormatTraitValue(iro)}");
            parts.Add($"R{FormatTraitValue(rel)}");

            return $"{hero.Name}\n{string.Join("  ", parts)}";
        }

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            CpsLogger.Verbose("Registering courier menus.");

            campaignGameStarter.AddGameMenuOption("town", "cps_town_courier", "Find a courier", new GameMenuOption.OnConditionDelegate(game_menu_town_find_courier_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_town_find_courier_on_consequence), false, 4, false);

            campaignGameStarter.AddGameMenu("cps_town_courier", "You find a Calradian Postal Service agent.  They can send a missive by raven for a small fee.", new OnInitDelegate(cps_town_courier_on_init), 0, GameMenu.MenuFlags.None, null);
            campaignGameStarter.AddGameMenuOption("cps_town_courier", "cps_town_courier_diplomacy", "Send diplomatic missive", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_diplomacy_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_diplomacy_on_consequence), false, -1, false);
            campaignGameStarter.AddGameMenuOption("cps_town_courier", "cps_town_courier_missive", "Send a personal letter", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_on_consequence), false, -1, false);
            campaignGameStarter.AddGameMenuOption("cps_town_courier", "cps_town_courier_back", "{=qWAmxyYz}Back to town center", new GameMenuOption.OnConditionDelegate(back_on_condition), (MenuCallbackArgs x) => GameMenu.SwitchToMenu("town"), true, -1, false);

            campaignGameStarter.AddGameMenu("cps_town_courier_missive", "With {CPS_MISSIVE_RECIPIENT} declared as the recipient, the agent is now ready to accept your missive. The fee for this service will be {CPS_COURIER_FEE}{GOLD_ICON}.", new OnInitDelegate(cps_town_courier_missive_on_init), 0, GameMenu.MenuFlags.None, null);
            if (ModuleConfiguration.Instance.EnableFriendlyMissives)
                campaignGameStarter.AddGameMenuOption("cps_town_courier_missive", "cps_town_courier_missive_friendly", "Pay a {CPS_COURIER_FEE}{GOLD_ICON} fee to send a friendly missive", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_simple_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_friendly_on_consequence), false, -1, false);
            if (ModuleConfiguration.Instance.EnableThreateningMissives)
                campaignGameStarter.AddGameMenuOption("cps_town_courier_missive", "cps_town_courier_missive_threat", "Pay a {CPS_COURIER_FEE}{GOLD_ICON} fee to send a threatening missive", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_simple_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_threat_on_consequence), false, -1, false);
            if (ModuleConfiguration.Instance.EnableCommandMissives)  // TODO: Command Missive should move out of "personal letter" submenu
                campaignGameStarter.AddGameMenuOption("cps_town_courier_missive", "cps_town_courier_missive_command", "Pay a {CPS_COURIER_FEE}{GOLD_ICON} fee to send a missive containing orders", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_command_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_command_on_consequence), false, -1, false);
            campaignGameStarter.AddGameMenuOption("cps_town_courier_missive", "cps_town_courier_missive_back", "{=qWAmxyYz}Back to town center", new GameMenuOption.OnConditionDelegate(back_on_condition), (MenuCallbackArgs x) => GameMenu.SwitchToMenu("town"), true, -1, false);

            campaignGameStarter.AddGameMenu("cps_town_courier_diplomacy", "With {CPS_MISSIVE_RECIPIENT} declared as the recipient, the agent is now ready to accept your missive. The fee for this service will be {CPS_COURIER_FEE}{GOLD_ICON}.", new OnInitDelegate(cps_town_courier_diplomacy_on_init), 0, GameMenu.MenuFlags.None, null);
            if (ModuleConfiguration.Instance.EnableDeclareWarMissives)
                campaignGameStarter.AddGameMenuOption("cps_town_courier_diplomacy", "cps_town_courier_diplomacy_war", "Request {CPS_MISSIVE_RECIPIENT} to declare war.", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_diplomacy_war_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_diplomacy_war_on_consequence), false, -1, false);
            if (ModuleConfiguration.Instance.EnablePeaceMissives)
                campaignGameStarter.AddGameMenuOption("cps_town_courier_diplomacy", "cps_town_courier_diplomacy_peace", "Request {CPS_MISSIVE_RECIPIENT} to make peace.", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_diplomacy_peace_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_diplomacy_peace_on_consequence), false, -1, false);
            if (ModuleConfiguration.Instance.EnableRequestWarMissives)
                campaignGameStarter.AddGameMenuOption("cps_town_courier_diplomacy", "cps_town_courier_diplomacy_join_war", "Request {CPS_MISSIVE_RECIPIENT} to join in your war.", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_diplomacy_join_war_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_diplomacy_join_war_on_consequence), false, -1, false);
            if (ModuleConfiguration.Instance.EnableAllianceMissives)
                campaignGameStarter.AddGameMenuOption("cps_town_courier_diplomacy", "cps_town_courier_diplomacy_alliance", "Request {CPS_MISSIVE_RECIPIENT} to seek an alliance.", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_diplomacy_alliance_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_diplomacy_alliance_on_consequence), false, -1, false);
            campaignGameStarter.AddGameMenuOption("cps_town_courier_diplomacy", "cps_town_courier_diplomacy_back", "{=qWAmxyYz}Back to town center", new GameMenuOption.OnConditionDelegate(back_on_condition), (MenuCallbackArgs x) => GameMenu.SwitchToMenu("town"), true, -1, false);

            CpsLogger.Verbose("Courier menus registered.");
        }

        private void OnDailyTick()
        {
            try
            {
                CpsLogger.Verbose($"{_missives.Count} missives out for delivery.");
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

                    // Store cooldowns as remaining days (double) to avoid CampaignTime deserialization issues.
                    // Expired cooldowns are omitted — they would allow sending anyway.
                    var cooldownsRemaining = new Dictionary<string, double>();
                    foreach (var kvp in _personalMissiveCooldowns)
                    {
                        double remaining = (kvp.Value - CampaignTime.Now).ToDays;
                        if (remaining > 0) cooldownsRemaining[kvp.Key] = remaining;
                    }
                    _personalMissiveCooldownData = JsonConvert.SerializeObject(cooldownsRemaining);
                    dataStore.SyncData("_personalMissiveCooldownData", ref _personalMissiveCooldownData);

                    CpsLogger.Verbose($"Saved {sync.Count} missives, {cooldownsRemaining.Count} cooldowns.");
                }
                else if (dataStore.IsLoading)
                {
                    dataStore.SyncData("_missiveSyncData", ref _missiveSyncData);
                    List<MissiveSyncData> sync = (!string.IsNullOrEmpty(_missiveSyncData)
                        ? JsonConvert.DeserializeObject(_missiveSyncData, settings) as List<MissiveSyncData>
                        : null) ?? new List<MissiveSyncData>();
                    _missives = (from m in sync where m.TypeName == "MissiveFriendly"  select new MissiveFriendly(m)).ToList<IMissive>();
                    _missives.AddRange(from m in sync where m.TypeName == "MissiveThreat"   select new MissiveThreat(m));
                    _missives.AddRange(from m in sync where m.TypeName == "MissiveCommand"  select new MissiveCommand(m));
                    _missives.AddRange(from m in sync where m.TypeName == "MissiveWar"      select new MissiveWar(m));
                    _missives.AddRange(from m in sync where m.TypeName == "MissivePeace"    select new MissivePeace(m));
                    _missives.AddRange(from m in sync where m.TypeName == "MissiveJoinWar"  select new MissiveJoinWar(m));
                    _missives.AddRange(from m in sync where m.TypeName == "MissiveAlliance" select new MissiveAlliance(m));

                    dataStore.SyncData("_personalMissiveCooldownData", ref _personalMissiveCooldownData);
                    if (!string.IsNullOrEmpty(_personalMissiveCooldownData))
                    {
                        var cooldownsRemaining = JsonConvert.DeserializeObject<Dictionary<string, double>>(_personalMissiveCooldownData)
                            ?? new Dictionary<string, double>();
                        _personalMissiveCooldowns = cooldownsRemaining.ToDictionary(
                            kvp => kvp.Key,
                            kvp => CampaignTime.DaysFromNow((float)kvp.Value));
                    }

                    CpsLogger.Verbose($"Loaded {_missives.Count} missives, {_personalMissiveCooldowns.Count} cooldowns.");
                }
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "SyncData failed");
            }
        }
    }
}
