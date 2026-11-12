# Phase 8 cut-over checklist

Prepared on 2026-11-11. **Nothing below has been deleted.** Phase 8 runs only after the new game has been tested
side by side with the old one **and the team explicitly approves the deletion** (see CLAUDE.md).

## Already done (non-destructive prep)

- `Assets/_Project/Scenes/Game.unity` is build scene 0. `Pinball.unity` stays enabled at index 1 until cut-over,
  because the old game's restart reloads its own build index.
- **Pinball > Build Web Game** (`_Project/Scripts/Editor/WebGLBuild.cs`) replaces the old `LuckyPinball/Build Web Game`.
  It builds `Game.unity` into `Builds/WebGL` with the old web settings (template `LuckyPinball`, decompression fallback,
  1280x720, quality level 0 with `Mobile_RPAsset`) and restores the editor's quality level and render pipeline
  afterwards. Needs the **WebGL Build Support** module, which isn't installed on the machine that prepared this.
- **The new game builds and runs without the old code.** A Windows64 build of `Game.unity` only succeeded (79 s,
  110 MB, 0 errors) and ran headless for 12 s with no exceptions in the player log.
- **Reference scan.** Every asset outside `_Project`, `Plugins` and `TextMesh Pro` was checked against everything
  in `_Project`, `ProjectSettings` and `Packages`. The new game references none of the old game's assets, and no new
  code names an old type. The parity tests use captured reference data (`Tests/*/Golden`), not the old code.

## Delete in Phase 8 (old game only)

| What | Why it's safe |
|---|---|
| `Assets/Scripts/` (everything left; `Feedback/` already moved to `_Project`) | Old runtime, its editor tools, the `LuckyPinball/*` Check and builder menus, and the old `WebGLBuild.cs`. Nothing in `_Project` references it. |
| `Assets/Scenes/Pinball.unity` | The old scene. Remove it from the build list in the same change. |
| `Assets/Scenes/Mat_2D303CFF/A9ADB8FF/F2D33AFF/FFFFFFFF/OuterGutter/PoweredReturn/ReturnBand.mat`, `Assets/Scenes/BallPhysics.physicMaterial` | Used only by `Pinball.unity`. The new game uses its own copies in `_Project/Art`. |
| `Assets/link.xml` | Only preserves `CapsuleCollider` for the old enemy's `CreatePrimitive(Cylinder)`; the new game creates no primitives at runtime. |

## Keep

| What | Why |
|---|---|
| `Assets/InputSystem_Actions.inputactions` | The new game's input (flippers, pause, restart); also the project-wide actions asset. |
| `Assets/Settings/` | URP pipeline, renderers and global settings, referenced by Graphics and Quality settings. |
| `Assets/WebGLTemplates/LuckyPinball` | The web build template (used by name; Unity requires this folder location). |
| `Assets/StreamingAssets`, `Assets/Plugins`, `Assets/TextMesh Pro` | FMOD, Odin, TMP. |
| `Docs/LuckNumberPrototype.md`, `Docs/LuckNumberV03Balance.md`, `Docs/TuningMigration.md`, `Docs/ArchitecturePlan.md` | Design source and migration record. |

## Not part of the old game (decide separately)

- `Assets/Scenes/SampleScene.unity`, `Assets/Readme.asset`, `Assets/TutorialInfo/`: leftovers from the URP project template.
- `Assets/Screenshots/`: screenshots saved by the Unity MCP tools, committed to git. Future screenshots should go to `Temp/`.

## Phase 8 steps (after approval)

1. Commit the new game first, so the deletion is its own revertable commit.
2. Delete the items in "Delete in Phase 8"; remove `Pinball.unity` from the build list.
3. Refresh, compile, and check the Console for errors (a missing-script warning would mean something still pointed at old code).
4. Run all EditMode and PlayMode tests.
5. Build (Windows, and WebGL where the module is installed) and run the build.
6. Update docs: in CLAUDE.md, the "replace, don't refactor" and "old rule checks" sections; in ArchitecturePlan, mark the roadmap done.
