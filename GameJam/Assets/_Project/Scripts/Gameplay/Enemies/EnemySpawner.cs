using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Pinball.Gameplay
{
    // Spawns enemies from the SpawnProfile into the board's SpawnZone, pooled per enemy type.
    // Port of the v0.6 Spawner:
    // - The run opens with `openingCount` enemies in shuffled columns `openingColumnSpacing` apart.
    // - Spawn attempts follow combat time: the interval is the base interval over the current difficulty's spawn
    //   multiplier, and a failed or blocked attempt still uses its turn (SpawnPhase). Each attempt spawns with
    //   `spawnChance`.
    // - Each enemy gets a random element from the game's elements (or its fixed one) and its health × the
    //   current difficulty's health multiplier.
    // - A spawn tries random X positions on the spawn row; a position is rejected if it's outside the arc,
    //   too close to another enemy or a ball, or if its straight path down would cross a SpawnBlocker.
    public sealed class EnemySpawner
    {
        readonly GameContext context;
        readonly Dictionary<EnemyDefinition, ObjectPool<Enemy>> pools = new Dictionary<EnemyDefinition, ObjectPool<Enemy>>();
        readonly List<SpawnBlocker> blockers = new List<SpawnBlocker>();
        readonly Transform parent;
        readonly Pinball.Core.SpawnPhase phase = new Pinball.Core.SpawnPhase();
        long nextSpawnOrder;

        public EnemySpawner(GameContext context, Transform parent)
        {
            this.context = context;
            this.parent = parent;
            context.Board.GetComponentsInChildren(true, blockers);
            context.Clock.Stepped += OnStep;
        }

        public double Phase => phase.Value;
        public int PeriodicAttempts { get; private set; }

        SpawnProfile Profile => context.config.spawnProfile;
        BoardRoot Board => context.Board;

        public void SpawnOpening()
        {
            int count = Mathf.Max(0, Profile.openingCount);
            var columns = new float[count];
            for (int i = 0; i < count; i++) columns[i] = (i - (count - 1) / 2f) * Profile.openingColumnSpacing;
            for (int i = 0; i < count; i++)
            {
                int j = context.Random.Range(i, columns.Length);
                (columns[i], columns[j]) = (columns[j], columns[i]);
                TrySpawn(PickEnemy(), columns[i]);
            }
        }

        // Combat seconds between spawn attempts right now.
        public double CurrentInterval => Profile.baseSpawnInterval / Profile.Sample(context.Clock.Now).spawnMultiplier;

        // Health a new enemy of this type gets right now.
        public int HealthFor(EnemyDefinition definition) =>
            Pinball.Core.DifficultyCurve.Health(definition.health, Profile.Sample(context.Clock.Now).healthMultiplier);

        void OnStep(double step)
        {
            if (!context.Run.IsPlaying || context.DriveTimeManually) return;
            int attempts = phase.Advance(step, CurrentInterval);
            for (int i = 0; i < attempts; i++)
            {
                PeriodicAttempts++;
                if (context.Random.Value() < Profile.spawnChance) TrySpawn(PickEnemy());
            }
        }

        public EnemyDefinition PickEnemy()
        {
            float total = 0f;
            foreach (var entry in Profile.enemies) if (entry.enemy) total += Mathf.Max(0f, entry.weight);
            if (total <= 0f) return null;
            float remaining = context.Random.Value() * total;
            EnemyDefinition last = null;
            foreach (var entry in Profile.enemies)
            {
                if (!entry.enemy || entry.weight <= 0f) continue;
                last = entry.enemy;
                if (remaining < entry.weight) return entry.enemy;
                remaining -= entry.weight;
            }
            return last;
        }

        // Try random positions on the spawn row (or one fixed X). Returns the enemy, or null if no room.
        public Enemy TrySpawn(EnemyDefinition definition, float? fixedX = null)
        {
            if (!definition) return null;
            float row = Board.spawnZone.RowZ;
            for (int attempt = 0; attempt < Mathf.Max(1, Profile.placementAttempts); attempt++)
            {
                float width = Board.spawnZone.HalfWidthAt(row) - definition.radius - definition.wallClearance;
                if (width < 0f || (fixedX.HasValue && Mathf.Abs(fixedX.Value) > width)) return null;
                float x = fixedX ?? Mathf.Lerp(-width, width, context.Random.Value());
                var position = new Vector3(x, 0f, row);
                if (!IsClear(definition, position)) continue;
                return Spawn(definition, ElementFor(definition), position);
            }
            return null;
        }

        ElementDefinition ElementFor(EnemyDefinition definition)
        {
            if (definition.elementRule == EnemyDefinition.ElementRule.Fixed) return definition.fixedElement;
            var elements = context.config.elements;
            return elements.Count == 0 ? null : elements[context.Random.Range(0, elements.Count)];
        }

        public bool IsClear(EnemyDefinition definition, Vector3 boardPosition)
        {
            float danger = Board.dangerLine.BoardZ;
            foreach (var blocker in blockers)
                if (blocker && blocker.isActiveAndEnabled && blocker.Blocks(boardPosition, definition.radius + definition.wallClearance, danger))
                    return false;

            foreach (var other in context.Enemies.FrontFirst(scratch))
            {
                var d = other.BoardPosition - boardPosition;
                float side = definition.radius + other.Radius + definition.horizontalGap;
                float stack = definition.radius + other.Radius + definition.verticalGap;
                if (Mathf.Abs(d.x) < side && Mathf.Abs(d.z) < stack) return false;
            }

            foreach (var ball in context.Balls.Active)
            {
                if (!ball) continue;
                var p = Board.ToBoard(ball.Body.position);
                float clearance = definition.radius + BallRadius(ball) + definition.ballClearance;
                if (new Vector2(p.x - boardPosition.x, p.z - boardPosition.z).sqrMagnitude < clearance * clearance) return false;
            }
            return true;
        }

        readonly List<Enemy> scratch = new List<Enemy>();

        static float BallRadius(Ball ball)
        {
            var sphere = ball.GetComponent<SphereCollider>();
            return sphere ? sphere.radius * ball.transform.lossyScale.x : 0.375f;
        }

        // Place an enemy directly (also used by tests and debug tools). Board-space position; height is
        // taken from the playfield. Health follows the current difficulty unless given.
        public Enemy Spawn(EnemyDefinition definition, ElementDefinition element, Vector3 boardPosition, int? health = null)
        {
            var enemy = PoolFor(definition).Get();
            enemy.Spawn(context, definition, element, health ?? HealthFor(definition), nextSpawnOrder++, boardPosition);
            context.Stats.spawns++;
            context.Enemies.Add(enemy);
            return enemy;
        }

        ObjectPool<Enemy> PoolFor(EnemyDefinition definition)
        {
            if (pools.TryGetValue(definition, out var pool)) return pool;
            pool = new ObjectPool<Enemy>(
                createFunc: () => Create(definition),
                actionOnGet: e => e.gameObject.SetActive(true),
                actionOnRelease: e => e.gameObject.SetActive(false),
                actionOnDestroy: e => { if (e) Object.Destroy(e.gameObject); });
            pools.Add(definition, pool);
            return pool;
        }

        Enemy Create(EnemyDefinition definition)
        {
            var enemy = Object.Instantiate(definition.prefab, parent).GetComponent<Enemy>();
            enemy.name = definition.name;
            enemy.Health.Died += source => OnDied(enemy, source);
            enemy.Removed += e => Release(definition, e);
            return enemy;
        }

        void OnDied(Enemy enemy, AttackRecord source)
        {
            context.Score.AddKill(enemy, source);
            enemy.Remove();
        }

        void Release(EnemyDefinition definition, Enemy enemy)
        {
            context.Enemies.Remove(enemy);
            pools[definition].Release(enemy);
        }
    }
}
