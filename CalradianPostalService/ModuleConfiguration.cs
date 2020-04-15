using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json;
using TaleWorlds.Engine;

using CPSModule = CalradianPostalService.CalradianPostalServiceSubModule;

namespace CalradianPostalService
{
    public class ModuleConfiguration
    {
        private static string ConfigsPath = System.IO.Path.Combine(Utilities.GetConfigsPath(), CPSModule.ModuleName);   // Documents folder
        private static string ConfigFilePath = System.IO.Path.Combine(ConfigsPath, $"{CPSModule.ModuleName}.config.json");
        private static string DefaultConfigFilePath = System.IO.Path.Combine(CPSModule.ModuleDataPath, $"{CPSModule.ModuleName}.config.json");

        private static readonly ILog log = LogManager.GetLogger(typeof(ModuleConfiguration));

        public class PostalServiceModelOptions
        {
            public float CourierRate = 1.0f;
            public float MissiveDeliveryRate = 1.0f;

            public bool RenownAffectsCourierFee = false;
            public float MinimumRenownAffectingFee = 50.0f;
            public bool DistanceAffectsCourierFee = true;
            public int MinimumCourierFee = 100;
            public int MaximumCourierFee = -1;

            public bool DistanceAffectsDeliveryTime = false;
            public float MissiveDistancePerDay = 1000.0f;
        }

        public class MissiveOptions
        {
            // TODO: Friendly / Threatening Missive
            public bool RenownAffectsRelationChange = false;

            // Diplomacy Missives
            public bool DeclareWarCostsInfluence = true;
            public bool AllowDeclareWarWithInsufficientInfluence = true;
            public bool OfferPeaceCostsInfluence = true;
            public bool AllowOfferPeaceWithInsufficientInfluence = true;
            public float JoinWarDecisionFactor = 1.0f;
        }

        public int ConfigVersion = 1;
        public bool EnableFriendlyMissives = true;
        public bool EnableThreateningMissives = true;
        public bool EnableCommandMissives = true;
        public bool EnableDeclareWarMissives = true;
        public bool EnablePeaceMissives = true;
        public bool EnableRequestWarMissives = true;

        private ModuleConfiguration(){}

        public PostalServiceModelOptions PostalService = new PostalServiceModelOptions();

        public MissiveOptions Missives = new MissiveOptions();

        public static ModuleConfiguration Instance { get; private set; } = new ModuleConfiguration();

        public static bool LoadConfiguration()
        {
            try
            {
                if (!Directory.Exists(ConfigsPath))
                {
                    Directory.CreateDirectory(ConfigsPath);
                }

                if (!File.Exists(ConfigFilePath))
                {
                    if (!File.Exists(DefaultConfigFilePath))
                    {
                        CPSModule.ErrorMessage("No configuration file found, using default configuration.", log);
                        SaveConfiguration();
                        return true;
                    }

                    File.Copy(DefaultConfigFilePath, ConfigFilePath);
                }

                string config = File.ReadAllText(ConfigFilePath);
                Instance = JsonConvert.DeserializeObject<ModuleConfiguration>(config);

                // TODO: Notify player when default configuration has changed, so they can decide whether they want to make adjustments
            }
            catch (Exception ex)
            {
                CPSModule.ErrorMessage(ex, "Unable to load configuration.", log);
                return false;
            }

            return true;
        }

        public static bool SaveConfiguration()
        {
            try
            {
                if (!Directory.Exists(ConfigsPath))
                {
                    Directory.CreateDirectory(ConfigsPath);
                }

                string config = JsonConvert.SerializeObject(Instance, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, config);
            }
            catch (Exception ex)
            {
                CPSModule.ErrorMessage(ex, "Unable to save configuration.", log);
                return false;
            }

            return true;
        }
    }
}
