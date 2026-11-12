# Breach-only life loss and continuous relaunch

Only enemy breaches reduce lives. `breachLifeCost` remains configurable; the obsolete `drainLifeCost` field and its Pinball scene value were removed.

When the last ball drains, the manager remains in Playing. A replacement countdown uses `launchWait` and advances on effective combat time. Monsters keep spawning and moving, difficulty increases, statuses and submitted skills resolve, recharge continues, and cards remain usable. The shared launch position and launch velocity are used when the countdown completes.

No replacement collider is parked on the table during the countdown. The replacement is instantiated only at launch. If another ball or queued multiball reward is available, an extra replacement is not launched. Duplicate drain notifications are ignored. A zero delay launches immediately.

Manual pause still freezes combat and the replacement countdown. Game over cancels pending replacement and prevents further launches. The initial opening countdown retains its existing Waiting behavior; only relaunch after a drain continues combat.

The English HUD shows RELAUNCH and the remaining countdown while a replacement is pending. Cards, accumulated recharge, current recharge requirement, empowered stock and combat time are retained across drains.

Verification: runtime and editor scripts compile against the installed Unity assemblies. Code review confirms that the only life-deduction call site is the enemy-breach entry. Play Mode checks are prepared at `LuckyPinball > Run > Check Breach Lives and Continuous Relaunch (Play Mode)`, covering free repeated drains, duplicate notifications, combat/recharge/difficulty/status clocks, reveal timing, manual pause, movement, multiball, zero delay and game-over cancellation. Native Play Mode execution has not been completed in this environment.
