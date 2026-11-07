# Lucky Pinball – Prototype Brief

## Context
- Game jam project based on the "Lucky Pinball Core Design" doc (3D pinball combat with charged QTEs, lucky numbers and cards).
- This prototype covers only the pinball physics and a new "mystery circle" enemy idea. It does **not** include charge, QTE, lucky numbers or cards.
- One person builds everything.
- Unity 6000.6.8f1, Universal 3D template. The project is at `C:\school\Personal Project\unityJam\GameJam`.
- The Unity MCP is MCP for Unity (CoplayDev), registered in Claude Code as `UnityMCP` at `http://localhost:8080/mcp`. Use it to create the scene, objects and scripts.
- Use primitive shapes only. No art or audio.

## New idea: mystery circles
- Every enemy and obstacle spawns as an identical grey cylinder, so the player can't tell which is which.
- The first ball hit reveals what it is:
  - **Enemy:** turns red and shows its HP. The reveal hit counts as 1 damage. Enemy HP is 3, and the enemy is destroyed at 0.
  - **Obstacle:** turns blue and becomes a pop bumper that kicks the ball away with extra force. It can't be damaged.
- Each circle has a 0.2 s hit cooldown, so a single overlapping contact can't count as several hits.

## Waves
- Wave 1 has 2 enemies and 2 obstacles. Each wave adds 1 of each, up to a maximum of 5 enemies and 5 obstacles.
- A wave is cleared when all its enemies are dead. Any remaining obstacles are then removed and the next wave spawns.
- Clearing wave 4 wins the run.
- Circles spawn only in the upper ~60% of the board, away from the flippers and the lanes above them. Spawned circles must not overlap each other.

## Pinball physics
- The board is a 3D plane tilted about 6.5°, with side walls, a curved top and a drain trigger at the bottom. The camera is fixed at an angle above the board.
- The ball is a sphere Rigidbody with Continuous Dynamic collision and a bouncy physics material. If the ball tunnels through objects, lower the fixed timestep to about 0.01.
- Flippers are kinematic Rigidbodies turned with `MoveRotation` between their rest and up angles.
  - Left flipper: A or Left Arrow.
  - Right flipper: D or Right Arrow.
- The player has 3 HP. A drain costs 1 HP, then the ball relaunches automatically after a 1 s countdown. At 0 HP the player loses.
- R restarts the run.

## HUD
- Show player HP, the current wave, and the number of enemies left.
- Show a win screen and a lose screen.

## Done when
- The flippers feel responsive and the ball never passes through them.
- Hitting a circle reveals whether it's an enemy or an obstacle. Enemies die after 3 hits. Obstacles kick the ball away.
- Waves grow in size as specified, and clearing wave 4 shows the win screen.
- Draining 3 times shows the lose screen, and R resets everything.
