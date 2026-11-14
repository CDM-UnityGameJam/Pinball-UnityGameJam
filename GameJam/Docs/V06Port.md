# Luck Number v0.6 / v0.7 port (from integration/v05-multiball-side-kickers)

## v0.7 "lucky groups" (integration up to `001ed3b`)

The dice are gone. The rebuild now plays the rules from `7d388bf` (`HandRules`, `HandInteractionRules`, `Docs/LuckyGroups.md`
on that branch); the sections below about dice, tiers and empowered hits describe v0.6 and are kept for history.

| v0.7 rule (old code) | Rebuild |
|---|---|
| Card numbers 1-3; hand sorted by number, stable (`HandOrder.Sort`) | `DeckConfig.numberWeights`, `GroupRules.SortByNumber`, `HandSystem.Sort` |
| A cast is a whole group: every held card of one number (`DiceRules.ValidGroup`, `UseCard`) | `GroupRules.ValidGroup`, `HandSystem.TryPlay(slot / cards)` |
| Free full opening hand, no lucky number; refills draw every empty slot and pick luck with p = 0.2 per new card unless the lucky number is still held (`DrawToFull`, `PickLucky`) | `HandSystem.DrawToFull`, `GroupRules.PickLucky`, `DeckConfig.luckyProbability` |
| Lucky group casts Lucky skills, others Normal; casting is immediate; last lucky card cleared after the cast is captured | `CastBatch.tier`, `ElementDefinition.normal / lucky`, `CastScheduler.Cast` |
| Refill cost by empty slots 100/250/450/700/1000, progress kept across casts, cleared on refill; natural 3/s, contact +10 | `DeckConfig.refillCosts`, `Core/ChargeAccount.cs` |
| Held lucky cards: Arc +2 damage, burn +1 DPS, stun +0.25 s each (`HeldArcDamage`, `HeldFireDps`, `HeldStun`) | `luckyDamageBonus` / `luckyDpsBonus` / `luckyStunBonus` on the passive modules |
| Empowered hits removed; ball damage always 4 | `CombatResolver` |
| Difficulty: base health 20, descent 50 s, spawn every 3 s, new stage multipliers (`DefaultDifficulty.asset`) | `Grunt.asset`, `SpawnProfile_Default.asset` |
| Drag a group out of the hand to cast; keys 1/2/3 select, double-tap within 0.3 s casts (`GroupKeyInput`) | `HandView`, `Core/GroupKeyInput.cs`, `SelectGroup1-3` actions |

Not ported from v0.7: the Excel workbook changes and the F8 panel (as before).


The rebuild now plays the rules implemented on `integration/v05-multiball-side-kickers` (up to `920da67`), not v0.3.
Source of truth was that branch's **code, `Pinball.unity` and `Settings/DefaultCardBalance.asset` / `Settings/DefaultDifficulty.asset`**;
where its docs disagree (for example `Difficulty.md` says base enemy health 40, the asset says 9), the assets won.

## Rules: where they live now

| v0.6 rule (old code) | Rebuild |
|---|---|
| Dice batches: 1–5 same-number cards, one die each; Base / Hit / Full Field / Jackpot (`DiceRules`, `DiceBatch`) | `Core/DiceRules.cs`, `Gameplay/Cards/DiceBatch.cs`, `HandSystem.TryPlay` |
| Reveal after 0.45 s, one batch at a time, targets locked at play, own-element foes excluded (`RevealBatch`, `SkillCast`) | `CastScheduler`, `GameConfig.diceRevealDelay` |
| Per-tier skill rows + extra copies (+1 Arc wave, +2 s Spark burn), Shockwave one impact (`LuckTuning.Plan`) | `ElementDefinition.tiers`, `extraWavesPerCopy`, `extraBurnSecondsPerCopy`; `Core/SkillPlanner.cs` |
| Held effects combine by element, scaled by cards held (5-row tables) (`EquipmentResolver.BallHit`) | Effect modules (`HeldIntTable` / `HeldFloatTable`); Arc also hits the direct foe |
| Enemy elements and immunity (`Enemy.Element`, `CanReceive`) | `Enemy.Element`, `Enemy.CanReceive`, `EnemyDefinition.elementRule` |
| Fractional burn DPS with a per-burn accumulator (`BurnDamageAccumulator`) | `Core/BurnStatus.cs` |
| Charge refill: base 100, +0 per card, natural 2/s, +13 per new contact, uncapped (`RechargeAccount`) | `Core/ChargeAccount.cs`, `DeckConfig` charge fields, `HandSystem` |
| New-contact tracking independent of the 0.2 s damage cooldown (`BeginBallContact`) | `EnemyContact.BeginContact / TryContact` |
| Empowered hits: +1 per card played, next damaging hit deals 8 | `HandSystem.EmpoweredHits`, `CombatResolver`, `GameConfig.empoweredHitDamage` |
| Empty opening hand | `StarterDeck.startingHand` is empty |
| Difficulty stages: spawn / move / health multipliers over combat time; new-enemy health = round(9 × mult) (`DifficultyRules`, `SpawnProgress`) | `Core/Difficulty.cs`, `SpawnProfile.stages`, `EnemySpawner`, `EnemyMarch` |
| Breach-only lives; drains relaunch after 3 s of combat time (`OnDrain`, `TickRelaunch`) | `GameContext.OnDrained`, `Run/BallRelaunch.cs` |
| Every life lost clears the field without rewards (`ClearFieldEnemies`) | `GameContext.OnLivesLost`, `EnemyRegistry.DespawnAll` |
| Time points (5 every 5 s) and combo tiers (3 s timeout; game over pays) (`ScoreRules`, `ScoreAccount`) | `Core/ComboScore.cs`, `ScoreSystem`, `GameConfig.scoring` |
| Multiball: only an attack's 2nd kill; no lucky-number reward; hard cap 3 | `MultiballSystem` |

## Board, ball and audio

- `BallLaunchPoint` moved to board (0, 0.555, −7) (scene `launchLocal`).
- New `Geometry/TopDeflector` on `Board.prefab`: spring post at (0, 0.57, 11.9), radius 0.5, kicks the ball straight down the table at 11 m/s (`Deflector`).
- One opening enemy (scene `openingNormalCount`).
- Ball wall sounds follow `PinballCollisionAudio`: side-facing contacts only, ≥ 1 m/s into the surface, never the playfield, deflector or other balls; a held-up flipper plays the launch cue (`Ball.onFlipperShot`).
- New cues `Sfx_CardsPlayed` (`SFX_ui_card_powerUp`), `Sfx_Score` (`SFX_ui_score`), `Sfx_EnemyContact` (`SFX_ctr_getHit`) on `CombatFeedbackPlayer`. FMOD banks taken from the integration branch.

## HUD

Click-to-select cards (number lock, blocked cards greyed, PAIR tag), **Cast N · Roll N dice** button, selection preview (upgrade chance, targets, plans per tier, charge cost),
dice reveal panel, charge bar, `BALLS · POWER · element ×n` line, combo line, RELAUNCH countdown, element symbols and colours on enemies, help text from the configs.

## Not ported

- The Excel workbook and its importer (`ConfigWorkbookReader/Import`): they write the old `CardBalanceConfig` / `DifficultyConfig` assets. The same numbers are edited on the rebuild's data assets.
- The F8 difficulty debug panel, editor preview-time launch and `DifficultyTelemetry` rolling stats.
- The rolling combat-charge journal (kept only as a statistic in v0.6).
- Shockwave's symbol is `○` instead of `◎` (not in the HUD fonts), and the Spark card text is shortened to fit the card.

## Tests

The v0.3 parity suite (`CardRulesParityTests`, `LegacyV03`, `Golden/LegacyLuckV03.json`, Q-band and refill-clock tests) is replaced by
`SkillPlannerTests`, `ChargeAndScoreTests`, `DataAssetTests` (EditMode) and `CombatRulesPlayModeTests` (PlayMode), which port the v0.6 acceptance checks
(`LuckPrototypeCheck`, `V06Checks`, `MultiballChecks`, life/relaunch, field clear, scoring, empty hand / direct Arc).
