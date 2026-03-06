using System;
using System.IO;
using System.Text;

namespace BloodlineProgression
{
    public class Config
    {
        private const string ConfigFileName = "bloodline_progression.json";

        // Multiplier settings
        public float SkillPointMultiplier { get; set; } = 2.0f;
        public float AttributePointMultiplier { get; set; } = 2.0f;
        public float LearningRateThreshold { get; set; } = 0.1f;

        // Apply bonus when below threshold
        public bool EnableLearningBonus { get; set; } = true;

        private static Config _instance;

        public static Config Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }

        public void Save()
        {
            try
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord",
                    "Modules",
                    "BloodlineProgression",
                    ConfigFileName);

                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                
                var json = SerializeToJson(this);
                File.WriteAllText(configPath, json, Encoding.UTF8);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {e.Message}");
            }
        }

        public static Config Load()
        {
            try
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord",
                    "Modules",
                    "BloodlineProgression",
                    ConfigFileName);

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath, Encoding.UTF8);
                    return DeserializeFromJson<Config>(json);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load config: {e.Message}");
            }

            // Return default config if loading fails
            return new Config();
        }

        public void ResetToDefaults()
        {
            SkillPointMultiplier = 2.0f;
            AttributePointMultiplier = 2.0f;
            LearningRateThreshold = 0.1f;
            EnableLearningBonus = true;
            Save();
        }

        private static string SerializeToJson<T>(T obj)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            
            bool first = true;
            foreach (var prop in typeof(T).GetProperties())
            {
                if (!first) sb.Append(",");
                first = false;

                var value = prop.GetValue(obj);
                string stringValue;
                
                if (value is bool b)
                {
                    stringValue = b ? "true" : "false";
                }
                else if (value is float f)
                {
                    stringValue = f.ToString("F5", System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (value is int i)
                {
                    stringValue = i.ToString();
                }
                else
                {
                    stringValue = $"\"{value}\"";
                }

                sb.Append($"  \"{prop.Name}\": {stringValue}");
            }
            
            sb.AppendLine();
            sb.Append("}");
            return sb.ToString();
        }

        private static T DeserializeFromJson<T>(string json) where T : new()
        {
            var config = new T();
            var type = typeof(T);
            
            foreach (var prop in type.GetProperties())
            {
                string search = $"\"{prop.Name}\"";
                int startIndex = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
                
                if (startIndex >= 0)
                {
                    int colonIndex = json.IndexOf(':', startIndex + search.Length);
                    if (colonIndex >= 0)
                    {
                        int valueStart = colonIndex + 1;
                        while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                            valueStart++;

                        int valueEnd = valueStart;
                        
                        if (json[valueStart] == '"')
                        {
                            // String value
                            valueStart++;
                            while (valueEnd < json.Length)
                            {
                                if (json[valueEnd] == '"' && json[valueEnd - 1] != '\\')
                                    break;
                                valueEnd++;
                            }
                            var stringValue = json.Substring(valueStart, valueEnd - valueStart);
                            prop.SetValue(config, stringValue, null);
                        }
                        else
                        {
                            // Numeric or boolean value
                            while (valueEnd < json.Length && 
                                   char.IsDigit(json[valueEnd]) || 
                                   json[valueEnd] == '.' || 
                                   json[valueEnd] == '-' || 
                                   json[valueEnd] == '+' ||
                                   json[valueEnd] == 'e' ||
                                   json[valueEnd] == 't' || // for true/false
                                   json[valueEnd] == 'f')
                            {
                                valueEnd++;
                            }

                            var valueText = json.Substring(valueStart, valueEnd - valueStart);
                            
                            if (prop.PropertyType == typeof(float))
                            {
                                float f;
                                if (float.TryParse(valueText, System.Globalization.NumberStyles.Float, 
                                    System.Globalization.CultureInfo.InvariantCulture, out f))
                                {
                                    prop.SetValue(config, f, null);
                                }
                            }
                            else if (prop.PropertyType == typeof(int))
                            {
                                int i;
                                if (int.TryParse(valueText, out i))
                                {
                                    prop.SetValue(config, i, null);
                                }
                            }
                            else if (prop.PropertyType == typeof(bool))
                            {
                                bool b = valueText.ToLower() == "true";
                                prop.SetValue(config, b, null);
                            }
                        }
                    }
                }
            }

            return config;
        }
    }
}