using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CalradianPostalService.Models;
using CalradianPostalService.Behaviors;

namespace CalradianPostalService
{
    public class CalradianPostalServiceSubModule : MBSubModuleBase
    {
        // TODO: read Version from SubModule.xml in OnSubModuleLoad
        public static string Version => "e1.0.0";

        public static PostalServiceModel PostalServiceModel => (from m in Campaign.Current.Models.GetGameModels() where m is PostalServiceModel select m).FirstOrDefault() as PostalServiceModel;

        public static Color InfoColor = new Color(0.25f, 0.8f, 0.8f);
        public static Color ErrorColor = new Color(0.8f, 0.0f, 0.0f);

        public static readonly string ModuleName = "CalradianPostalService";
        public static readonly string ModulePath = $"{BasePath.Name}/Modules/{ModuleName}";   // Game Modules folder
        public static readonly string ModuleDataPath = $"{ModulePath}/ModuleData";

        private static readonly ILog log = LogManager.GetLogger(typeof(CalradianPostalServiceSubModule));

        //public override void BeginGameStart(Game game)
        //{
        //    base.BeginGameStart(game);

        //    DebugMessage("BegingGameStart called.");
        //}

        //public override bool DoLoading(Game game)
        //{
        //    DebugMessage("DoLoading called.");

        //    return base.DoLoading(game);
        //}

        //public override void OnCampaignStart(Game game, object starterObject)
        //{
        //    base.OnCampaignStart(game, starterObject);

        //    DebugMessage("OnCampaignStart called.");
        //}

        //public override void OnGameEnd(Game game)
        //{
        //    base.OnGameEnd(game);

        //    DebugMessage("OnGameEnd called.");
        //}

        //public override void OnGameInitializationFinished(Game game)
        //{
        //    base.OnGameInitializationFinished(game);

        //    DebugMessage("OnGameInitializationFinished called.");
        //}

        //public override void OnGameLoaded(Game game, object initializerObject)
        //{
        //    base.OnGameLoaded(game, initializerObject);

        //    DebugMessage("OnGameLoaded called.");
        //}

        //public override void OnMissionBehaviourInitialize(Mission mission)
        //{
        //    base.OnMissionBehaviourInitialize(mission);

        //    DebugMessage("OnMissionBehaviourInitialize called.");
        //}

        //public override void OnMultiplayerGameStart(Game game, object starterObject)
        //{
        //    base.OnMultiplayerGameStart(game, starterObject);

        //    DebugMessage("OnMultiplayerGameStart called.");
        //}

        //public override void OnNewGameCreated(Game game, object initializerObject)
        //{
        //    base.OnNewGameCreated(game, initializerObject);

        //    DebugMessage("OnNewGameCreated called.");
        //}

        //protected override void OnApplicationTick(float dt)
        //{
        //    base.OnApplicationTick(dt);

        //    //DebugMessage("OnApplicationTick called.");
        //}

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            //DebugMessage("OnBeforeInitialModuleScreenSetAsRoot called.");

            string versionInfo = $"CalradianPostalService Version {Version} loaded.";
            InfoMessage(versionInfo);
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            try
            {
                DebugMessage("OnGameStart called.");
                if (game.GameType is Campaign)
                {
                    CampaignGameStarter campaignGameStarter = gameStarterObject as CampaignGameStarter;
                    campaignGameStarter.AddModel(new DefaultPostalServiceModel());
                    campaignGameStarter.AddBehavior(new PostalServiceBehavior());
                }
            }
            catch (Exception ex)
            {
                DebugMessage(ex);
            }
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            // TODO: read in configs and hook methods w/ harmony
            try
            {
                XmlConfigurator.Configure(new System.IO.FileInfo($"{ModuleDataPath}/log4net.config.xml"));
                ModuleConfiguration.LoadConfiguration();
            }
            catch(Exception exception1)
            {
                string message;
                Exception exception = exception1;
                string str = exception.Message;
                Exception innerException = exception.InnerException;
                if (innerException != null)
                {
                    message = innerException.Message;
                }
                else
                {
                    message = null;
                }
                System.Windows.Forms.MessageBox.Show(string.Concat("Error:\n", str, " \n\n", message));
                DebugMessage(message);
            }
        }

        //protected override void OnSubModuleUnloaded()
        //{
        //    base.OnSubModuleUnloaded();

        //    DebugMessage("OnSubModuleUnloaded called.");
        //}

        private static void DebugMessage(string msg)
        {
#if DEBUG
            DebugMessage(msg, log);
#endif
        }

        private static void DebugMessage(Exception ex)
        {
            DebugMessage(ex, log);
        }

        internal static void DebugMessage(string msg, ILog log)
        {
#if DEBUG
            InformationManager.DisplayMessage(new InformationMessage(msg));
            log.Debug(msg);
#endif
        }

        internal static void DebugMessage(Exception exception, ILog log)
        {
#if DEBUG
            string msg = $"{exception.Message}\n{exception.InnerException}\n{exception.StackTrace}";
            InformationManager.DisplayMessage(new InformationMessage(msg));
            log.Debug(msg);
#endif
        }

        internal static void InfoMessage(string msg, ILog log = null)
        {
            InformationManager.DisplayMessage(new InformationMessage(msg, InfoColor));
            log?.Info(msg);
        }

        internal static void ErrorMessage(string msg, ILog log = null)
        {
            InformationManager.DisplayMessage(new InformationMessage(msg, ErrorColor));
            log?.Info(msg);
        }

        internal static void ErrorMessage(Exception exception, string msg, ILog log = null)
        {
            InformationManager.DisplayMessage(new InformationMessage(msg, ErrorColor));
            log.Error($"{msg}\n{exception.Message}\n{exception.InnerException.Message}");
        }
    }
}
