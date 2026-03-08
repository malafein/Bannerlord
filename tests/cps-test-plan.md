# Calradian Postal Service — Manual Test Plan

## Setup
- Load an existing save (or start a new campaign and use console commands to advance time/relations as needed)
- Enable debug logging — check `GameLogs` for `[MissiveFriendly]`, `[MissiveThreat]`, etc. entries after each test

---

## 1. Personal Missives — Access & Cost

| # | Action | Expected |
|---|--------|----------|
| 1.1 | Enter a town → Find a courier → Send a personal letter | Recipient list appears |
| 1.2 | Select a recipient | Menu shows fee of **50g**, not the distance-based diplomatic fee |
| 1.3 | With < 50g gold, attempt to send | Option is **disabled** with "not enough gold" tooltip |
| 1.4 | Send a friendly missive | 50g deducted; "Missive sent" log entry appears |

## 2. Personal Missives — Cooldown

| # | Action | Expected |
|---|--------|----------|
| 2.1 | Send any personal missive to Recipient A | Succeeds |
| 2.2 | Immediately try to send another to Recipient A | Option **disabled** with "Wait X days" tooltip |
| 2.3 | Send a personal missive to Recipient B (different hero) | Still **allowed** — cooldown is per-recipient |
| 2.4 | Save, reload, check Recipient A option | Still **disabled** (cooldown persisted) |
| 2.5 | Advance time 7+ days, check Recipient A option | Now **enabled** again |

## 3. Friendly Missive — Outcomes

| # | Setup | Expected log entry |
|---|-------|--------------------|
| 3.1 | Send to a generous/earnest recipient | "was moved by your letter (+X relation)" or "appreciated your letter" |
| 3.2 | Send to a curt recipient with low relation | Possible "was not pleased by your letter (-X relation)" |
| 3.3 | Send to a close friend (relation ~80+) | Reception likely, but relation gain unlikely (diminishing returns) |
| 3.4 | Send to an enemy (relation ~-80) | Reception unlikely; if backfire fires, -1 to -3 relation |
| 3.5 | Check debug log | Verify `receptionChance`, `improvementChance`/`backfireChance`, and rolls all printed |

## 4. Threatening Missive — Outcomes

| # | Setup | Expected log entry |
|---|-------|--------------------|
| 4.1 | Send to a high-valor recipient (valor ≥ 2) | Frequent "chosen to defy you" — no relation change |
| 4.2 | Send to a high-ironic recipient | Frequent "found your threat amusing" — no relation change |
| 4.3 | Send to an honorable recipient with warm relations | "was angered" with -2 or -3 penalty |
| 4.4 | Send to a low-relation, low-honor recipient | Low anger chance; "was not impressed" outcome likely |
| 4.5 | Check debug log | Verify three independent rolls printed for 1a/1b/2 |

## 5. Peace Missive — Target Selection

| # | Action | Expected |
|---|--------|----------|
| 5.1 | Select a recipient at war with multiple factions | Peace option enabled; target selection lists all warring factions |
| 5.2 | Select a recipient not at war with anyone | Peace option **disabled** with "not currently at war" tooltip |
| 5.3 | Select a third-party faction as the peace target | Missive sent; on delivery, acceptance uses recipient's relation with *that* faction's leader |
| 5.4 | Select your own faction as the peace target | Works as before; acceptance uses recipient's relation with your faction leader |

## 6. Diplomatic Missives — Save/Load (regression)

| # | Action | Expected |
|---|--------|----------|
| 5.1 | Send a peace/war/join-war/alliance missive | "Missive sent, arrives in X days" logged |
| 5.2 | Save the game before arrival | Save completes without error |
| 5.3 | Load the save | Log shows "Loaded N missives" — count matches what was sent |
| 5.4 | Advance time to delivery | Missive fires and outcome is logged normally |

## 7. Charm XP

| # | Action | Expected |
|---|--------|----------|
| 6.1 | Note Charm XP before sending | Baseline recorded |
| 6.2 | Send a friendly missive; recipient appreciates it | +15 Charm XP granted to player |
| 6.3 | Send a threatening missive that angers the recipient | +15 Charm XP granted |
| 6.4 | Send any diplomatic missive | +20 Charm XP granted regardless of outcome |
| 6.5 | Send a friendly missive that is ignored or backfires | **No** Charm XP (reception failed) |
