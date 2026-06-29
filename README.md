# Heathen Game Framework

Low-level game architecture primitives for Unity. Unity ships as a sandbox and gives
developers very little structure; this package provides the structural framework that
engines like Unreal, O3DE, Flax and Godot have built in, without prescribing how you
build your game. Heathen's own Foundations (GameplayTags, Lexicon, Ogham, HATE, ...)
build on it, and Toolkits build on those.

This is a low-level primitive (like xxHash): it has no Toolkit of its own. Foundations
are built on it; Toolkits are built on those Foundations. Licensed Apache 2.0.

Package: `com.heathen.gameframework` · Assemblies: `Heathen.GameFramework` (runtime),
`Heathen.GameFramework.Editor` (editor)

Because it is this low-level, its types live directly in the **`Heathen`** namespace (runtime) and
**`Heathen.Editor`** (editor), e.g. `Heathen.GameFramework`, `Heathen.World`, `Heathen.Subsystem`,
`Heathen.GameMode`, `Heathen.GameState`, `Heathen.PlayerState`. (Avoiding a `Heathen.GameFramework`
*namespace* is deliberate: it would shadow the same-named entry class from inside the `Heathen.*` tree.)

## What it provides

### 1. Subsystems

Statically-accessible singletons with managed lifecycle, no GameObject, no scene
presence. Borrowed from Unreal's subsystem family.

- **Scopes**: `Global` (one per process, lives for the session) and `World` (one
  instance per framework `World`, created and destroyed with it).
- **Lifecycle**: `Initialize` / `Deinitialize`, with declared dependency ordering so a
  subsystem can require another to be up first (e.g. HATE requires GameplayTags).
- **Tick** (opt-in): six player-loop phases driven via the `PlayerLoop` API, not
  `MonoBehaviour`:
  `BeforeFixed`, `OnFixed`, `AfterFixed`, `BeforeUpdate`, `OnUpdate`, `AfterUpdate`.
- **Access**: `GameFramework.Get<T>()` for Global; `world.Get<T>()` (and
  `GameFramework.MainWorld`) for World-scoped. Tools wrap these in their own static
  facade, e.g. `OghamSubsystem.StartStory("OGHAM.OpeningProse")`.

### 2. World, and the optional GameMode structure

Unity has no first-class World object (a `Scene` is just a GameObject container, and is
not 1:1 with a simulation world). The framework introduces a lightweight managed `World`
that owns its World-scoped subsystems and ticks them while active. HATE worlds and
Storyteller sessions are World-scoped.

Worlds are pure-memory containers, not GameObjects. Multiple worlds can exist at once
(e.g. a `PauseWorld` and a `GameplayWorld`; later `Player1World` / `Player2World`). They
are owned by a `WorldManagerSubsystem`, which is itself a **Global** subsystem, so the
World system is just the first consumer of the subsystem structure rather than a special
case. A world's lifecycle starts its subsystems on create and destroys them on dispose.

Each world can optionally host the Unreal-style structure (all optional, never required):

```
[Global] WorldManagerSubsystem
             └── World (instance)
                   ├── WorldSubsystems[]
                   └── GameMode (optional logic)
                         ├── GameState (data)
                         └── PlayerState[] (data, keyed by PlayerId)
```

- **GameMode** is logic: rules and flow, exposed as hooks you register (composition over
  inheritance). It can read/mutate GameState and PlayerState. Server-only, never replicated.
- **GameState** is data about the current game. Server-authoritative, replicated read-only.
- **PlayerState** is per-player data (one per `PlayerId`). Per-owner authority, replicated
  to observers.

### Networking and layering rules

These constructs are designed so an HLAPI layer (Netcode, Mirror, FishNet, ...) can be
laid on top later without a rewrite, but the framework itself depends on no net library:

- **Data / logic split** is load-bearing: GameMode is logic, GameState/PlayerState are
  plain serialisable data with no behaviour, so a replicator only ever touches data.
- **Authority** is a declared property per construct (`Server` / `Owner` / `Client`); the
  framework declares it, the net adapter enforces it.
- A light **replication seam** (a `Revision` bumped on mutation) lets a replicator diff;
  the real serialise/delta contract is defined when an adapter is built.
- The framework is the lowest layer: it must **not** depend on any net library **or on
  DataLens**. DataLens-backed GameState/PlayerState and HLAPI replicators live above it.

### 3. Settings + Codegen

A JSON settings framework (Newtonsoft, not `JsonUtility`) that locates, reads and writes
a plain serialisable object from one of three locations:

- `ProjectSettings/<Tool>.json` (project-wide standard library; not in builds)
- a top-level `Project/<Tool>/` folder (outside `Assets/`)
- anywhere in `Assets/`, located by **AssetDatabase GUID** so the file can be moved
  freely and the framework still finds it

Authoring source location is separate from runtime delivery: `ProjectSettings/` and loose
`Assets/` JSON are not loaded at runtime, so a settings type declares whether it is
build-time-only (consumed by codegen, the baked output runs) or runtime-readable
(delivered via bake or a loadable artefact).

A generator registry carries a descriptor per settings type (location, delivery, and a
typed metadata provider), giving:

- a single build hook (`IPreprocessBuildWithReport`) that generates all registered
  generators before a build,
- on-demand generation,
- cross-tool metadata reflection, so e.g. the HATE Forge can read the GameplayTags
  vocabulary without re-scanning the project.

### 4. Editor undo for non-UnityObject data

`UndoHistory<T>` (`Heathen.Editor`) is a serialise-based undo/redo history for authoring data
that is not a `UnityEngine.Object` at edit time (where Unity's `Undo` can't be used). Each
`Push` stores a JSON snapshot (with the `UnityJson` converters); `Undo`/`Redo` return a fresh
copy. Reusable by any tool that edits a POCO/JSON model (Ogham graph editor, HATE Forge, ...).
The editor chooses Push granularity and wires its own Ctrl+Z / Ctrl+Y.

## Status

- **P1 (subsystem spine)**: done. Scope/attribute, `Subsystem` base, dependency-ordered
  Global registry + `GameFramework.Get<T>()`, reflection discovery, boot/teardown.
- **P2 (PlayerLoop tick dispatch)**: done. Six phases injected as children of FixedUpdate /
  Update; allocation-free dispatch; idempotent install, self-healing across
  enter-play-without-domain-reload, restored on quit / exiting play mode.
- **P3 (World + WorldManager)**: done. `World` container + `WorldManagerSubsystem` (Global);
  per-world subsystem discovery/dependency-order/lifecycle and tick (un)registration; hybrid
  boundary (auto convenience world after globals are up, explicit `CreateWorld` for multi-world);
  `world.Get<T>()`, `GameFramework.MainWorld` / `CreateWorld`; deferred tick add/remove queue so
  worlds can churn subsystems mid-tick safely; discovery cached per scope.
- **P4 (GameMode / GameState / PlayerState)**: done. Optional per-world structure. `Authority`
  enum + `Revision` change seam (contracts only, no net code). Data/logic split: GameState and
  PlayerState are plain data on the World (so they survive on a client with no GameMode);
  GameMode is server-only logic over them with both override hooks and register events.
  `world.SetState/SetMode/AddPlayer/RemovePlayer/GetPlayer`.
- **P5 (Settings locate + serialise)**: done. `[Settings]` attribute (Name/Location/Extension/
  Delivery/Folder) on a plain POCO; editor `SettingsStore.Load/Save/Exists/GetPath` reads/writes
  Newtonsoft JSON from `ProjectSettings/`, a top-level `Project/<Folder>/`, or anywhere in
  `Assets/` (located by AssetDatabase GUID, scanned by unique extension, cached; existing files are
  written back in place so moves are respected). Editor-only; runtime delivery is per-tool/P6.
- **P6 (generators + metadata)**: done. `ISettingsGenerator` (Name/Output/IsStale/Generate) +
  `SettingsGenerators` registry (GenerateAll/GenerateStale/StaleNames, type-discovered); one shared
  build hook that regenerates `RuntimeAsset` generators and guards `SourceCode` ones (fails the build
  if stale). Domain-agnostic `ISettingsMetadataProvider` + `SettingsMetadata.All<T>()/First<T>()` so
  tools read each other's metadata by contract without re-scanning. `Tools ▸ Heathen ▸ Game Framework`
  menu. Tools keep their own bake logic.

**v1 feature-complete.** Next work is retro-fitting the existing Foundations onto the framework
(settings relocation, tag ownership/prefix, subsystem registration).

Wired into the ToolkitSource project via a `file:` manifest ref for compile/verify.
