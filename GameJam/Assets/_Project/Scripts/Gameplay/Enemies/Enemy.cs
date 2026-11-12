using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Root of an enemy prefab: identity (definition, number, spawn order) and placement on the board.
    // Rules live in the sibling components; the look lives in EnemyView and the Visual child.
    [RequireComponent(typeof(Rigidbody), typeof(EnemyHealth), typeof(EnemyStatus))]
    [RequireComponent(typeof(EnemyContact))]
    public sealed class Enemy : MonoBehaviour
    {
        [SuffixLabel("m"), Tooltip("Height of the enemy's centre above the playfield surface.")]
        public float centerHeight = 0.25f;

        public EnemyDefinition Definition { get; private set; }
        public int Number { get; private set; }
        public long SpawnOrder { get; private set; }
        public float Radius => Definition ? Definition.radius : 0.5f;

        public Rigidbody Body { get; private set; }
        public EnemyHealth Health { get; private set; }
        public EnemyStatus Status { get; private set; }
        public EnemyContact Contact { get; private set; }

        public bool Alive => Health.Alive && gameObject.activeInHierarchy;

        // Raised once when the enemy leaves play (killed or breached), before it goes back to the pool.
        public event Action<Enemy> Removed;
        public event Action Breached;

        internal GameContext Context { get; private set; }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Health = GetComponent<EnemyHealth>();
            Status = GetComponent<EnemyStatus>();
            Contact = GetComponent<EnemyContact>();
        }

        internal void Spawn(GameContext context, EnemyDefinition definition, int number, long spawnOrder, Vector3 boardPosition)
        {
            Context = context;
            Definition = definition;
            Number = number;
            SpawnOrder = spawnOrder;
            Health.ResetFor(definition.health, context);
            Status.ResetFor(this);
            Contact.ResetFor(this);
            var world = context.Board.ToWorld(WithHeight(boardPosition));
            Body.position = world;
            transform.position = world;
            transform.rotation = context.Board.transform.rotation;
        }

        public Vector3 BoardPosition => Context.Board.ToBoard(Body.position);

        // Move (kinematically) to a board-space XZ; height follows the playfield surface.
        internal void MoveTo(Vector3 boardPosition) => Body.MovePosition(Context.Board.ToWorld(WithHeight(boardPosition)));

        Vector3 WithHeight(Vector3 boardPosition)
        {
            boardPosition.y = Context.Board.SurfaceHeightAt(boardPosition.x, boardPosition.z) + centerHeight;
            return boardPosition;
        }

        internal void Breach()
        {
            if (!Alive || !Context.Run.IsPlaying) return;
            Health.Kill();
            Context.Stats.breaches++;
            Breached?.Invoke();
            Context.Run.LoseLives(Context.config.breachLifeCost, "Enemy breach");
            Remove();
        }

        internal void Remove() => Removed?.Invoke(this);
    }
}
