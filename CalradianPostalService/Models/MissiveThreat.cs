using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;


namespace CalradianPostalService.Models
{
    public class MissiveThreat : MissiveBase, IMissive
    {

        public MissiveThreat() { }
        public MissiveThreat(MissiveSyncData data) : base(data) { }

        public override void OnDelivery()
        {
            base.OnDelivery();

            int valor   = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Valor);
            int honor   = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.Honor);
            int ironic  = MissiveAcceptanceHelper.Trait(Recipient, DefaultTraits.PersonaIronic);

            float relation = (float)Recipient.GetRelation(Sender);
            float roll     = MBRandom.RandomFloat;

            // High Valor: recipient defies the threat rather than being intimidated
            // Roll against valor to see if they take it as a challenge instead
            float defianceChance = MissiveAcceptanceHelper.Clamp01(valor * 0.20f);
            if (roll <= defianceChance)
            {
                // Grudging respect — the threat is met with steel, not fear
                if (Hero.MainHero == Sender)
                    CpsLogger.Info($"{Recipient} was not impressed by your threat and has chosen to defy you.");

                CpsLogger.Debug($"[MissiveThreat] DEFIANCE valor:{valor} defianceChance:{defianceChance:F2} roll:{roll:F2}");
                // No relation change — the threat bounced off
                return;
            }

            // Ironic persona: small chance they find it amusing instead of angering
            float amusementChance = MissiveAcceptanceHelper.Clamp01(ironic * 0.15f);
            if (roll <= amusementChance + defianceChance)
            {
                if (Hero.MainHero == Sender)
                    CpsLogger.Info($"{Recipient} seems to have found your threat more amusing than frightening.");

                CpsLogger.Debug($"[MissiveThreat] AMUSEMENT ironic:{ironic} amusementChance:{amusementChance:F2} roll:{roll:F2}");
                return;
            }

            // Standard anger — base chance that the threat lands
            float angerChance = (relation + 100f) / 200f; // -100→0%, 0→50%, 100→100%

            // Honor multiplies the offence taken — being threatened is beneath them
            float honorMultiplier = 1f + honor * 0.15f; // at +2: ×1.3, at -2: ×0.7

            angerChance = MissiveAcceptanceHelper.Clamp01(angerChance * honorMultiplier);

            CpsLogger.Debug($"[MissiveThreat] relation:{relation} honor:{honor} honorMult:{honorMultiplier:F2} " +
                $"angerChance:{angerChance:F2} roll:{roll:F2}");

            if (roll <= angerChance)
            {
                if (Hero.MainHero == Sender)
                    CpsLogger.Info($"{Recipient} was angered by your letter.");

                // Honor doubles the relation penalty when deeply offended
                int relationPenalty = honor >= 1 ? -2 : -1;
                // TODO: negative relation changes don't grant charm xp — needs separate xp grant here
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Sender, Recipient, relationPenalty);
            }
            else if (Hero.MainHero == Sender)
            {
                CpsLogger.Info($"{Recipient} received your letter, but was not impressed.");
            }
        }
    }
}
