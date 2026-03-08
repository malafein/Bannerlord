"""
Test plan: Calradian Postal Service (CPS)
Each step is a dict with keys: id, section, action, expected,
and optionally auto_patterns (list of regex strings).
"""

PLAN_NAME = "Calradian Postal Service"
LOG_PREFIXES = ("[CalradianPostalService]",)

STEPS = [
    # -----------------------------------------------------------------------
    # 1. Personal Missives — Access & Cost
    # -----------------------------------------------------------------------
    {
        "id": "1.1", "section": "1. Personal Missives — Access & Cost",
        "action": "Enter a town → Find a courier → Send a personal letter",
        "expected": "Recipient list appears",
    },
    {
        "id": "1.2", "section": "1. Personal Missives — Access & Cost",
        "action": "Select a recipient",
        "expected": "Menu shows fee of 50g, not the distance-based diplomatic fee",
    },
    {
        "id": "1.3", "section": "1. Personal Missives — Access & Cost",
        "action": "With < 50g gold, attempt to send",
        "expected": 'Option is disabled with "not enough gold" tooltip',
    },
    {
        "id": "1.4", "section": "1. Personal Missives — Access & Cost",
        "action": "Send a friendly missive",
        "expected": '50g deducted; "Missive sent" log entry appears',
        "auto_patterns": [r"\[MissiveFriendly\] Missive sent"],
    },

    # -----------------------------------------------------------------------
    # 2. Personal Missives — Cooldown
    # -----------------------------------------------------------------------
    {
        "id": "2.1", "section": "2. Personal Missives — Cooldown",
        "action": "Send any personal missive to Recipient A",
        "expected": "Succeeds; cooldown set log appears",
        "auto_patterns": [r"\[Cooldown\].*cooldown set"],
    },
    {
        "id": "2.2", "section": "2. Personal Missives — Cooldown",
        "action": "Immediately try to send another to Recipient A",
        "expected": 'Option disabled with "Wait X days" tooltip — verify visually',
    },
    {
        "id": "2.3", "section": "2. Personal Missives — Cooldown",
        "action": "Send a personal missive to Recipient B (different hero)",
        "expected": "Still allowed — cooldown is per-recipient",
        "auto_patterns": [r"\[Missive(?:Friendly|Threat)\] Missive sent"],
    },
    {
        "id": "2.4", "section": "2. Personal Missives — Cooldown",
        "action": "Save, reload, check Recipient A option",
        "expected": 'Still disabled (cooldown persisted); "Loaded N missives, N cooldowns" in log',
        "auto_patterns": [r"Loaded \d+ missives, \d+ cooldowns"],
    },
    {
        "id": "2.5", "section": "2. Personal Missives — Cooldown",
        "action": "Advance time 7+ days, check Recipient A option",
        "expected": "Now enabled again — verify visually",
    },

    # -----------------------------------------------------------------------
    # 3. Friendly Missive — Outcomes
    # -----------------------------------------------------------------------
    {
        "id": "3.1", "section": "3. Friendly Missive — Outcomes",
        "action": "Send to a generous/earnest recipient",
        "expected": '"was moved by your letter" or "appreciated your letter" in log',
        "auto_patterns": [r"was moved by your letter|appreciated your letter"],
    },
    {
        "id": "3.2", "section": "3. Friendly Missive — Outcomes",
        "action": "Send to a curt recipient with low relation",
        "expected": '"was not pleased by your letter" possible in log',
        "auto_patterns": [r"was not pleased by your letter|received your letter"],
    },
    {
        "id": "3.3", "section": "3. Friendly Missive — Outcomes",
        "action": "Send to a close friend (relation ~80+)",
        "expected": "Reception likely, but relation gain unlikely — check improvementChance in log",
        "auto_patterns": [r"\[MissiveFriendly\] APPRECIATED improvementChance"],
    },
    {
        "id": "3.4", "section": "3. Friendly Missive — Outcomes",
        "action": "Send to an enemy (relation ~-80)",
        "expected": "Reception unlikely; if backfire fires, −1 to −3 relation in log",
        "auto_patterns": [r"\[MissiveFriendly\] NOT APPRECIATED backfireChance"],
    },
    {
        "id": "3.5", "section": "3. Friendly Missive — Outcomes",
        "action": "Check debug log",
        "expected": "Verify receptionChance, improvementChance/backfireChance, and rolls all printed",
        "auto_patterns": [r"\[MissiveFriendly\].*receptionChance"],
    },

    # -----------------------------------------------------------------------
    # 4. Threatening Missive — Outcomes
    # -----------------------------------------------------------------------
    {
        "id": "4.1", "section": "4. Threatening Missive — Outcomes",
        "action": "Send to a high-valor recipient (valor ≥ 2)",
        "expected": '"chosen to defy you" in log; no relation change',
        "auto_patterns": [r"chosen to defy you"],
    },
    {
        "id": "4.2", "section": "4. Threatening Missive — Outcomes",
        "action": "Send to a high-ironic recipient",
        "expected": '"found your threat more amusing" in log; no relation change',
        "auto_patterns": [r"found your threat more amusing"],
    },
    {
        "id": "4.3", "section": "4. Threatening Missive — Outcomes",
        "action": "Send to an honorable recipient with warm relations",
        "expected": '"was angered by your letter" with −2 or −3 penalty in log',
        "auto_patterns": [r"was angered by your letter"],
    },
    {
        "id": "4.4", "section": "4. Threatening Missive — Outcomes",
        "action": "Send to a low-relation, low-honor recipient",
        "expected": '"was not impressed" in log',
        "auto_patterns": [r"received your letter, but was not impressed"],
    },
    {
        "id": "4.5", "section": "4. Threatening Missive — Outcomes",
        "action": "Check debug log",
        "expected": "Three [MissiveThreat] debug lines: defianceChance/roll1a, amusementChance/roll1b, angerChance/roll2",
        "auto_patterns": [r"\[MissiveThreat\].*roll1a", r"\[MissiveThreat\].*roll1b", r"\[MissiveThreat\].*roll2"],
    },

    # -----------------------------------------------------------------------
    # 5. Peace Missive — Target Selection
    # -----------------------------------------------------------------------
    {
        "id": "5.1", "section": "5. Peace Missive — Target Selection",
        "action": "Select a recipient at war with multiple factions",
        "expected": "Peace option enabled; target selection lists all warring factions",
    },
    {
        "id": "5.2", "section": "5. Peace Missive — Target Selection",
        "action": "Select a recipient not at war with anyone",
        "expected": 'Peace option disabled with "not currently at war" tooltip',
    },
    {
        "id": "5.3", "section": "5. Peace Missive — Target Selection",
        "action": "Select a third-party faction as the peace target",
        "expected": "Missive sent; [MissivePeace] debug log shows target faction name",
        "auto_patterns": [r"\[MissivePeace\] Missive sent"],
    },
    {
        "id": "5.4", "section": "5. Peace Missive — Target Selection",
        "action": "Select your own faction as the peace target",
        "expected": "Works as before; [MissivePeace] debug log shows target:YourFaction",
        "auto_patterns": [r"\[MissivePeace\] Missive sent"],
    },

    # -----------------------------------------------------------------------
    # 6. Diplomatic Missives — Save/Load
    # -----------------------------------------------------------------------
    {
        "id": "6.1", "section": "6. Diplomatic Missives — Save/Load",
        "action": "Send a peace/war/join-war/alliance missive",
        "expected": '"Missive sent, arrives in X days" in log',
        "auto_patterns": [r"Missive sent to .+\. Arrives in"],
    },
    {
        "id": "6.2", "section": "6. Diplomatic Missives — Save/Load",
        "action": "Save the game before arrival",
        "expected": '"Saved N missives, N cooldowns" in log',
        "auto_patterns": [r"Saved \d+ missives, \d+ cooldowns"],
    },
    {
        "id": "6.3", "section": "6. Diplomatic Missives — Save/Load",
        "action": "Load the save",
        "expected": '"Loaded N missives, N cooldowns" — count matches what was sent',
        "auto_patterns": [r"Loaded \d+ missives, \d+ cooldowns"],
    },
    {
        "id": "6.4", "section": "6. Diplomatic Missives — Save/Load",
        "action": "Advance time to delivery",
        "expected": '"Missive delivered" in log; outcome logged normally',
        "auto_patterns": [r"Missive delivered from"],
    },

    # -----------------------------------------------------------------------
    # 7. Charm XP
    # -----------------------------------------------------------------------
    {
        "id": "7.1", "section": "7. Charm XP",
        "action": "Note Charm XP before sending (open character screen → Skills → Social)",
        "expected": "Baseline recorded — no log to detect; mark manually",
    },
    {
        "id": "7.2", "section": "7. Charm XP",
        "action": "Send a friendly missive; recipient appreciates it",
        "expected": '+15 Charm XP logged: "[CharmXP] +15 Charm XP granted"',
        "auto_patterns": [r"\[CharmXP\] \+15 Charm XP"],
    },
    {
        "id": "7.3", "section": "7. Charm XP",
        "action": "Send a threatening missive that angers the recipient",
        "expected": '+15 Charm XP logged: "[CharmXP] +15 Charm XP granted"',
        "auto_patterns": [r"\[CharmXP\] \+15 Charm XP"],
    },
    {
        "id": "7.4", "section": "7. Charm XP",
        "action": "Send any diplomatic missive (peace/war/join-war/alliance)",
        "expected": '+20 Charm XP logged: "[CharmXP] +20 Charm XP granted"',
        "auto_patterns": [r"\[CharmXP\] \+20 Charm XP"],
    },
    {
        "id": "7.5", "section": "7. Charm XP",
        "action": "Send a friendly missive that is ignored or backfires (check log carefully)",
        "expected": "No [CharmXP] line — XP is only granted when reception succeeds",
        "auto_patterns": [r"\[MissiveFriendly\] NOT APPRECIATED"],
    },
]
