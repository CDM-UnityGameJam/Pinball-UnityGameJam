using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Pinball.Gameplay
{
    // Spawns enemies from the SpawnProfile into the board's SpawnZone, pooled per enemy type.
    // Port of the old Spawner:
    // - The run opens with `openingCount` enemies in shuffled columns `openingColumnSpacing` apart.
    // - Every `spawnInterval` seconds of play, a spawn happens with `spawnChance`.
    // - A spawn tries random X positions on the spawn row; a position is rejected if it's outside the arc,
    //   too close to another enemy or a ball, or if its straight path down would cross a SpawnBlocker.
    public sealed class EnemySpawner
    {
        readonly GameContext context;
        readonly Dictionary<EnemyDefinition, ObjectPool<Enemy>> pools = new Dictionary<EnemyDefinition, ObjectPool<Enemy>>();
        readonly List<SpawnBlocker> blockers = new List<SpawnBlocker>();
        readonly Transform parent;
        long nextSpawnOrder;
        float timer;

        public EnemySpawner(GameContext context, Transform parent)
        {
            this.context = context;
            this.parent = parent;
            context.Board.GetComponentsInChildren(true, blockers);
        }

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

        // Advance the spawn timer with scaled game time.
        public void Tick(float deltaTime)
        {
            if (!context.Run.IsPlaying) return;
            timer += deltaTime;
            float minutes = (float)(context.Clock.Now / 60.0);
            float interval = Mathf.Max(0.01f, Profile.spawnInterval * Profile.spawnIntervalOverTime.Evaluate(minutes));
            while (timer >= interval)
            {
                timer -= interval;
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
                return Spawn(definition, NumberFor(definition), position);
            }
            return null;
        }

        int NumberFor(EnemyDefinition definition)
        {
            if (definition.numberRule == EnemyDefinition.NumberRule.Fixed) return definition.fixedNumber;
            var luck = context.config.luck;
            return context.Random.Range(luck.minNumber, luck.maxNumber + 1);
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
        // taken from the playfield.
        public Enemy Spawn(EnemyDefinition definition, int number, Vector3 boardPosition)
        {
            var enemy = PoolFor(definition).Get();
            enemy.Spawn(context, definition, number, nextSpawnOrder++, boardPosition);
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
