using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;


namespace CalradianPostalService.Models
{
    public class MissiveFriendly : MissiveBase, IMissive
    {

        public MissiveFriendly() { }
        public MissiveFriendly(MissiveSyncData data) : base(data) { }

        public override void OnSend()
        {
            int fee = CalradianPostalServiceSubModule.PostalServiceModel.GetPersonalMissiveFee(Sender, Recipient);
            GiveGoldAction.ApplyBetweenCharacters(Sender, null, fee, false);
        }

        public override void OnDelivery()
        {
            base.OnDelivery();

            float relation    = (float)Recipient.GetRelation(Sender);
            float charmBonus  = MissiveAcceptanceHelper.CharmBonus(Sender);
            int generosity    = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Generosity);
            int earnest       = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.PersonaEarnest);
            int curt          = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.PersonaCurt);

            // --- Roll 1: Reception ---
            // Did the recipient appreciate receiving the letter at all?
            // Base 50%, nudged by relation (small), personality, and sender's Charm.
            float receptionChance = 0.50f
                + relation    * 0.002f   // -100→-0.20, +100→+0.20
                + charmBonus             // -0.10 to +0.10
                + generosity  * 0.08f
                + earnest     * 0.08f
                - curt        * 0.08f;
            receptionChance = MissiveAcceptanceHelper.Clamp01(receptionChance);

            float roll1 = MBRandom.RandomFloat;

            CpsLogger.Verbose($"[MissiveFriendly] relation:{relation:F0} charm:{charmBonus:F3} " +
                $"generosity:{generosity} earnest:{earnest} curt:{curt} " +
                $"receptionChance:{receptionChance:F2} roll1:{roll1:F2}");

            if (roll1 <= receptionChance)
            {
                // Letter was appreciated — grant Charm XP
                if (Sender == Hero.MainHero)
                {
                    Sender.HeroDeveloper?.AddSkillXp(DefaultSkills.Charm, 15f);
                    CpsLogger.Verbose($"[CharmXP] +15 Charm XP granted to {Sender.Name}.");
                }

                // --- Roll 2: Diminishing returns ---
                // Easier to improve a cold relationship; harder when already warm.
                float improvementChance = MissiveAcceptanceHelper.Clamp01((100f - relation) / 100f);
                float roll2 = MBRandom.RandomFloat;

                CpsLogger.Verbose($"[MissiveFriendly] APPRECIATED improvementChance:{improvementChance:F2} roll2:{roll2:F2}");

                if (roll2 <= improvementChance)
                {
                    int maxGain = 1;
                    if (generosity >= 1 || earnest >= 1) maxGain++;
                    if (generosity == 2 || earnest == 2 || charmBonus >= 0.05f) maxGain++;
                    int gain = MBRandom.RandomInt(1, maxGain + 1);

                    if (Hero.MainHero == Sender)
                        CpsLogger.Info($"{Recipient} was moved by your letter. (+{gain} relation)");

                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Sender, Recipient, gain);
                }
                else if (Hero.MainHero == Sender)
                {
                    CpsLogger.Info($"{Recipient} appreciated your letter.");
                }
            }
            else
            {
                // Letter was not appreciated — check if it actively backfires.
                // Curt recipients hate pleasantries; hostile relations make a friendly letter
                // read as manipulative; low Charm means it was poorly worded.
                float backfireChance = 0.15f
                    + curt       * 0.12f
                    - generosity * 0.06f
                    - earnest    * 0.06f
                    - charmBonus            // low charm increases backfire risk
                    + Math.Max(0f, -relation * 0.002f); // hostile relations amplify risk
                backfireChance = MissiveAcceptanceHelper.Clamp01(backfireChance);

                float roll3 = MBRandom.RandomFloat;

                CpsLogger.Verbose($"[MissiveFriendly] NOT APPRECIATED backfireChance:{backfireChance:F2} roll3:{roll3:F2}");

                if (roll3 <= backfireChance)
                {
                    int maxLoss = 1;
                    if (curt >= 1) maxLoss++;
                    if (curt == 2 || relation <= -50f || charmBonus <= -0.05f) maxLoss++;
                    int loss = MBRandom.RandomInt(1, maxLoss + 1);

                    if (Hero.MainHero == Sender)
                        CpsLogger.Info($"{Recipient} was not pleased by your letter. (-{loss} relation)");

                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Sender, Recipient, -loss);
                }
                else if (Hero.MainHero == Sender)
                {
                    CpsLogger.Info($"{Recipient} received your letter.");
                }
            }
        }
    }
}
