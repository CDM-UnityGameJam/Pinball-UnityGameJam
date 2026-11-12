using System.Collections.Generic;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Powered side return. A ball entering the inlet (or touching the visible track) is carried along the
    // travel path to the outlet and shot back onto the playfield. Port of the old SideReturnLane:
    // - No combat during transit: the ball is kinematic with collisions off.
    // - If another ball or an enemy sits on the outlet, the ball waits above it until it's clear.
    // - A released ball can't be captured again for `reentryCooldown`.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ReturnLane : MonoBehaviour
    {
        [Title("Shape")]
        [Required, Tooltip("Trigger at the inlet.")] public BoxCollider inlet;
        [Tooltip("Points the ball travels through, in order. The outlet is the final point.")]
        public List<Transform> travelPath = new List<Transform>();
        [Required, Tooltip("Where the ball is released; it leaves along this transform's forward.")]
        public Transform outlet;
        [Tooltip("Points of the visible track. Balls touching it are captured too (thin inlets can miss fast balls).")]
        public List<Transform> captureTrack = new List<Transform>();
        [MinValue(0), SuffixLabel("m")] public float captureTrackWidth = 0.38f;

        [Title("Tuning")]
        [MinValue(0.1), SuffixLabel("s"), Tooltip("Travel time for the whole route. A blocked outlet adds waiting time.")]
        public float travelSeconds = 1f;
        [MinValue(0), SuffixLabel("m/s")] public float exitSpeed = 15f;
        [MinValue(0), SuffixLabel("s"), Tooltip("Before the released ball can enter a return lane again.")]
        public float reentryCooldown = 1.2f;
        [MinValue(0), SuffixLabel("m"), Tooltip("Extra free space needed around the outlet (beyond the ball radius).")]
        public float outletClearance = 0.12f;
        [SuffixLabel("m"), Tooltip("Height above the outlet where a ball waits while the outlet is blocked.")]
        public float waitHeight = 1.1f;
        [Tooltip("Layers that block the outlet.")] public LayerMask outletBlockers;

        [Title("Hooks")]
        public UltEvent onCapture;
        public UltEvent onRelease;

        sealed class Trip
        {
            public Ball ball;
            public Vector3 start;
            public float elapsed;
            public bool gravity, collisions;
        }

        readonly List<Trip> trips = new List<Trip>();
        GameContext context;

        public int BallsInTransit => trips.Count;

        public void Initialize(GameContext context) => this.context = context;

        bool Active => context != null && context.Run.IsPlaying;

        void OnTriggerEnter(Collider other) => TryCaptureAtInlet(other);
        void OnTriggerStay(Collider other) => TryCaptureAtInlet(other);

        void TryCaptureAtInlet(Collider other)
        {
            var ball = BallOf(other);
            if (!ball || !CanCapture(ball)) return;
            // Test the inlet volume itself, using the rigidbody pose (it can lead the interpolated transform).
            var body = ball.Body;
            var rotationDelta = body.rotation * Quaternion.Inverse(ball.transform.rotation);
            var colliderPosition = body.position + rotationDelta * (other.transform.position - ball.transform.position);
            if (!Physics.ComputePenetration(inlet, inlet.transform.position, inlet.transform.rotation,
                    other, colliderPosition, rotationDelta * other.transform.rotation, out _, out _)) return;
            Capture(ball);
        }

        static Ball BallOf(Collider other) => other.attachedRigidbody ? other.attachedRigidbody.GetComponent<Ball>() : null;

        bool CanCapture(Ball ball) =>
            Active && !ball.Body.isKinematic && !ball.InReturnLane && Time.time >= ball.ReturnLaneReadyAt && travelPath.Count > 0;

        void Capture(Ball ball)
        {
            var body = ball.Body;
            trips.Add(new Trip { ball = ball, start = body.position, gravity = body.useGravity, collisions = body.detectCollisions });
            ball.InReturnLane = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = false;
            onCapture?.Invoke();
        }

        void FixedUpdate()
        {
            if (!Active) return;
            CaptureFromTrack();
            Advance(Time.fixedDeltaTime);
        }

        // Sweep each free ball's movement this step against the track segments.
        void CaptureFromTrack()
        {
            if (captureTrack.Count < 2) return;
            foreach (var ball in context.Balls.Active)
            {
                if (!ball || !CanCapture(ball)) continue;
                var sphere = ball.GetComponent<SphereCollider>();
                if (!sphere || !sphere.enabled) continue;
                var scale = sphere.transform.lossyScale;
                float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                var body = ball.Body;
                var start = body.position + body.rotation * Vector3.Scale(sphere.center, scale);
                var movement = body.linearVelocity * Time.fixedDeltaTime;
                float reach = radius + captureTrackWidth * 0.5f;
                int steps = Mathf.Max(1, Mathf.CeilToInt(movement.magnitude / Mathf.Max(0.05f, radius * 0.4f)));
                if (TouchesTrack(start, movement, steps, reach)) Capture(ball);
            }
        }

        bool TouchesTrack(Vector3 start, Vector3 movement, int steps, float reach)
        {
            for (int sample = 0; sample <= steps; sample++)
            {
                var point = start + movement * (sample / (float)steps);
                for (int i = 1; i < captureTrack.Count; i++)
                {
                    Vector3 a = captureTrack[i - 1].position, b = captureTrack[i].position, delta = b - a;
                    float t = delta.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector3.Dot(point - a, delta) / delta.sqrMagnitude) : 0f;
                    if ((point - (a + delta * t)).sqrMagnitude <= reach * reach) return true;
                }
            }
            return false;
        }

        void Advance(float deltaTime)
        {
            for (int i = trips.Count - 1; i >= 0; i--)
            {
                var trip = trips[i];
                if (!trip.ball || !trip.ball.isActiveAndEnabled) { trips.RemoveAt(i); continue; }
                var body = trip.ball.Body;
                trip.elapsed += Mathf.Max(0f, deltaTime);
                float progress = Mathf.Clamp01(trip.elapsed / Mathf.Max(0.1f, travelSeconds));
                body.position = PositionAt(trip.start, progress);
                if (progress < 1f) continue;
                if (!OutletClear(trip.ball))
                {
                    body.position = outlet.position + transform.parent.up * waitHeight;
                    continue;
                }
                Release(trip);
                trips.RemoveAt(i);
            }
        }

        void Release(Trip trip)
        {
            var body = trip.ball.Body;
            body.position = outlet.position;
            body.useGravity = trip.gravity;
            body.detectCollisions = trip.collisions;
            body.isKinematic = false;
            body.linearVelocity = outlet.forward * exitSpeed;
            trip.ball.InReturnLane = false;
            trip.ball.ReturnLaneReadyAt = Time.time + reentryCooldown;
            onRelease?.Invoke();
        }

        Vector3 PositionAt(Vector3 start, float progress)
        {
            float total = Vector3.Distance(start, travelPath[0].position);
            for (int i = 1; i < travelPath.Count; i++) total += Vector3.Distance(travelPath[i - 1].position, travelPath[i].position);
            float distance = total * progress;
            Vector3 previous = start;
            foreach (var point in travelPath)
            {
                float length = Vector3.Distance(previous, point.position);
                if (distance <= length && length > 0.0001f) return Vector3.Lerp(previous, point.position, distance / length);
                distance -= length;
                previous = point.position;
            }
            return outlet.position;
        }

        public bool OutletClear(Ball ignored)
        {
            var sphere = ignored.GetComponent<SphereCollider>();
            float ballRadius = sphere ? sphere.radius * ignored.transform.lossyScale.x : 0.375f;
            foreach (var hit in Physics.OverlapSphere(outlet.position, ballRadius + outletClearance, outletBlockers, QueryTriggerInteraction.Ignore))
                if (hit.attachedRigidbody != ignored.Body) return false;
            return true;
        }

        void OnDisable()
        {
            foreach (var trip in trips)
            {
                if (!trip.ball) continue;
                var body = trip.ball.Body;
                body.position = outlet.position;
                body.detectCollisions = trip.collisions;
                body.useGravity = trip.gravity;
                body.isKinematic = context != null && context.Run.State == RunState.GameOver;
                trip.ball.InReturnLane = false;
            }
            trips.Clear();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.6f);
            for (int i = 1; i < travelPath.Count; i++)
                if (travelPath[i - 1] && travelPath[i]) Gizmos.DrawLine(travelPath[i - 1].position, travelPath[i].position);
            if (outlet) Gizmos.DrawRay(outlet.position, outlet.forward * 1.5f);
            Gizmos.color = new Color(1f, 0.5f, 0.1f);
            for (int i = 1; i < captureTrack.Count; i++)
                if (captureTrack[i - 1] && captureTrack[i]) Gizmos.DrawLine(captureTrack[i - 1].position, captureTrack[i].position);
        }
    }
}
