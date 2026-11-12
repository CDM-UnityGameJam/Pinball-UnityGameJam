using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;

namespace Pinball.Gameplay
{
    // One pinball. Physics settings live on the prefab's Rigidbody, collider and physics material.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Ball : MonoBehaviour
    {
        [Title("Hooks")]
        public UltEvent onLaunch;
        public UltEvent onDrain;
        [Tooltip("The ball hit a wall, guide or flipper hard enough (rolling on the playfield never counts).")]
        public UltEvent onBounce;
        [MinValue(0), SuffixLabel("m/s"), Tooltip("Minimum speed INTO the surface (along the contact normal). Sliding along it doesn't count.")]
        public float minBounceSpeed = 3f;
        [Range(0f, 90f), SuffixLabel("deg"), Tooltip("Contacts whose surface faces within this angle of the board's up are the playfield floor, not a bounce.")]
        public float floorAngle = 45f;
        [Tooltip("Layers that count as a bounce. Enemies have their own hit hooks.")]
        public LayerMask bounceLayers = ~0;

        // The run this ball belongs to (set when spawned).
        public GameContext Context { get; internal set; }

        Rigidbody body;
        public Rigidbody Body => body ? body : body = GetComponent<Rigidbody>();

        // Set while a return lane carries the ball; it can't be captured again until ReturnLaneReadyAt.
        public bool InReturnLane { get; internal set; }
        public float ReturnLaneReadyAt { get; internal set; } = float.NegativeInfinity;

        // Hold the ball still at a point (waiting for launch, game over).
        public void Park(Vector3 position)
        {
            Body.isKinematic = true;
            Body.position = position;
            transform.position = position;
        }

        public void Freeze() => Body.isKinematic = true;

        void OnCollisionEnter(Collision collision)
        {
            if ((bounceLayers.value & (1 << collision.collider.gameObject.layer)) == 0 || collision.contactCount == 0) return;
            if (IsBounce(collision.GetContact(0).normal, collision.relativeVelocity)) onBounce?.Invoke();
        }

        // A bounce is a hit into a wall-like surface: the speed along the contact normal is high enough and the
        // surface isn't the floor the ball rolls on.
        public bool IsBounce(Vector3 contactNormal, Vector3 relativeVelocity)
        {
            var up = Context != null ? Context.Board.Up : Vector3.up;
            if (Vector3.Angle(contactNormal, up) <= floorAngle) return false;
            return Mathf.Abs(Vector3.Dot(relativeVelocity, contactNormal)) >= minBounceSpeed;
        }

        public void Launch(Vector3 position, Vector3 velocity)
        {
            Body.position = position;
            transform.position = position;
            Body.isKinematic = false;
            Body.angularVelocity = Vector3.zero;
            Body.linearVelocity = velocity;
            onLaunch?.Invoke();
        }

        internal void ResetForReuse()
        {
            InReturnLane = false;
            ReturnLaneReadyAt = float.NegativeInfinity;
            Body.isKinematic = true;
            Body.useGravity = true;
            Body.detectCollisions = true;
        }
    }
}
