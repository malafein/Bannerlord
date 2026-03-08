using System;
using System.IO;
using Newtonsoft.Json;

namespace CalradianPostalService
{
    public class ModuleConfiguration
    {
        private static readonly string ConfigsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord", "Configs", CalradianPostalServiceSubModule.ModuleName);
        private static readonly string ConfigFilePath = Path.Combine(ConfigsPath, $"{CalradianPostalServiceSubModule.ModuleName}.config.json");
        private static readonly string DefaultConfigFilePath = Path.Combine(CalradianPostalServiceSubModule.ModuleDataPath, $"{CalradianPostalServiceSubModule.ModuleName}.config.json");

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

            // Personal missives (friendly/threat) use a flat fee instead of the distance-based rate
            public int PersonalMissiveBaseFee = 50;
            // Days before another personal missive can be sent to the same recipient
            public int PersonalMissiveCooldownDays = 7;
        }

        public class MissiveOptions
        {
            public bool RenownAffectsRelationChange = false;

            // Per-missive scaling factors (1.0 = default weight; increase to make acceptance more likely overall)
            public float DeclareWarDecisionFactor = 1.0f;
            public float JoinWarDecisionFactor = 1.0f;
            public float OfferPeaceDecisionFactor = 1.0f;
            public float AllianceDecisionFactor = 1.0f;
        }

        public int ConfigVersion = 1;
        public bool EnableFriendlyMissives = true;
        public bool EnableThreateningMissives = true;
        public bool EnableCommandMissives = true;
        public bool EnableDeclareWarMissives = true;
        public bool EnablePeaceMissives = true;
        public bool EnableRequestWarMissives = true;
        public bool EnableAllianceMissives = true;

        private ModuleConfiguration() { }

        public PostalServiceModelOptions PostalService = new PostalServiceModelOptions();
        public MissiveOptions Missives = new MissiveOptions();

        public static ModuleConfiguration Instance { get; private set; } = new ModuleConfiguration();

        public static bool LoadConfiguration()
        {
            try
            {
                if (!Directory.Exists(ConfigsPath))
                    Directory.CreateDirectory(ConfigsPath);

                if (!File.Exists(ConfigFilePath))
                {
                    if (!File.Exists(DefaultConfigFilePath))
                    {
                        CpsLogger.Info("No configuration file found, using defaults.");
                        SaveConfiguration();
                        return true;
                    }
                    File.Copy(DefaultConfigFilePath, ConfigFilePath);
                }

                string config = File.ReadAllText(ConfigFilePath);
                Instance = JsonConvert.DeserializeObject<ModuleConfiguration>(config);

                // TODO: Notify player when default configuration has changed
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "Unable to load configuration");
                return false;
            }

            return true;
        }

        public static bool SaveConfiguration()
        {
            try
            {
                if (!Directory.Exists(ConfigsPath))
                    Directory.CreateDirectory(ConfigsPath);

                string config = JsonConvert.SerializeObject(Instance, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, config);
            }
            catch (Exception ex)
            {
                CpsLogger.Error(ex, "Unable to save configuration");
                return false;
            }

            return true;
        }
    }
}
