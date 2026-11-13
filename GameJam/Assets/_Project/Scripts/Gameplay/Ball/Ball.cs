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
        [Tooltip("The ball hit the side of a wall, guide or resting flipper hard enough. The playfield, deflectors, " +
                 "other balls and enemies never count (enemies have their own hit hooks).")]
        public UltEvent onBounce;
        [Tooltip("Same, against a flipper the player is holding up (a flipper shot).")]
        public UltEvent onFlipperShot;
        [MinValue(0), SuffixLabel("m/s"), Tooltip("Minimum speed INTO the surface (along the contact normal). Sliding along it doesn't count.")]
        public float minBounceSpeed = 1f;
        [Range(0f, 90f), SuffixLabel("deg"), Tooltip("Only side-facing contacts count: surfaces facing within this angle of the board's up (or down) are floors and tops.")]
        public float floorAngle = 60f;
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
            var hit = collision.collider;
            if (Body.isKinematic || (bounceLayers.value & (1 << hit.gameObject.layer)) == 0 || collision.contactCount == 0) return;
            if (Context != null && (!Context.Run.IsPlaying || hit == Context.Board.playfieldSurface)) return;
            if (hit.GetComponentInParent<Deflector>() || hit.GetComponentInParent<Ball>()) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (!IsBounce(collision.GetContact(i).normal, collision.relativeVelocity)) continue;
                var flipper = hit.GetComponentInParent<Flipper>();
                if (flipper && flipper.IsPressed) onFlipperShot?.Invoke();
                else onBounce?.Invoke();
                return;
            }
        }

        // A bounce is a hit into a side-facing surface: the speed along the contact normal is high enough and the
        // surface isn't a floor or a top.
        public bool IsBounce(Vector3 contactNormal, Vector3 relativeVelocity)
        {
            var up = Context != null ? Context.Board.Up : Vector3.up;
            float angle = Vector3.Angle(contactNormal, up);
            if (angle <= floorAngle || angle >= 180f - floorAngle) return false;
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
