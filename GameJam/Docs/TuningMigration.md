# Tuning migration (Phase 2)

Where every tuning value of the old game ended up. Values were read from the live `GameManager` in `Assets/Scenes/Pinball.unity` on 2026-11-10 by a one-off `execute_code` run (no project file references the old code). `Tests/EditMode/DataAssetTests.cs` locks the assets to these values.

The scene overrides some old code defaults. Those rows are marked **scene**, and the scene value is the one that plays.

## Moved into data assets (`Assets/_Project/Data/`)

| Old field | New home | Value |
|---|---|---|
| `maxLives`, `drainLifeCost`, `breachLifeCost`, `launchWait` | `GameConfig` lives / costs / launchCountdown | 3 / 1 / 1 / 3 s |
| `launchSpeed`, `launchBiasDegrees` | `GameConfig` launchSpeed / launchSpreadDegrees | **scene** 32 (code 18) / 8° |
| `luck.collisionDamage` | `GameConfig.ballHitDamage` | 4 |
| `luck.burnTickInterval`, `luck.passiveStunImmunity` | `GameConfig.status` | 0.5 s / 2 s |
| Lucky numbers 1–6 (hard-coded), `luck.luckyRefreshInterval` | `LuckConfig` min/max number, refresh | 1–6, 10 s |
| `LuckTuning.MinResult/MaxResult`, `normalWeights`, `luckyWeights` | `LuckConfig.bands` | Backlash −10..−1 (25/10), Low 0..19 (45/25), Medium 20..59 (25/40), High 60..100 (5/25) |
| `luck.shortCircuitMin/Max` | `LuckConfig` short circuit | 2 s / 4 s |
| `luck.low/medium/high` (`StrengthBand`) | each `ElementDefinition.bandScaling` | burn snapped to 0.5 s, stun to 0.1 s |
| `passiveArc*`, `ArcTargets()` | `Arc` → `ChainDamagePassive` | 3 m, 4 dmg, targets 2 + 1 per extra card, max 5 |
| `activeArcDamage`, `arcWaveInterval` | `Arc` → `WaveDamageSkill` | 12 / 0.25 s |
| `passiveSpark*`, `passiveBurn*`, `ExtraSparkTargets()` | `Spark` → `IgnitePassive` | 2.5 m, 3 s, 4 DPS, extra 0 + 1 per extra card, max 3 |
| `activeBurnDps` | `Spark` → `BurnSkill` | 6 DPS |
| `shockRadius`, `shockRadiusPerCard`, `passiveStunDuration` | `Shockwave` → `StunPulsePassive` | 1.2 m + 0.25 per extra card, 0.35 s |
| `activeShockDamage`, `shockWaveInterval` | `Shockwave` → `StunWaveSkill` | 8 / 0.35 s, stun on first wave |
| `Card.Name/SkillName/Description`, `CombatVfx` element colours | `ElementDefinition` text and colour | Arc #1A8CFF, Spark #FF4004, Shockwave #59E6FF |
| Starting hand in `GameManager.Awake`, forced `handSize = 5` | `DeckConfig` | 6 Spark, 6 Shockwave, 2 Arc, 4 Arc, 5 Spark; size 5 |
| `luck.refillInterval`, `luck.playRefillDelay` | `DeckConfig` refill | 8 s / 4 s |
| `Card.Random()` | `DeckConfig` element and number weights | all 1 (uniform) |
| `normalEnemy.health/radius/hitCooldown`, `normalKillPoints` | `Grunt` (`EnemyDefinition`) | 40 / 0.75 / 0.2 s / 50 |
| `enemyDescendTime`, `luck.enemyQueueGap` | `Grunt` march | 40 s / 0.1 m |
| `normalEnemy.*Gap`, `*Clearance` | `Grunt` spawn spacing | 0.45 / 0.35 / 0.45 / 0.3 |
| `normalEnemy.unstick*` | `Grunt.unstick` | **scene** 5 m/s, 20 m/s² (code 2 / 3); bias 0.35 / 0.15 |
| `spawnInterval`, `spawning.normalChancePercent`, `openingNormalCount`, `openingColumnSpacing`, `normalAttempts` | `SpawnProfile_Default` | **scene** 2 s (code 5) / 96 % / 3 / 2 m / 30 |

Card draw seed parity: the old `Card.Random()` drew the number, then the element, with `Random.Range`. The weighted draw in Phase 5 won't reproduce those exact seeded sequences. Only the uniform odds are kept.

## Deferred to later phases (not data-asset numbers)

**Phase 3: board anchors and prefabs** (spatial values become Transforms and Colliders, §4.5):

| Old value (scene) | Becomes |
|---|---|
| `launchLocal` (0, 0.455, −9) | `BallLaunchPoint` |
| `spawning.normalSpawnZ` 9.5, `boardHalfWidth` 6.5, `arcCenterZ` 7, `arcRadius` 3.6, `arcHorizontalRadius` 6.5 | `SpawnZone` |
| `spawning.dangerZ` −5, danger line scale (16, 0.02, 0.08), offset (0, 0.01), colour #E0505E | `DangerLine` (+ visual child) |
| `spawning.ballRadius` 0.375 | the Ball prefab's collider (scale 0.75 × radius 0.5) |
| Board tilt 353.5° on X (−6.5°) | `Board.prefab` root rotation |
| Flippers: rest ±25°, up ∓25°, 1800°/s; pivots at (±2.3, 0.25, −7.5) | `Flipper.prefab` (+ input action) |
| Ball: mass 0.1, drag 0, angular drag 0.05, interpolate, continuous dynamic; `BallPhysics` material: bounce 0.5, friction 0.05/0.05, combine Minimum / Maximum | `Ball.prefab` |
| Side return lanes: travel 1 s, exit 15 m/s, re-entry cooldown 1.2 s, exit directions (±0.84, 0.54), 6 path points each | `ReturnLane.prefab` |
| Physics: 0.01 s step, gravity −180 | already in Project Settings (Phase 0) |

**Not active in the live game:** `SpringL`/`SpringR` (radius 0.45, kick 11) and the `ReturnBand` exist in `Pinball.unity` but are **disabled**. **Decision (2026-11-10): springs and the return band are skipped for now** and not rebuilt in Phase 3. Their values are kept here in case they come back.

**Phase 4: enemy prefab**: `normalEnemy` visuals (halfHeight 0.25, centerHeight 0.25, cap position/scale, flash 0.12 s, ring/fill/match/preview/flash/popup colours) go on `Enemy_Base.prefab` and its materials.

**Phase 6: UI**: `HudTuning` disappears; layout moves into Canvas prefabs, and colours into a `UiTheme` asset.

## Dropped (dead in v0.3, see ArchitecturePlan §2.7)

Special and bumper enemies (`specialEnemy`, `bumperEnemy`, `special*`/`bumper*` spawn fields, `specialTargets`), `normalTargets`, `CardTuning`, energy, `hitEnergy`, `pulseDamage`, `revealDuration`, `lifetime`, `kickSpeed` on enemies, and `EnemyTuning.hitDamage` (never used; ball hits use `collisionDamage`).

## Phase 3 status

All board, ball, flipper and lane values in the Phase 3 table above are now in `Board.prefab`, `Ball.prefab`, `Flipper.prefab` / `Flipper_Right.prefab` and `ReturnLane.prefab`. Materials and the ball physics material were copied into `_Project/Art`.

**Playfield surface:** the old surface (flat at y 0.32 from z -4, smooth ramp down to the floor over z -7..-4) is saved as a static mesh asset, `Art/Models/PlayfieldSurface.asset`, copied vertex for vertex from the old generated mesh, and used as the board's MeshCollider plus its `Visual`. No generator script exists in the project.

Box versions were tried first and rejected, because a fast ball hops where separate colliders meet. Distance from the old ball after 0.5 s / 1 s:

| Surface | Launch straight 32 m/s | Launch 8° | Drop near top | Roll onto left guide |
|---|---|---|---|---|
| 3 box segments | 0.22 / 0.40 m | 0.14 / 0.30 m | 0.02 / 0.17 m | 0.07 / 0.50 m |
| 8 box segments | 0.32 / 0.54 m | 0.68 / 1.25 m | 0.02 / 0.17 m | 0.02 / 0.03 m |
| 1 straight box | 0.17 / 0.30 m | 0.18 / 0.37 m | 0.02 / 0.17 m | 0.06 / 0.14 m |
| **Mesh (used)** | **0.000 m** | **0.000 m** | **0.000 m** | **0.000 m** |

`Tests/PlayMode/BoardPhysicsParityTests` replays the old paths (`Tests/PlayMode/Golden/LegacyBallPathsV03.txt`) and fails beyond 1 cm.

## Phase 4 status

- Enemy visuals moved to `Enemy_Base.prefab`: hitbox (convex cylinder 1.5 x 0.25 x 1.5), ring and cap meshes, `centerHeight` 0.25, and `EnemyView` colours (ring #C9475A, cap #5A2632, flash white for 0.12 s, burn (1, 0.32, 0.03), stun (0.3, 0.7, 1)). Health-bar labels and popups wait for Phase 6.
- `SpawnZone` / `DangerLine` / `SpawnBlocker` (one on each return-lane outlet, radius 0.7) replace `normalSpawnZ`, `dangerZ`, the arc numbers and `SideReturnLane.BlocksSpawn`.
- Measured against the old scene with the same enemy placement and launch: the ball's path and the damage dealt are identical (0.000 m difference) for a head-on and an off-centre hit.
