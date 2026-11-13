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

Design scope: rebuild exactly what is in the engine now (**Luck Number v0.3**). The "mystery circles", special/bumper enemies and energy from the old brief are out of scope.

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

- Phases 0–5 are done: prep, `Pinball.Core` rules, data assets (`_Project/Data/`), board/ball/flippers/lanes/run loop (`_Project/Scenes/Game.unity`), enemies (`Enemy_Base.prefab`, registry, spawner, march, score), and cards & combat (`HandSystem`, `LuckSystem`, `CastScheduler`, `CombatResolver`, `RunStats`; effect modules in `Scripts/Gameplay/Effects`). Phase 6 (UI) is done: `Prefabs/UI/Hud.prefab` (uGUI + TMP, 1920x1080, presenters in `Scripts/Presentation/Hud`, colours in `Data/Config/UiTheme.asset`, font Londrina Solid in `Art/Fonts/LondrinaSolid` with Black as the bold weight and LiberationSans as fallback for `→`/`×`), drag-to-play via `HandView`/`CardView`, drops checked against the board's `PlayfieldArea`. Phase 7 (feedback) is done: gameplay raises `CombatFeedback` events (`context.Feedback`), and `CombatFeedbackPlayer` (scene object `Feedback`) plays each element's cues (`passiveHitVfx/Sfx`, `activeHitVfx/Sfx`, `castSfx`) plus status/run cues. `VfxCue` is pooled and takes a `VfxRequest` (from/radius/strong/board frame); effect prefabs in `Prefabs/VFX` (LineRenderer bolt/ring/flame), cue assets in `Data/Feedback`, ball auras from `ElementDefinition.ballAuraPrefab` via `BallAuraView`. FMOD: launch/bounce on Ball, creature hit on Enemy_Base (UltEvent hooks to `SfxPlayer`), life lost in the feedback player, `MX_Combat` music on the camera. Next: Phase 8 (cut-over) — only after side-by-side testing and the user's explicit approval. The prep is done and the exact delete/keep list is in [`GameJam/Docs/CutoverChecklist.md`](GameJam/Docs/CutoverChecklist.md); web builds use **Pinball > Build Web Game**.
- Ported from the old `feature/multiball-side-kickers` branch: `MultiballSystem` (`context.Multiball`; extra ball on a lucky-number kill or an attack's 2nd kill, cap and entry timing in `GameConfig.multiball`, entry anchor `ExtraBallEntry` on `Board.prefab`), `Kicker` on `WallL`/`WallR` (extended to z 1.15 / length 11.7, `Art/Materials/Kicker.mat`), and `EnemyDefinition.hitBounceSpeed` (pop-bumper push in `EnemyContact`). The return lanes are **disabled** in `Board.prefab` (re-enable `ReturnLaneL/R` to bring them back); `BoardPhysicsParityTests` switches them on because the reference paths were recorded with them.
- Art pass (branch `art-integration`):
  - **HUD.** Mockup v2 layout in `Hud.prefab`:
    - palette: text F3D036 / CA50AC / 63F9F9, outline 961761 via the `LondrinaSolid-* SDF Outline` materials
    - background illustration: `Prefabs/UI/Backdrop.prefab`, a ScreenSpaceCamera canvas
    - lives top left as cupcake silhouettes: `TopBarView.hearts`, one per `GameConfig.lives`, laid out by a HorizontalLayoutGroup
    - SCORE top centre, run TIME top right
    - COMBO on the right, with a gradient count and hype lines (`ComboView.hype` thresholds). `ComboView` reads `context.Streak` (`HitStreak`), which resets only when the last ball drains (`GameContext.LastBallDrained`)
    - smaller LUCKY/NEXT tiles under the lives: `LuckPanelView`, reading `LuckSystem.Next`
    - RULES panel inactive
    - a flat row of large cards, cut off at the bottom edge and tilted back by `CardPerspective` (a UI mesh effect on each card Image, tapering toward the hand's centre). A click selects a card (it rises, shows `CardView.hint` and previews matching enemies); a second click within `HandView.selectSeconds` casts it. Drag-to-board still works.
  - **Cards.** Portrait card art comes from `ElementDefinition.cardArt` (index 0 = `minNumber`).
  - **Next lucky number: deliberate divergence.** `LuckSystem.Next` is rolled one change ahead, which moves that draw earlier in the shared random stream. Seeded runs no longer replay the old prototype's roll order (decision 7 no longer holds there; see `TuningMigration.md`).
  - **Board.**
    - The cabinet model `Art/Models/PinballMachine_UVed.fbx` is `CabinetVisual` on `Board.prefab`, textured with `Art/Materials/Cabinet.mat` (Toon + `Textures/Board/PinballMachine_UVed_BaseColor.png`). It is visual only, and physics still uses the primitives (some of their renderers are hidden).
    - `Editor/SmoothNormalsPostprocessor` bakes averaged normals into UV7 of every model in `Art/Models` for the outline. It adds no geometry.
    - The flippers' `Bat/Visual` meshes are the model's teardrop pieces (`polySurface1/2`, textured with `Cabinet.mat`), fitted hinge-to-tip on the 1.9 m bat, with a navy pivot pin (`Bat/Pin`, `FlipperPin.mat`, no collider). The model's own static copies under `CabinetVisual` are hidden, and the flipper colliders are unchanged.
    - The Kicker `Visual` children sit 0.2 m inward of their colliders, so they don't z-fight with the cabinet wall.
  - **Look.**
    - `Art/Shaders/Toon.shader` (`Pinball/Toon`): two-tone lighting plus an inverted-hull outline. It keeps `_BaseColor`, so `EnemyView`'s property-block tint still works. The board, ball, flipper, kicker and enemy materials use it.
    - `Art/Shaders/EdgeOutline.shader`: screen-space ink lines from depth and normals, run by a FullScreenPassRendererFeature on `_Project/Settings/Game_Renderer.asset`.
    - `Game_Renderer` is renderer index 1 in `Assets/Settings/Mobile_RPAsset.asset` and is used only by the `Game.unity` camera. The old scene keeps the default renderer.
    - Tune lines in `Art/Materials/EdgeOutline.mat`.
  - **Enemies.**
    - The monster sprites (`Art/Textures/Monsters`) replace the circles: `Enemy_Ball` / `Enemy_Bottle` / `Enemy_Cupcake` are variants of `Enemy_Base`, with the Ring/Cap renderers off and a `Monster` SpriteRenderer that has `FaceCamera`.
    - `EnemyView.body` tints the monster for preview, hit, burn and stun.
    - The enemy assets are `Grunt` (Ball), `Grunt_Bottle` and `Grunt_Cupcake`, all with the v0.3 stats, at equal weight in `SpawnProfile_Default`. Picking already drew a random value, so the random stream is unchanged.
    - Labels sit `EnemyLabelLayer.labelHeight` world metres above the enemy along the camera's up.
  - **Sound.** `Sfx_CreatureGetHit` plays on the enemy's `onBallHit`. There is no life-lost sound yet (no FMOD event). The danger line's renderer is off.
  - **Flippers.** They snap to rest on every launch (`Flipper.ResetToRest`), because time is frozen during the countdown.
  - **Test hygiene.** An interrupted PlayMode run can leave `Assets/InitTestScene*.unity` open as the active scene; pressing Play there re-runs the tests ("the game plays itself"). Reopen `_Project/Scenes/Game.unity`. These files and `Assets/Screenshots/` are git-ignored.
- Rules parity is proven by tests: `CardRulesParityTests` replays every scenario of the old `LuckPrototypeCheck` in `Game.unity`. Tests take control of time with `GameContext.DriveTimeManually` and step `Clock.Advance` themselves. `DebugCheats` (on the GameContext object) sets the lucky number, forces the next Q, adds cards and spawns enemies in play mode.
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
7. **Tests:** Unity Test Framework (NUnit), not editor "Check" menus. Test assemblies can't reference the old code. Parity with the prototype is checked against reference data captured from it (`Tests/EditMode/Golden/LegacyLuckV03.json`, generated by running the old `LuckTuning` through `execute_code`). `Tests/EditMode/LegacyV03.cs` holds the v0.3 tuning in Core types; Phase 2's data assets must match it. All randomness goes through `Pinball.Core.IRandom` (`UnityRandom` reproduces the old rolls for the same seed).

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
