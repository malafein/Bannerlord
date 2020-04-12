using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors.Towns;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CalradianPostalService
{
    [HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), "AddGameMenus")]
    public class PlayerTownVisitCampaignBehaviorPatchAddGameMenus
    {
        private static Models.PostalServiceModel PostalServiceModel => CalradianPostalServiceSubModule.PostalServiceModel;

        private static void Postfix(PlayerTownVisitCampaignBehavior __instance, CampaignGameStarter campaignGameSystemStarter)
        {
            campaignGameSystemStarter.AddGameMenuOption("town", "cps_town_courier", "Find a courier", new GameMenuOption.OnConditionDelegate(game_menu_town_find_courier_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_town_find_courier_on_consequence), false, 4, false);
            campaignGameSystemStarter.AddGameMenu("cps_town_courier", "You find a Calradian Postal Service agent.  They can send a missive by raven for a small fee.", new OnInitDelegate(cps_town_courier_on_init), 0, GameMenu.MenuFlags.none, null);
            campaignGameSystemStarter.AddGameMenuOption("cps_town_courier", "cps_town_courier_missive", "Pay a {AMOUNT}{GOLD_ICON} fee to send the missive", new GameMenuOption.OnConditionDelegate(game_menu_cps_town_courier_missive_on_condition), new GameMenuOption.OnConsequenceDelegate(game_menu_cps_town_courier_missive_on_consequence), false, -1, false);
            campaignGameSystemStarter.AddGameMenuOption("cps_town_courier", "cps_town_courier_back", "{=qWAmxyYz}Back to town center", new GameMenuOption.OnConditionDelegate(back_on_condition), (MenuCallbackArgs x) => GameMenu.SwitchToMenu("town"), true, -1, false);
        }

        private static bool back_on_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        public static bool game_menu_town_find_courier_on_condition(MenuCallbackArgs args)
        {
            // TODO: any conditions to check?

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

        public static bool game_menu_cps_town_courier_missive_on_condition(MenuCallbackArgs args)
        {
            // TODO: calculate courier fee
            int courierFee = 25;
            MBTextManager.SetTextVariable("AMOUNT", courierFee, false);
            //List<Location> list = Settlement.CurrentSettlement.LocationComplex.FindAll((string x) => x == "lordshall").ToList<Location>();
            //args.OptionIssueType = Campaign.Current.IssueManager.CheckIssueForMenuLocations(list);
            //args.OptionQuestStatus = Campaign.Current.QuestManager.CheckQuestForMenuLocations(list);
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            if (Hero.MainHero.Gold < courierFee)
            {
                args.Tooltip = new TextObject("{=d0kbtGYn}You don't have enough gold.", null);
                args.IsEnabled = false;
            }
            return courierFee > 0;
        }

        public static void game_menu_cps_town_courier_missive_on_consequence(MenuCallbackArgs args)
        {
            // TODO: select recipient and type of missive
            //ImageIdentifierType.Character

            var contacts = PostalServiceModel.GetValidMissiveRecipients(Hero.MainHero);
            var elements = (from c in contacts select new InquiryElement(c.StringId, c.Name.ToString(), new ImageIdentifier(CharacterCode.CreateFrom(c.CharacterObject)))).DefaultIfEmpty().ToList();

            InformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Send Missive", "To whom should we deliver this missive?", elements, true, true,
                "Send", "Cancel", OnSendMissive, (List<InquiryElement> r) => { }));
        }

        static void OnSendMissive(List<InquiryElement> recipients)
        {
            // TODO: send the missive
            InformationManager.ShowTextInquiry(new TextInquiryData("Calradian Postal Service", "TODO: Send the missive", true, false, "OK", "Cancel",
                (string s) => { InformationManager.DisplayMessage(new InformationMessage($"You entered: {s}", new Color(0.25f, 0.8f, 0.8f))); }, null));
        }
    }
}
