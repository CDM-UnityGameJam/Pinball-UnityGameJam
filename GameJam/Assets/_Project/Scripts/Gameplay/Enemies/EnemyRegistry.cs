using System;
using System.Collections.Generic;
using Pinball.Core;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Every enemy in play. Replaces the old FindObjectsByType<Enemy> calls.
    // Also the combat-clock timer for all enemy burns, so they tick front first and before other timers
    // (the old TickGameplay order).
    public sealed class EnemyRegistry : ICombatTimer
    {
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Enemy> scratch = new List<Enemy>();
        readonly BoardRoot board;

        public EnemyRegistry(BoardRoot board) => this.board = board;

        public int Count => enemies.Count;

        // (enemy, ball, contact point, damage allowed) for every new ball contact.
        public event Action<Enemy, Ball, Vector3, bool> BallContact;
        public event Action<Enemy> Added;
        public event Action<Enemy> Removed;

        internal void Add(Enemy enemy)
        {
            if (enemies.Contains(enemy)) return;
            enemies.Add(enemy);
            Added?.Invoke(enemy);
        }

        internal void Remove(Enemy enemy)
        {
            if (enemies.Remove(enemy)) Removed?.Invoke(enemy);
        }

        internal void ReportContact(Enemy enemy, Ball ball, Vector3 point, bool damageAllowed) => BallContact?.Invoke(enemy, ball, point, damageAllowed);

        // Take every living enemy off the field without rewards (a life was lost).
        internal void DespawnAll()
        {
            foreach (var e in FrontFirst(scratch)) e.Despawn();
        }

        // Living enemies, closest to the danger line first (ties: oldest first). Fills and returns `into`.
        public List<Enemy> FrontFirst(List<Enemy> into)
        {
            into.Clear();
            foreach (var e in enemies) if (e && e.Alive) into.Add(e);
            float danger = board.dangerLine.BoardZ;
            into.Sort((a, b) =>
            {
                int order = Mathf.Abs(a.BoardPosition.z - danger).CompareTo(Mathf.Abs(b.BoardPosition.z - danger));
                return order != 0 ? order : a.SpawnOrder.CompareTo(b.SpawnOrder);
            });
            return into;
        }

        // Living enemies within `radius` of a world point, measured flat on the board, nearest first
        // (ties: oldest first). With an element, only enemies it can affect (not of that element).
        // Optional enemy to leave out.
        public List<Enemy> Nearby(Vector3 point, float radius, ElementDefinition element, Enemy except, List<Enemy> into)
        {
            into.Clear();
            var up = board.Up;
            float limit = radius * radius + 0.00001f;
            foreach (var e in enemies)
            {
                if (!e || !e.Alive || e == except || (element && e.Element == element)) continue;
                if (Vector3.ProjectOnPlane(e.transform.position - point, up).sqrMagnitude <= limit) into.Add(e);
            }
            into.Sort((a, b) =>
            {
                float da = Vector3.ProjectOnPlane(a.transform.position - point, up).sqrMagnitude;
                float db = Vector3.ProjectOnPlane(b.transform.position - point, up).sqrMagnitude;
                int order = da.CompareTo(db);
                return order != 0 ? order : a.SpawnOrder.CompareTo(b.SpawnOrder);
            });
            return into;
        }

        public double NextEventAt
        {
            get
            {
                double next = double.PositiveInfinity;
                foreach (var e in enemies) if (e) next = Math.Min(next, e.Status.NextTickAt);
                return next;
            }
        }

        public void OnTime(double now)
        {
            foreach (var e in FrontFirst(scratch)) if (e.Alive) e.Status.Tick(now);
        }
    }
}
