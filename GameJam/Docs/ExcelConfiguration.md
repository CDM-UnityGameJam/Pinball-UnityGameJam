# Excel configuration workflow

The delivered workbook is under `outputs/luck-number-excel-20261111/`. Its visible parameter names, units, options and usage guide are in Chinese. The Unity assets, importer scripts, menus and controls remain in English.

## Editing and importing

1. Edit yellow cells in the workbook. Keep unused gray cells blank. Use values in imported input cells, not formulas.
2. Save the workbook as `.xlsx`.
3. In Unity, open `LuckyPinball > Configuration > Import Excel Workbook...`.
4. Browse to the workbook and click `Validate workbook`.
5. Confirm `Difficulty target` and `Card balance target`.
6. Click `Apply both configurations (Undo supported)`.

The targets default to the GameManager references in the open scene. If there is no manager, the tool offers the default assets:

- `Assets/Settings/DefaultDifficulty.asset`
- `Assets/Settings/DefaultCardBalance.asset`

Both datasets are parsed and validated before either asset is modified. Applying saves only the selected assets. The operation participates in Unity Undo. Revalidate after every workbook edit; applying an older validated snapshot is rejected when the file modification time changes.

This is a manual Excel-to-Unity workflow. Saving Excel does not automatically import. Inspector edits do not automatically update the workbook. Avoid maintaining conflicting changes in both places.

## Workbook structure

- Difficulty settings and stages. New stage rows may be appended to the three editable stage columns; times must remain strictly increasing.
- Card scalar parameters: recharge, collision damage, passive effects, active damage, stacking, dice delay and existing queue spacing.
- Held-count tables covering exactly 1 through 5 cards.
- Twelve independent active skill rows, one for each element/tier pair. Limited mode requires a whole target count. All-target mode requires that field to be blank.
- Formula-driven preview of held effects, active damage, stacking, duration, charge cost and empowered hit grants. This sheet is not imported.
- A Chinese usage guide.
- Hidden schema identifiers and a hidden label registry used to translate the Chinese options to English enum identifiers.

Preserve the hidden identifiers and input layout. Visible worksheet names are not used as import identifiers, so ordinary Excel worksheet renaming does not change import routing.

The initial workbook preserves the CURRENT assets, including the current difficulty base spawn interval of **5 seconds**. The imported target always receives the complete workbook dataset, rather than a partial patch.

## Runtime behavior

Importing changes the persistent assets. While playing, future collision effects and new skill submissions read the new parameters. Existing submitted skills and already applied statuses retain their captured values. Current hand instances, charge progress, current charge requirement and empowered stock are not reset by the importer.

## Implementation

- `Assets/Scripts/Editor/ConfigWorkbookReader.cs`: dependency-free Open XML reader using ZIP and XML. Supports shared and inline strings. Imported formulas are rejected to avoid stale cached input values. Preview formulas are ignored.
- `Assets/Scripts/Editor/ConfigWorkbookImport.cs`: English Editor window, target selection, validation, preview, Undo and asset save.
- `Tests/ConfigWorkbookRunner.cs`: standalone tests against the actual exported workbook.

The reader rejects missing or duplicate keys, incomplete tables, non-finite numbers, incorrect integer fields, invalid configurations, input formulas and unknown enum identifiers. It does not execute macros, formulas or external links. It reads the workbook without modifying it.

## Verification

Passed:

- Runtime and editor script compilation against the installed Unity assemblies. Only pre-existing SideReturnLane obsolete API warnings remain.
- Actual workbook read-back against both current assets: scalar settings, difficulty stages, all held-count entries and all twelve active tier rows match.
- Valid changes to difficulty interval, passive Arc damage and independent Base Spark target count parse correctly.
- Thirteen rejection fixtures cover duplicate stage times, invalid charge minimum, missing numbers, duplicate keys, non-finite input, input formulas, incomplete held/active tables, noncanonical identifiers, unused parameter cells, partial stage rows, missing metadata and negative damage.
- Both shared-string and inline-string cell handling.
- Read-only parsing preserves the source workbook bytes.
- Workbook formula recalculation, representative element/tier changes, combat charge floor and formula error scan. All six visible worksheets were rendered and visually checked.
- Native XLSX visibility flags hide technical identifiers and the label registry while keeping the Chinese working sheets visible.

Not performed:

- Clicking through the importer in an active Unity Editor, verifying native asset-save/Undo interaction, and importing during Play Mode.
- Opening and recalculating the final file in the desktop Excel application. Workbook formulas were verified in the bundled spreadsheet engine instead.

Existing Unity licensing startup failures have prevented earlier editor validation. The importer has been compiled and its parser tested independently; native Editor interaction is not claimed as verified.

## Single-impact Shockwave update

Shockwave now always resolves one impact, for every tier and any number of cards. Held Shockwave remains a single stun application per accepted ball hit, with no additional damage. Active Shockwave deals its configured damage once; scope and stun duration remain tier-specific. Extra Shockwave wave count and Shockwave wave interval were removed from the configuration and Inspector. The shared tier row retains a serialized wave value for compatibility, but the battle planner and execution path enforce one impact even if an older row contains a larger value.

The original Chinese workbook was edited in place after Excel was closed. Only Shockwave-related inputs, explanations and preview formulas changed. All other saved difficulty, held, scalar and active-tier input values were compared and preserved. Shockwave wave cells now show a fixed gray 1, and importing any other value is rejected. There are 14 parser rejection cases, including attempts to restore multiple Shockwave waves. Twenty tier/card-count preview combinations and the standalone battle-plan suite passed. Runtime/editor compilation passed; native Play Mode verification remains pending.

## Time-scaled health for new spawns

DifficultyConfig now owns `baseEnemyHealth` (40 in the active scene profile), and each difficulty stage has a positive finite `healthMultiplier`. Health uses the same interpolated difficulty sampling time as spawn and movement rates. The approved stage health multipliers at 0/30/90/180/300/480 seconds are 1/1.1/1.25/1.5/2/2.5.

At birth, an ordinary enemy receives `max(1, floor(baseEnemyHealth * healthMultiplier + 0.5))` for both current and maximum HP. No subsequent difficulty update changes a living enemy's HP or maximum HP. Existing damage and elemental rules remain unchanged. Legacy non-normal EnemyTuning.health is hidden and does not control ordinary enemy HP.

Inspector previews, the additional health multiplier curve, and the F8 difficulty panel show the HP of the next new enemy. A one-shot editor preview time is applied in Awake so opening monsters also use the requested preview difficulty.

The original Chinese workbook was updated in place. It adds base enemy HP at C11, editable stage health multipliers in column H, and calculated new-enemy HP in column I. Existing spawn/movement values and every card input were preserved. The difficulty worksheet now uses schema `luck-number.difficulty.v2`; the importer rejects older templates rather than silently dropping health settings. All other worksheet schemas remain unchanged.

Verification passed: runtime/editor compilation; health interpolation at 0/30/60/90/180/300/480 seconds; rounding/minimum and invalid health configuration checks; workbook formulas; valid health edits and 19 import rejection fixtures. Native spawn checks for 80 HP at 300 seconds, 100 HP at 480 seconds, and unchanged existing enemy HP are prepared in DifficultyChecks but have not run in Play Mode in this environment.

## Empty opening hand and direct-hit Arc

A new run now starts with five empty slots. Recharge begins at the configured base cost with zero progress; no cast surcharge or empowered hits are granted at startup. Opening launch wait retains its existing timer gate. When combat starts, natural recovery and qualified collisions earn charge normally and cards are refilled one at a time.

A held Arc enchantment now applies its damage once to the living, non-immune directly hit enemy as well as the configured number of nearby eligible enemies. The direct target does not consume a nearby-target slot. Multiple held Arc cards still affect nearby target count, not repeated direct strikes. Collision damage and Arc share the same attack record; lethal physical hits are not killed again, Arc damage does not grant extra collision charge, and Lightning enemies retain Arc immunity.

Runtime and editor script compilation passed. A Play Mode check is available under `LuckyPinball > Card Balance > Check Empty Start and Direct Arc (Play Mode)`. Native Play Mode execution remains unverified.

## Uncapped contact recharge with natural recovery retained

Only natural recovery and qualifying new physical ball/enemy contacts grant charge. Burning (including first and lethal ticks), enchantment damage, active skills, stun and kills grant no additional energy. Each contact earns the full configured rechargeHit amount; rolling budgets and minimum-gain rules have been removed from runtime settings, the Inspector and help text. The recent-contact journal is retained only as a statistic and never limits earnings.

The existing contact rules remain: separate/recontact counts again, each ball is independent, a persistent contact does not repeat, lethal contacts count, and the damage cooldown does not block a genuine new contact's energy. Refill resolves after collision damage and enchantments, so a newly drawn card starts contributing on the next hit. Full hands still stop and discard charge. Natural recovery rate, refill costs and collision gain values are preserved.

The original Chinese workbook was updated in place. Obsolete budget/minimum input rows are now fixed explanatory rows, and the charge preview simply reads the collision gain. The importer ignores retired budget keys from earlier templates so they cannot restore a cap. Compilation, the 30,000-event unlimited-gain account test and workbook parsing passed. Native Play Mode validation remains pending.
