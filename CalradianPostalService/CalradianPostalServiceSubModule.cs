using System;
using System.Linq;
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
        public static string Version { get; private set; } = "unknown";

        public static PostalServiceModel PostalServiceModel =>
            (from m in Campaign.Current.Models.GetGameModels()
             where m is PostalServiceModel
             select m).FirstOrDefault() as PostalServiceModel;

        public static readonly string ModuleName = "CalradianPostalService";
        public static readonly string ModulePath = $"{BasePath.Name}/Modules/{ModuleName}";
        public static readonly string ModuleDataPath = $"{ModulePath}/ModuleData";

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            CpsLogger.Info($"Version {Version} loaded.");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            try
            {
                if (game.GameType is Campaign)
                {
                    CampaignGameStarter campaignGameStarter = gameStarterObject as CampaignGameStarter;
                    campaignGameStarter.AddModel(new DefaultPostalServiceModel());
                    campaignGameStarter.AddBehavior(new PostalServiceBehavior());
                    CpsLogger.Log("Model and behavior registered.");
                }
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "OnGameStart failed");
            }
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.Load($"{ModulePath}/SubModule.xml");
                Version = doc.SelectSingleNode("/Module/Version/@value")?.Value ?? "unknown";
                CpsLogger.Log($"Version read from SubModule.xml: {Version}");

                ModuleConfiguration.LoadConfiguration();
                CpsLogger.Log("Configuration loaded.");
            }
            catch (Exception ex)
            {
                // CpsLogger.Error can't be used here if it fails before game APIs are ready,
                // so fall back to DisplayMessage directly.
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[CalradianPostalService] Init error: {ex.Message}",
                    new Color(0.8f, 0.0f, 0.0f)));
            }
        }
    }

    /// <summary>
    /// Logging for CalradianPostalService. All output goes to the game's rgl_log
    /// (same file as other game events) via Debug.Print. Info and Error levels
    /// also show an in-game message.
    /// </summary>
    internal static class CpsLogger
    {
        private const string Prefix = "[CalradianPostalService]";
        private static readonly Color InfoColor = new Color(0.25f, 0.8f, 0.8f);
        private static readonly Color ErrorColor = new Color(0.8f, 0.0f, 0.0f);

        // Writes to rgl_log only. Use for trace/diagnostic entries.
        public static void Log(string message)
            => TaleWorlds.Library.Debug.Print($"{Prefix} {message}", 0, TaleWorlds.Library.Debug.DebugColor.White, 17592186044416UL);

        // Shows in-game (cyan) and writes to rgl_log. Use for player-visible status.
        public static void Info(string message)
        {
            InformationManager.DisplayMessage(new InformationMessage($"{Prefix} {message}", InfoColor));
            Log(message);
        }

        // Shows in-game (red) and writes to rgl_log.
        public static void Error(string message)
        {
            InformationManager.DisplayMessage(new InformationMessage($"{Prefix} {message}", ErrorColor));
            Log($"ERROR: {message}");
        }

        // Logs full exception details to rgl_log; shows brief message in-game.
        public static void Error(Exception ex, string context = null)
        {
            string label = context != null ? $"{context}: {ex.Message}" : ex.Message;
            Error(label);
            Log($"  StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
                Log($"  InnerException: {ex.InnerException.Message}");
        }

        // Writes to rgl_log only (no in-game message). Always active; use for verbose diagnostics.
        public static void Verbose(string message)
            => Log($"[V] {message}");
    }
}
