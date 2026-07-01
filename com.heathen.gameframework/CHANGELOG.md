# Changelog

All notable changes to the Heathen Game Framework are documented here. This project follows a
`MAJOR.MINOR.BUILD` scheme (the build number is a continuously growing value).

## [1.0.0]

First stable release. The framework gives Unity the structural spine other engines ship with — subsystems,
a settings/generation pipeline, and standard editor contracts every Heathen tool builds on.

### Subsystems

- **`Subsystem` base + `[Subsystem(scope)]`** with `SubsystemScope` (Global = one per session, World = one per
  World). Subsystems are discovered by type; a trivial, side-effect-free constructor is required so a type can be
  introspected (scope, `DependsOn`, `StartMode`) without side effects.
- **Player-loop tick phases** via marker interfaces — `IBeforeFixed` / `IOnFixed` / `IAfterFixed` /
  `IBeforeUpdate` / `IOnUpdate` / `IAfterUpdate`.
- **`SubsystemStartMode`** (`Disabled` / `OnDemand` / `Automatic`, default `Automatic`) with dependency-ordered
  creation.
- **Project ▸ Subsystems** overview (every subsystem as a card: scope, start mode, tick phases, dependencies)
  and a **Subsystem Debug** window for live inspection in Play mode.

### Settings generation pipeline

- **`ISettingsGenerator`** (`Name`, `Output` = `SourceCode` | `RuntimeAsset`, `IsStale()`, `Generate()`),
  discovered by type. `RuntimeAsset` generators are (re)baked silently; `SourceCode` generators are prompt-only.
- **`SettingsPlayModeGuard`** — holds Play and offers *Build & Play* when a source generator is stale; regenerates
  runtime assets during the transition. **`SettingsBuildPreprocessor`** keeps generated output current for player
  builds.

### Editor contracts (tool opt-in, discovered by type)

- **`ISubsystemConfigEditor`** — the one standard place to edit a subsystem's start mode, surfaced on the
  overview.
- **`ISubsystemHealth`** + `SubsystemHealth` aggregator — a subsystem reports setup problems (not built,
  misconfigured, …), surfaced as a header **chip** on the overview, a **play-mode guard** (*Play Anyway / View
  Subsystems / Cancel*), and a **Scene-view attention overlay**.

### Shared editor styling

- **`HeathenEditorStyles`** — one visual language for every tool: boxed section cards, indentation, add/remove
  buttons, a common palette, and the standard **Build status button** (`BuildStatus` — Ready / Build / Update /
  Error) so every tool's build affordance looks and behaves identically.

### Adopted by

Steamworks (Foundation + Toolkit), Ogham Storyteller, and Gameplay Tags all build on these contracts. Tools whose
output is produced by a `ScriptedImporter` (e.g. Lexicon) are inherently always-current and stay outside the
build/dirty pipeline by design.
