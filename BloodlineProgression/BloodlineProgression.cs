using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BloodlineProgression
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (gameStarterObject is CampaignGameStarter campaignGameStarter)
            {
                campaignGameStarter.AddModel(new BloodlineCharacterDevelopmentModel());
                campaignGameStarter.AddBehavior(new BloodlineCampaignBehavior());

                var config = Config.Instance;
                BloodlineLogger.Log($"Mod loaded. Config: AttributePointMultiplier={config.AttributePointMultiplier}, SkillPointMultiplier={config.SkillPointMultiplier}, EnableLearningBonus={config.EnableLearningBonus}, LearningRateThreshold={config.LearningRateThreshold}");

                InformationManager.DisplayMessage(new InformationMessage("[Bloodline Progression] Mod loaded and active!", Colors.Red));
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Bloodline] Attr x{config.AttributePointMultiplier:F1} | Focus x{config.SkillPointMultiplier:F1} | LearningBonus={(config.EnableLearningBonus ? $"on (min {config.LearningRateThreshold:F2})" : "off")}",
                    Colors.Green));
            }
        }
    }

    // Awards extra focus/attribute points on level-up, restricted to bloodline heroes (main hero, siblings, and all their descendants).
    public class BloodlineCampaignBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.HeroLevelledUp.AddNonSerializedListener(this, OnHeroLevelledUp);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnHeroLevelledUp(Hero hero, bool shouldNotify)
        {
            if (!BloodlineHelper.IsBloodlineHero(hero)) return;

            try
            {
                var config = Config.Instance;
                var devModel = Campaign.Current.Models.CharacterDevelopmentModel;

                // Extra focus points: the base game already awarded FocusPointsPerLevel (1).
                // We add (multiplier - 1) more for bloodline heroes.
                int baseFocus = devModel.FocusPointsPerLevel;
                int extraFocus = baseFocus * (int)Math.Max(1f, config.SkillPointMultiplier) - baseFocus;
                if (extraFocus > 0)
                {
                    hero.HeroDeveloper.UnspentFocusPoints += extraFocus;
                    BloodlineLogger.Log($"LevelUp {hero.Name} lvl={hero.Level}: +{extraFocus} focus (unspent now {hero.HeroDeveloper.UnspentFocusPoints})");
                }

                // Extra attribute points when our reduced interval triggers but the base interval does not.
                // e.g. base=4 levels/point, multiplier=2 → modifiedLevels=2.
                // At levels 2, 6, 10, ... the base game gives 0 attribute points but we give 1.
                int baseLevels = devModel.LevelsPerAttributePoint;
                int multiplier = (int)Math.Max(1f, config.AttributePointMultiplier);
                int modifiedLevels = Math.Max(1, baseLevels / multiplier);

                if (modifiedLevels < baseLevels && hero.Level % modifiedLevels == 0 && hero.Level % baseLevels != 0)
                {
                    hero.HeroDeveloper.UnspentAttributePoints += 1;
                    BloodlineLogger.Log($"LevelUp {hero.Name} lvl={hero.Level}: +1 attribute (unspent now {hero.HeroDeveloper.UnspentAttributePoints})");
                }
            }
            catch (Exception ex)
            {
                BloodlineLogger.Log($"Error in OnHeroLevelledUp: {ex.Message}");
            }
        }
    }

    public class BloodlineCharacterDevelopmentModel : DefaultCharacterDevelopmentModel
    {
        // Throttle learning rate bonus logging to avoid log spam.
        private static int _learningBonusLogCount = 0;
        private const int LearningBonusLogInterval = 50;

        public override ExplainedNumber CalculateLearningRate(IReadOnlyPropertyOwner<CharacterAttribute> characterAttributes, int focusValue, int skillValue, SkillObject skill, bool includeDescriptions)
        {
            ExplainedNumber result = base.CalculateLearningRate(characterAttributes, focusValue, skillValue, skill, includeDescriptions);

            try
            {
                var config = Config.Instance;
                if (!config.EnableLearningBonus) return result;

                // Only apply to bloodline heroes.
                // Cast via object to avoid the compile-time reference-conversion restriction.
                var hero = (characterAttributes as object as CharacterObject)?.HeroObject;
                if (!BloodlineHelper.IsBloodlineHero(hero)) return result;

                if (result.ResultNumber < config.LearningRateThreshold)
                {
                    float baseRate = result.ResultNumber;
                    // LimitMin raises the clamp floor directly — the correct fix since the base game's
                    // own LimitMin(0) makes Add() ineffective on deeply-negative internal sums.
                    result.LimitMin(config.LearningRateThreshold);

                    if (_learningBonusLogCount % LearningBonusLogInterval == 0)
                    {
                        BloodlineLogger.Log($"LearningRate floor set: hero={hero.Name}, skill={skill?.StringId ?? "?"}, baseRate={baseRate:F4}, threshold={config.LearningRateThreshold:F4}, finalRate={result.ResultNumber:F4}, focus={focusValue}, skillValue={skillValue}");
                    }
                    _learningBonusLogCount++;
                }
            }
            catch (Exception ex)
            {
                BloodlineLogger.Log($"Error in CalculateLearningRate: {ex.Message}");
            }

            return result;
        }
    }

    internal static class BloodlineHelper
    {
        // Returns true for the main hero, their siblings, and all descendants of both.
        public static bool IsBloodlineHero(Hero hero)
        {
            if (hero == null) return false;

            var main = Hero.MainHero;
            if (main == null) return false;
            if (hero == main) return true;

            // Descendants of the main hero.
            if (IsDescendant(hero, main)) return true;

            // Siblings and their descendants.
            foreach (var sibling in main.Siblings)
            {
                if (sibling == hero) return true;
                if (IsDescendant(hero, sibling)) return true;
            }

            return false;
        }

        private static bool IsDescendant(Hero candidate, Hero ancestor)
        {
            foreach (var child in ancestor.Children)
            {
                if (child == candidate) return true;
                if (IsDescendant(candidate, child)) return true;
            }
            return false;
        }
    }

    internal static class BloodlineLogger
    {
        private const string Prefix = "[BloodlineProgression]";

        public static void Log(string message)
        {
            Debug.Print($"{Prefix} {message}", 0, Debug.DebugColor.White, 17592186044416UL);
        }
    }
}
