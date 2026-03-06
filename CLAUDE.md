# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the mod
dotnet build BloodlineProgression/BloodlineProgression.csproj

# Build in release mode (also auto-deploys to game folder)
dotnet build BloodlineProgression/BloodlineProgression.csproj -c Release

# Build the API analyzer utility
dotnet build APIAnalyzer/APIAnalyzer.csproj

# Run the API analyzer
dotnet run --project APIAnalyzer/APIAnalyzer.csproj
```

The post-build event automatically copies the built DLL and `SubModule.xml` to the Bannerlord game's module folder at `~/.steam/steam/steamapps/common/Mount & Blade II Bannerlord/Modules/BloodlineProgression/`.

## Architecture

This is a Mount & Blade II: Bannerlord game mod workspace with two projects:

### BloodlineProgression (mod DLL)
Targets `netstandard2.0`. The mod hooks into the game via `MBSubModuleBase.OnGameStart()` to replace `DefaultCharacterDevelopmentModel` with a custom subclass.

- **[BloodlineProgression.cs](BloodlineProgression/BloodlineProgression.cs)** — `SubModule` entry point + `BloodlineCharacterDevelopmentModel` override. The custom model adjusts attribute points, focus points, and implements a "Bloodline Resilience" learning rate bonus when skill learning rate drops below a configurable threshold.
- **[Config.cs](BloodlineProgression/Config.cs)** — Singleton config with hand-rolled JSON serialization (no external JSON library). Persists to `~/Documents/Mount and Blade II Bannerlord/Modules/BloodlineProgression/bloodline_progression.json`.
- **[SubModule.xml](BloodlineProgression/SubModule.xml)** — Module manifest declaring mod ID, version, and dependencies (Native, SandBoxCore, Sandbox v1.3.15).
- **[Directory.Build.props](BloodlineProgression/Directory.Build.props)** — Sets game DLL reference paths pointing to the Steam install.

### APIAnalyzer (console tool)
Targets `net10.0`. Uses .NET reflection to inspect `DefaultCharacterDevelopmentModel` from the game assembly and print relevant methods/properties (filtered by "Point", "Learning", "Calculate"). Useful for discovering game API surface when extending the mod.

## Game References

All game DLLs are referenced from:
```
~/.steam/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/
```
Key assemblies: `TaleWorlds.CampaignSystem`, `TaleWorlds.Core`, `TaleWorlds.Library`, `TaleWorlds.MountAndBlade`, `TaleWorlds.ObjectSystem`, `TaleWorlds.Localization`.

## Testing

No test framework. Testing is manual via in-game play. Game logs are accessible via the `GameLogs` symlink in the workspace root.
