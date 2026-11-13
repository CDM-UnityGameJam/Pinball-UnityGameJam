# Lucky Pinball — project guide for Claude

Unity **6000.6.8f1** (URP) project in `GameJam/`. We are **rebuilding the game from scratch** into a designer-adjustable architecture. The full plan is in [`GameJam/Docs/ArchitecturePlan.md`](GameJam/Docs/ArchitecturePlan.md). Read §4 (target architecture), §5 (roadmap) and §6 (decisions) before starting any phase.

## The core rule: replace, don't refactor

- The **new game** lives only in `GameJam/Assets/_Project/`, with its own scene (`_Project/Scenes/Game.unity`), asmdefs and namespaces.
- The **old game** is `GameJam/Assets/Scripts/` plus `Assets/Scenes/Pinball.unity`. It is AI-generated: a ~12-responsibility `GameManager` singleton, prefab-less runtime-built enemies/HUD/VFX, tuning stored on the scene object, and board geometry made by editor generator scripts.
- New code must **never reference, inherit from or copy the structure of** old classes (`GameManager`, `Enemy`, `Hud`, `Card`, `GameTuning`, `HudTuning`, `Spawner`, `EquipmentResolver`, `SkillCast`, …).
- **Do not modify or delete anything** in the old implementation (`Assets/Scripts/`, `Assets/Scenes/Pinball.unity`, its editor scripts and Check menus), not even dead code. It stays intact and playable for side-by-side comparison. It is deleted only after the new implementation has been tested against it **and the user explicitly approves the deletion** (Phase 8). Dead code listed in ArchitecturePlan §2.7 is simply not ported.
- Exception: `Assets/Scripts/Feedback/` (`SfxCue`, `VfxCue`, `SfxPlayer`, `VfxPlayer`, `Sfx`) is the pattern to follow. It **moves** into `_Project` (keep the `.meta` GUIDs).

## What gets ported (rules and feel, not code structure)

The old code is the reference for **behaviour only**. Port these into the new structure:
- Q-band rolls and the `SkillPlan` math (`LuckTuning` in `GameTuning.cs`)
- burn/stun stacking, target snapshotting, refill debt
- the combat clock (gameplay time that only advances while Playing; `TickGameplay`)
- enemy queue movement (`EnemyMovement.cs`)
- physics: flipper angles and speed (kinematic `MoveRotation`), ball physic material, continuous collision detection on the ball, **0.01 s fixed step**, and **gravity 180** (the value serialized in `Pinball.unity`; the code default of 60 is overridden).

Sources of truth, in order: **the live code and the values serialized in `Pinball.unity`** > `GameJam/Docs/LuckNumberPrototype.md` (and `LuckNumberV03Balance.md`). Where the code and the docs disagree, **the code wins**. `GameJam/Docs/Archive/` (the old prototype brief and parameter guide) is outdated; ignore it.

Design scope: **Luck Number v0.6** as implemented on `integration/v05-multiball-side-kickers` (dice batches, enemy elements, contact charge, difficulty stages, combo scoring, relaunch on drain). See [`GameJam/Docs/V06Port.md`](GameJam/Docs/V06Port.md) for where each rule lives and what was not ported. That branch's code, `Pinball.unity` and `Settings/*.asset` are the reference; the old game on this branch is still v0.4. The "mystery circles", special/bumper enemies and energy from the old brief are out of scope.

## Target architecture

Guiding rules: **prefabs own presentation, ScriptableObjects own numbers and content, code owns rules.** Gameplay never calls UI/VFX/audio; it raises events. No `static Instance`: one `GameContext` composition root per scene wires everything. Anything spatial is a Transform or Collider in the scene, never a typed-in coordinate. A new element, enemy or wave is a new asset, not a code change.

```
GameJam/Assets/_Project/
  Art/ Audio/ Data/{Config,Elements,Enemies,Spawning,Decks}/
  Prefabs/{Board,Ball,Enemies,UI,VFX}/   Scenes/Game.unity
  Scripts/
    Core/          Pinball.Core          pure rules, minimal UnityEngine, unit-tested, IRandom injected
    Feedback/      Pinball.Feedback      SfxCue/VfxCue (refs FMODUnity)
    Gameplay/      Pinball.Gameplay      systems + data SOs (refs Core, Feedback, InputSystem, UltEvents)
    Presentation/  Pinball.Presentation  views/presenters (refs Gameplay, UGUI, TMP, UltEvents)
    Editor/        Pinball.Editor        editor-only tooling (Odin)
  Tests/EditMode/  Pinball.Tests.EditMode
  Tests/PlayMode/  Pinball.Tests.PlayMode
```
Namespaces match the assembly names (`Pinball.Core`, `Pinball.Gameplay`, …). Dependencies only point downward: Presentation → Gameplay → Core.

## Progress

- Phases 0–5 are done: prep, `Pinball.Core` rules, data assets (`_Project/Data/`), board/ball/flippers/lanes/run loop (`_Project/Scenes/Game.unity`), enemies (`Enemy_Base.prefab`, registry, spawner, march, score), and cards & combat (`HandSystem`, `CastScheduler`, `DiceBatch`, `CombatResolver`, `RunStats`; effect modules in `Scripts/Gameplay/Effects`). Phase 6 (UI) is done: `Prefabs/UI/Hud.prefab` (uGUI + TMP, 1920x1080, presenters in `Scripts/Presentation/Hud`, colours in `Data/Config/UiTheme.asset`, font Londrina Solid in `Art/Fonts/LondrinaSolid` with Black as the bold weight and LiberationSans as fallback for `→`/`×`), click-to-select and a Cast button via `HandView`/`CardView`, plus `SelectionPreviewView`, `DiceRevealView` and `CastPanelView`. Phase 7 (feedback) is done: gameplay raises `CombatFeedback` events (`context.Feedback`), and `CombatFeedbackPlayer` (scene object `Feedback`) plays each element's cues (`passiveHitVfx/Sfx`, `activeHitVfx/Sfx`, `castSfx`) plus status/run cues. `VfxCue` is pooled and takes a `VfxRequest` (from/radius/strong/board frame); effect prefabs in `Prefabs/VFX` (LineRenderer bolt/ring/flame), cue assets in `Data/Feedback`, ball auras from `ElementDefinition.ballAuraPrefab` via `BallAuraView`. FMOD: launch/bounce on Ball, creature hit on Enemy_Base (UltEvent hooks to `SfxPlayer`), life lost in the feedback player, `MX_Combat` music on the camera. Next: Phase 8 (cut-over) — only after side-by-side testing and the user's explicit approval. The prep is done and the exact delete/keep list is in [`GameJam/Docs/CutoverChecklist.md`](GameJam/Docs/CutoverChecklist.md); web builds use **Pinball > Build Web Game**.
- Ported from the old `feature/multiball-side-kickers` branch: `MultiballSystem` (`context.Multiball`; extra ball on an attack's 2nd kill, cap and entry timing in `GameConfig.multiball`, entry anchor `ExtraBallEntry` on `Board.prefab`), `Kicker` on `WallL`/`WallR` (extended to z 1.15 / length 11.7, `Art/Materials/Kicker.mat`), and `EnemyDefinition.hitBounceSpeed` (pop-bumper push in `EnemyContact`). The return lanes are **disabled** in `Board.prefab` (re-enable `ReturnLaneL/R` to bring them back); `BoardPhysicsParityTests` switches them on because the reference paths were recorded with them.
- Rules are checked by tests: `CombatRulesPlayModeTests` ports the v0.6 acceptance checks in `Game.unity`. Tests take control of time with `GameContext.DriveTimeManually` and step `Clock.Advance` themselves. `DebugCheats` (on the GameContext object) forces the next dice, adds cards, fills the charge and spawns enemies of a chosen element in play mode.
- Board pieces follow the collider-root + `Visual`-child rule. `Board.prefab` holds the anchors (`BallLaunchPoint`, `SpawnZone`, `DangerLine`, `DrainZone`) referenced from `BoardRoot`; `GameContext` is the composition root (`RunStateMachine`, `CombatClock`, `BallSystem`).
- Physics parity is measured, not eyeballed: run the same ball start in both scenes with `Physics.simulationMode = Script` and compare board-space paths. The playfield surface is the one non-primitive piece: `Art/Models/PlayfieldSurface.asset` (a MeshCollider; box joints made a fast ball hop). `BoardPhysicsParityTests` replays ball paths recorded in the old scene and must stay within 1 cm.
- [`GameJam/Docs/TuningMigration.md`](GameJam/Docs/TuningMigration.md) records where every old tuning value went, and lists the live scene values Phase 3/4/6 still need (board anchors, flippers, ball, lanes, enemy visuals, HUD).
- The game's entry asset is `Data/Config/GameConfig.asset`, which points to the luck, deck and spawn configs and the elements. Element behaviour is effect modules (`Scripts/Gameplay/Effects`): a new kind of effect is one new `IPassiveEffect` / `IActiveSkill` class.

## Agreed decisions (ArchitecturePlan §6)

1. **Board:** hand-placed primitives for now (exception: the playfield surface mesh, see Progress). Every board piece is a prefab with the **Collider on the root and a separate `Visual` child** (MeshFilter/MeshRenderer only) so the artist can swap meshes later. No generator scripts, no procedural meshes. `PinballSceneBuilder` / `OuterRingSurface` are not ported.
2. **Events:** plain **C# events** between systems; **UltEvents** (`com.kybernetik.ultevents`, embedded in `GameJam/Packages/`) for Inspector hooks on prefabs (`onHit`, `onKilled`, `onFlip`, …); **ScriptableObject event channels only for global events** (`RunStateChanged`, `LuckyNumberChanged`, `GameOver`).
3. **UI:** uGUI + TextMeshPro, built as prefabs in the Canvas. No code-built layouts.
4. **Odin Inspector:** runtime data may depend on it (`[SerializeReference]` effect modules, validation attributes, `[Button]` debug tools).
5. **Input:** `InputSystem_Actions.inputactions` with `InputActionReference` fields. Never poll `Keyboard.current` directly.
6. **Layers:** `Ball`, `Board`, `Enemy`, `Flipper`, `Trigger`, with a collision matrix. Don't use `CompareTag("Player")`.
7. **Tests:** Unity Test Framework (NUnit), not editor "Check" menus. Test assemblies can't reference the old code. `DataAssetTests` pin the data assets to the v0.6 values from the integration branch. All randomness goes through `Pinball.Core.IRandom` (`UnityRandom` reproduces the old rolls for the same seed).

## Working with Unity through MCP

The `UnityMCP` server (MCP for Unity, `mcpforunityserver==10.3.0` via `uvx`, registered in `.mcp.json`) talks to the open editor through the `com.coplaydev.unity-mcp` package. The editor must be open on `GameJam/` with the MCP bridge running.

- **Paths** in MCP tools are relative to the Unity project, e.g. `Assets/_Project/...`, not `GameJam/Assets/...`.
- **Check state before acting:** `manage_scene get_active` / `get_hierarchy`, `read_console`. If tools fail to connect, check that the editor is open and that the bridge is running (Window > MCP for Unity), and that `uvx` is on PATH.
- **After any C# change:** call `refresh_unity` (compile `request`, `wait_for_ready`), then `read_console` with types `error` and fix every compile error before continuing. New types only exist after a successful compile.
- **Move or rename assets** with `manage_asset` (`move`/`rename`) or `git mv` together with the `.meta`, so GUIDs and references survive. Never delete and recreate.
- **Scenes, prefabs and ScriptableObjects:** edit them through the MCP tools (`manage_gameobject`, `manage_components`, `manage_prefabs`, `manage_scriptable_object`), not by hand-editing YAML. Save the scene afterwards (`manage_scene save`).
- **Verify Unity APIs** with `unity_reflect` / `unity_docs` before using anything unfamiliar (Input System, URP, Unity 6 renames such as `linearVelocity` and `FindObjectsByType`).
- **Tests:** `run_tests` (EditMode / PlayMode), then `get_test_job` with `wait_timeout`.
- **Play-mode checks:** `manage_editor play`/`stop`, plus `manage_camera screenshot` for visual comparison with the old scene. Play mode keeps running while Unity is unfocused, thanks to `PlayerSettings.runInBackground` and `Pinball.Editor.BackgroundPlayMode`, which queues player-loop updates while the editor is in the background (toggle: **Pinball > Keep Play Mode Running In Background**). An `execute_code` call blocks Unity's main thread for about 1 s while it compiles, so don't measure frame timing across those calls.
- **Old rule checks** (reference only, until Phase 1 ports them): menu **LuckyPinball > Luck Number > Check Card Rules**, plus **Run Acceptance Checks (Play Mode)** in play mode. `Check Return Band` fails because the band is disabled in `Pinball.unity`. That's expected.
- Some tools (`manage_gameobject`, `manage_components`, `manage_script`, …) are deferred; load them with ToolSearch before use. Use `batch_execute` for repetitive edits.

## Housekeeping

- Known noise in the Console: FMOD `PluginImporter ... version 1` warnings, obsolete-API warnings in the old scripts, and a `UAC0005` warning in UltEvents. These are not ours to fix.
- Don't commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or the generated `.csproj`/`.sln` files.
