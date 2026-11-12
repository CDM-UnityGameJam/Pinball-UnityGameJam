using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Pinball.Gameplay
{
    // Every ball in play, pooled. Supports several balls at once (multiball).
    public sealed class BallSystem
    {
        readonly ObjectPool<Ball> pool;
        readonly List<Ball> active = new List<Ball>();

        public IReadOnlyList<Ball> Active => active;
        public int Count => active.Count;

        public BallSystem(GameContext context, Ball prefab, Transform parent)
        {
            pool = new ObjectPool<Ball>(
                createFunc: () => { var b = Object.Instantiate(prefab, parent); b.Context = context; return b; },
                actionOnGet: ball => { ball.ResetForReuse(); ball.gameObject.SetActive(true); },
                actionOnRelease: ball => ball.gameObject.SetActive(false),
                actionOnDestroy: ball => { if (ball) Object.Destroy(ball.gameObject); },
                collectionCheck: true, defaultCapacity: 2);
        }

        // A new ball held still at a point.
        public Ball Spawn(Vector3 position)
        {
            var ball = pool.Get();
            ball.name = "Ball";
            ball.Park(position);
            active.Add(ball);
            return ball;
        }

        // Take a drained ball out of play. False if it wasn't in play.
        public bool Remove(Ball ball)
        {
            if (!ball || !active.Remove(ball)) return false;
            ball.onDrain?.Invoke();
            pool.Release(ball);
            return true;
        }

        public void FreezeAll()
        {
            foreach (var ball in active) if (ball) ball.Freeze();
        }
    }
}
