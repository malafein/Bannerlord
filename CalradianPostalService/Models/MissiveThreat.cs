using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;


namespace CalradianPostalService.Models
{
    public class MissiveThreat : MissiveBase, IMissive
    {

        public MissiveThreat() { }
        public MissiveThreat(MissiveSyncData data) : base(data) { }

        public override void OnSend()
        {
            int fee = CalradianPostalServiceSubModule.PostalServiceModel.GetPersonalMissiveFee(Sender, Recipient);
            GiveGoldAction.ApplyBetweenCharacters(Sender, null, fee, false);
        }

        public override void OnDelivery()
        {
            base.OnDelivery();

            float relation   = (float)Recipient.GetRelation(Sender);
            float charmBonus = MissiveAcceptanceHelper.CharmBonus(Sender);
            int valor        = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Valor);
            int honor        = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Honor);
            int ironic       = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.PersonaIronic);

            // --- Roll 1a: Defiance (independent) ---
            // High Valor recipients meet the threat with steel, not fear.
            float defianceChance = MissiveAcceptanceHelper.Clamp01(valor * 0.20f);
            float roll1a = MBRandom.RandomFloat;

            CpsLogger.Verbose($"[MissiveThreat] valor:{valor} defianceChance:{defianceChance:F2} roll1a:{roll1a:F2}");

            if (roll1a <= defianceChance)
            {
                if (Hero.MainHero == Sender)
                    CpsLogger.Info($"{Recipient} was not impressed by your threat and has chosen to defy you.");
                return;
            }

            // --- Roll 1b: Amusement (independent) ---
            // Ironic recipients find it more funny than frightening.
            float amusementChance = MissiveAcceptanceHelper.Clamp01(ironic * 0.15f);
            float roll1b = MBRandom.RandomFloat;

            CpsLogger.Verbose($"[MissiveThreat] ironic:{ironic} amusementChance:{amusementChance:F2} roll1b:{roll1b:F2}");

            if (roll1b <= amusementChance)
            {
                if (Hero.MainHero == Sender)
                    CpsLogger.Info($"{Recipient} seems to have found your threat more amusing than frightening.");
                return;
            }

            // --- Roll 2: Anger ---
            // If neither defiance nor amusement fired, the threat lands.
            // Better relations mean they feel more personally betrayed; Honor amplifies the offence.
            // Charm gives a slight reduction — a well-crafted threat is less crudely offensive.
            float angerChance = (relation + 100f) / 200f;           // -100→0%, 0→50%, 100→100%
            float honorMult   = 1f + honor * 0.15f;                 // at +2: ×1.30, at -2: ×0.70
            angerChance = MissiveAcceptanceHelper.Clamp01(angerChance * honorMult - charmBonus * 0.3f);

            float roll2 = MBRandom.RandomFloat;

            CpsLogger.Verbose($"[MissiveThreat] relation:{relation:F0} honor:{honor} honorMult:{honorMult:F2} " +
                $"charm:{charmBonus:F3} angerChance:{angerChance:F2} roll2:{roll2:F2}");

            if (roll2 <= angerChance)
            {
                // Grant Charm XP — the intimidation successfully landed
                if (Sender == Hero.MainHero)
                {
                    Sender.HeroDeveloper?.AddSkillXp(DefaultSkills.Charm, 15f);
                    CpsLogger.Verbose($"[CharmXP] +15 Charm XP granted to {Sender.Name}.");
                }

                int maxPenalty = 1;
                if (honor >= 1) maxPenalty++;
                if (honor == 2 || relation >= 50f) maxPenalty++; // close friend or deeply honorable → deeply offended
                int penalty = MBRandom.RandomInt(1, maxPenalty + 1);

                if (Hero.MainHero == Sender)
                    CpsLogger.Info($"{Recipient} was angered by your letter. (-{penalty} relation)");

                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Sender, Recipient, -penalty);
            }
            else if (Hero.MainHero == Sender)
            {
                CpsLogger.Info($"{Recipient} received your letter, but was not impressed.");
            }
        }
    }
}
