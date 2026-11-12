# Combat-time difficulty

Default asset: `Assets/Settings/DefaultDifficulty.asset`.
Scene: `Assets/Scenes/Pinball.unity`, GameManager > Difficulty > Difficulty Config.

The six default stages match the requested table. Legacy `spawnInterval` / `enemyDescendTime` fields and their scene values were removed; active runtime and developer measurement callers now use the configuration. Spawn geometry, opening count (3), probability, element rolls, clearance tests, enemy queues, stun, elemental immunity, multiball and recharge rules are retained.

## Inspector

Select the asset to edit base interval, base descent time, global speed multipliers and stages. The list supports add, remove, reorder and Duplicate selected stage. Duplicate preserves multipliers and places its time between neighbours (or 30 seconds after the final row). Errors identify the field/row; invalid configurations use the full default configuration at runtime without mutating the asset.

The asset Inspector previews any sampling time, attempts per minute, movement multiplier, speed and fixed-speed traversal duration. Its editable distance defaults to the current scene's 14.5 metres. Two plots show spawn and movement multipliers including configured totals. The GameManager Inspector uses the actual spawn/danger-line distance automatically.

On GameManager, set Preview / test time to 300 and press Play once at preview time. This is a one-shot editor launch request, consumed on entering Play Mode, rather than a saved runtime override. During Play Mode the same button applies the preview time immediately. A subsequent restart starts automatically at zero unless another one-shot launch is requested.

Duplicate the asset with Unity's Duplicate command and assign the copy to GameManager to maintain separate difficulty presets. Editing an asset during Play Mode is a persistent configuration edit.

## F8 panel (Editor / Development Build)

Focus the Game view and press F8 to toggle. The draggable panel displays combat time, difficulty sampling time/mode, interval, total movement multiplier/speed, living enemies, successful spawns/kills in the last 30 combat seconds, and the average refill interval for samples completed in that window (— when empty).

- Apply multipliers: temporarily **replace** the configuration's global spawn/movement multipliers. Stage multipliers continue to apply. Inputs must be finite and positive.
- Use specified time: fix only difficulty sampling to the entered non-negative time.
- Freeze difficulty: capture the current sampling time, whether automatic or specified.
- Restore automatic: use the current actual CombatTime immediately; temporary multipliers remain until reset.
- Reset temporary multipliers: return to the configuration's global values.

Panel actions never write the asset. A new GameManager/run clears temporary multipliers and time overrides. Pausing, launch waits and game over stop combat time and spawning progress. Losing life retains the current combat time. Reveal, burning, stun and recharge continue using CombatTime rather than the difficulty clock.

## Spawn phase and telemetry

Spawner advances normalized phase from GameManager's combat-time slices: phase += elapsed / current interval. Each whole phase consumes one attempt even when chance fails or placement is blocked. Live config/time changes retain the fractional phase; no blocked backlog accumulates. Opening spawns are separate from the zero-initialized phase.

Every living enemy reads one common speed per movement plan, then existing stun and front-to-back spacing constraints apply. The speed uses the current spawn-to-danger distance / configured base descent time.

Statistics use (CombatTime − 30, CombatTime]. Opening monsters count as successful spawns. A refill interval starts at the cast that first creates an empty slot, then at the preceding refill if more slots remain; only completed intervals appear in the rolling mean. Full-hand idle time is excluded.

## Verification

Passed:
- Runtime and editor script compilation against installed Unity 6000.6.8f1 assemblies (no errors; existing obsolete API / DEVELOPMENT_BUILD warnings remain).
- Standalone difficulty tests: interpolation and boundaries, 60-second 1.125 / 1.025 multipliers, invalid-field diagnostics/fallback, separate specified/frozen clock, total overrides and reset, speed changes retaining spawn phase, consumed failed attempts, rolling statistics.
- Default scene/config/script GUID references and removal of the old serialized timing fields.
- Existing exhaustive dice/selection checks and v0.6 recharge checks, including 30,000 combat-charge events.

| Time | Spawn interval | Movement multiplier | Speed with 14.5m path |
|---|---:|---:|---:|
| 0s | 2.000s | 1.000x | 0.3625m/s |
| 60s | 1.778s | 1.025x | 0.3716m/s |
| 300s | 1.000s | 1.250x | 0.4531m/s |
| 480s+ | 0.800s | 1.400x | 0.5075m/s |

Pending native editor validation:
Unity MCP reported no editor session. A hidden batch launch (`DifficultyChecks.RunBatch`) exited with code 1 before producing a test result. The log connected to the licensing client but did not identify a definitive failure cause; see `tmp/difficulty-unity.log`. Play Mode execution, real collision regression, Inspector layout and F8 visual interaction have therefore NOT been verified.

Integration checks are ready under `LuckyPinball > Difficulty > Run Integration Checks (Play Mode)`. They cover opening count, pause/wait phase gates, live config edits, blocked spawning/recovery, specified/frozen time, stun/queue/independent lanes, burn/reveal clocks and the existing v0.6 combat acceptance suite. Pure tests are available under `LuckyPinball > Difficulty > Check Rules`.

## New enemy health scaling

The active difficulty asset includes baseEnemyHealth and a per-stage healthMultiplier. These values affect only enemies born after a change; current enemies keep their HP and maximum HP. Health samples the same automatic, specified, or frozen difficulty time as the other difficulty rates, then rounds the product to the nearest whole point (halves upward), with a minimum of 1.

The Chinese workbook includes base HP and health multiplier inputs and a calculated spawn-HP column. Its current difficulty schema is v2. Editing the old hidden EnemyTuning health value no longer affects ordinary enemies.
