# Bannerlord
Mods for Mount &amp; Blade II: Bannerlord

## Calradian Postal Service
A courier service that allows you to send missives between heroes, exercising your Charm skill and affecting relations.
This mod is still in early development, so I don't recommend installing in your main save. Feel free to check it out and provide feedback.

Will be available on [Nexus](https://www.nexusmods.com/mountandblade2bannerlord/mods/) after a few more missive types are implemented.

### Implemented Features

* New game menu added to towns to find a courier
* Select any known, living hero as a recipient
* **Personal missives** — flat 50g fee, 7-day cooldown per recipient
  * **Friendly** — Three possible outcomes based on recipient personality, current relations, and sender's Charm skill:
    * Letter is appreciated → chance of +1 to +3 relation (diminishing returns — harder to improve an already warm friendship)
    * Letter falls flat → no effect
    * Letter backfires → -1 to -3 relation (more likely with curt recipients, hostile relations, or low Charm)
  * **Threatening** — Three independent outcome checks:
    * High-valor recipients may defy the threat (no effect)
    * Ironic recipients may find it amusing (no effect)
    * Otherwise the threat may anger them → -1 to -3 relation (more severe with honorable recipients or warm existing relations)
* **Diplomatic missives** — distance-based courier fee
  * **Declare War** — request a lord propose war to their kingdom council against a target
  * **Make Peace** — request a lord propose peace with any faction they are currently at war with (not limited to your own)
  * **Request to Join War** — request a lord join you in an existing war
  * **Alliance** — request a lord propose a formal alliance with another kingdom
* Sender's **Charm skill** affects outcome chances across all missive types (-10% at skill 0, neutral around skill 85, up to +15% at skill 300)
* All missive types grant **Charm XP** on delivery (15 XP for personal, 20 XP for diplomatic)
* Acceptance logic for diplomatic missives accounts for recipient traits (valor, honor, mercy, calculating), military strength ratios, existing relations, sender prestige, and war burden
* All in-flight missives are **saved and restored** with the game
* Configurable courier fees, delivery speed, and cooldown duration

### Planned Features
* More diplomatic missive types
  * Marriage, Trade, Request Troops, Request Support (influence)
* **Command missives** — direct companions and vassals to specific tasks
  * Examples: "Follow me", "Patrol near Danustica", "Raid enemy villages", "Capture Ergeon"

### Features Being Considered
* Add mounted courier troop that physically delivers missives on the campaign map
* Add quest type — mail courier
* **Remote correspondence** — a multi-exchange letter system enabling interactions with distant characters that normally require in-person dialogue (e.g. entering a mercenary contract, accepting a quest or mission from a distant lord). Each exchange would be subject to normal delivery delays, making negotiation across distance a deliberate, time-sensitive process rather than an instant conversation.
* Integrate with Steam friends list for sending messages to friends


---

## Module Unblocker
A script to unblock DLLs in mod directories. Available on [Nexus](https://www.nexusmods.com/mountandblade2bannerlord/mods/181/)

### When do you need this?

This is a **Windows-only** issue. When you download files from the internet, Windows marks them with a Zone.Identifier (a hidden NTFS alternate data stream) that causes the OS to block DLL execution as a security measure.

* **Windows 10** — You will likely need this for any mods not built from source on your machine
* **Windows 11** — May also be needed; behavior has not been confirmed
* **Windows 7** — Users have reported not needing this
* **Linux / Mac** — Not needed; this is an NTFS/Windows-specific mechanism

If your mods aren't loading and you're on Windows 10 or 11, this is likely the fix.

### Usage

Place the `ModuleUnblocker` folder inside your Bannerlord `Modules` directory. Before running it for the first time, you will need to unblock `ModuleUnblocker.bat` itself (right-click → Properties → Unblock), since it was also downloaded from the internet.

Run it any time you install or update a mod.

**ModuleUnblocker.bat** — Unblocks files in custom mod folders only. Excludes `CustomBattle`, `Native`, `SandBox`, `SandBoxCore`, and `StoryMode`. This is the recommended script for most users.

**ModuleUnblockerAll.bat** — Unblocks all module folders including TaleWorlds' own. Slower, but useful if you are using mods that modify native game files.

These scripts must be run from their own module directory (`Mount & Blade II Bannerlord\Modules\ModuleUnblocker`).
