# v0.5 + multiball / side kickers integration

Branch: integration/v05-multiball-side-kickers

Local v0.5 source was preserved in dc7e711 before merging feature/multiball-side-kickers.

## Rules

- No lucky-number kill ball reward. The obsolete field and scene value are removed.
- All elements, waves and burn ticks in one DiceBatch share its AttackRecord. Only the transition to two kills attempts one extra ball.
- Direct collision damage and all enchantments caused by that collision share one AttackRecord, including later burn ticks. Independent collisions have independent records.
- At most three balls including pending rewards; attempts at capacity are consumed, not banked. Extra drains do not cost a life while another ball remains.
- v0.5 recharge remains shared across balls and sources: base 100, +50 per committed card, natural 10/s, eligible hit +2.5, enemy cooldown 1s, rolling 16/s cap; cards refill after collision enchantments resolve.
- Full side walls kick; old powered side-return lanes are inactive. Enhanced enemy rebound is retained.
- Logs and GameJam/tmp are untracked and ignored; existing local files remain on disk.

## Validation performed in Unity 6000.6.8f1

- Runtime and Editor scripts compiled; final console query returned zero errors. Existing FMOD PluginImporter version warnings remain.
- LuckPrototypeCheck.Run passed in Play Mode: exhaustive dice/card checks, recharge randomized checks, v0.5 batch effects, immunity, snapshots, pause gates and shared charge thresholds.
- MultiballChecks.Run passed in Play Mode: direct + enchantment two kills; delayed passive burn completing the same collision; five kills in one batch rewarding one ball; no repeat reward after freeing a ball slot; pending-inclusive limit; hard three-ball cap despite inspector override.
- Live scene checks passed: WallL and WallR have whole-wall Slingshot components, both SideReturn objects are inactive, normal enemy bounce minimum is enabled.
- Fixed a pre-existing TickGameplay boundary issue discovered by the acceptance run: the epsilon loop gate skipped the last fraction of a time step, leaving charge just below 250 after 25 seconds. The loop now consumes the complete requested interval; all existing recharge acceptance checks pass without changing recharge values.
- git diff --check passed. No push performed. No claim of manual playtesting or physical balance calibration.

Re-run in Play Mode using LuckyPinball > Luck Number > Run Acceptance Checks and Check Multiball Integration.
