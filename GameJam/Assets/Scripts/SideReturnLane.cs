using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SideReturnLane : MonoBehaviour
{
    public int side = 1;
    [Min(.1f), Tooltip("Travel time for the full return route. Lower values are faster; a blocked outlet adds waiting time.")]
    public float travelSeconds = 2.3f;
    [Min(0f), Tooltip("Launch speed at the top outlet, independent of travel time inside the lane.")]
    public float exitSpeed = 18f;
    [Min(0f), Tooltip("Cooldown before re-entering the return lane after leaving the outlet.")]
    public float reentryCooldown = 1.2f;
    public Vector3 outlet = new Vector3(4.3f, .3f, 8.3f);
    public Vector3[] path;
    [Tooltip("Outlet direction: X across the board, Y toward the top of the board.")]
    public Vector2 exitDirection;
    class Trip
    {
        public Rigidbody ball;
        public SideReturnBall state;
        public float elapsed;
        public bool gravity, collisions;
        public Vector3 start;
    }
    readonly List<Trip> trips = new List<Trip>();

    void OnTriggerEnter(Collider collider) => TryCapture(collider);
    void OnTriggerStay(Collider collider) => TryCapture(collider);

    public bool TryCapture(Collider collider)
        => Capture(collider, false);

    bool Capture(Collider collider, bool trackContact)
    {
        var gm = GameManager.Instance;
        var ball = collider.attachedRigidbody;
        if (!gm || !gm.IsPlaying || !ball || ball.isKinematic || !collider.CompareTag("Player")) return false;
        // Test the oriented entrance itself, including balls touching its edge.
        var entrance = GetComponent<BoxCollider>();
        // Rigidbody poses can lead their interpolated visual transforms by a physics step.
        var rotationDelta = ball.rotation * Quaternion.Inverse(ball.transform.rotation);
        var colliderPosition = ball.position + rotationDelta * (collider.transform.position - ball.transform.position);
        if (!trackContact && !Physics.ComputePenetration(entrance, entrance.transform.position, entrance.transform.rotation,
            collider, colliderPosition, rotationDelta * collider.transform.rotation, out _, out _)) return false;
        var state = ball.GetComponent<SideReturnBall>();
        if (!state) state = ball.gameObject.AddComponent<SideReturnBall>();
        if (state.InTransit || Time.time < state.ReadyAt) return false;
        if (path == null || path.Length < 2) return false;
        trips.Add(new Trip { ball = ball, state = state, start = gm.board.InverseTransformPoint(ball.position),
            gravity = ball.useGravity, collisions = ball.detectCollisions });
        state.InTransit = true;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.isKinematic = true;
        ball.detectCollisions = false; // Dedicated powered transport; no combat hits inside the duct.
        return true;
    }

    void FixedUpdate()
    {
        DetectTrackContacts();
        Advance(Time.fixedDeltaTime);
    }

    // Trigger callbacks alone can miss a thin entrance when a solid guide bounces
    // the ball during the same physics step. Sweep the ball over the visible track.
    public void DetectTrackContacts()
    {
        var gm = GameManager.Instance;
        var line = GetComponent<LineRenderer>();
        if (!gm || !gm.IsPlaying || !line || line.positionCount < 2) return;
        foreach (var body in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if (body.isKinematic || !body.CompareTag("Player")) continue;
            var sphere = body.GetComponent<SphereCollider>();
            if (!sphere || !sphere.enabled) continue;
            var scale = sphere.transform.lossyScale;
            float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var start = body.position + body.rotation * Vector3.Scale(sphere.center, scale);
            var movement = body.linearVelocity * Time.fixedDeltaTime;
            float reach = radius + line.widthMultiplier * .5f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(movement.magnitude / Mathf.Max(.05f, radius * .4f)));
            bool contact = false;
            for (int sample = 0; sample <= steps && !contact; sample++)
            {
                var point = start + movement * (sample / (float)steps);
                for (int segment = 1; segment < line.positionCount; segment++)
                {
                    var a = line.GetPosition(segment - 1);
                    var b = line.GetPosition(segment);
                    if (!line.useWorldSpace) { a = line.transform.TransformPoint(a); b = line.transform.TransformPoint(b); }
                    var delta = b - a;
                    float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector3.Dot(point - a, delta) / delta.sqrMagnitude) : 0f;
                    if ((point - (a + delta * t)).sqrMagnitude <= reach * reach)
                    { contact = true; break; }
                }
            }
            if (contact) Capture(sphere, true);
        }
    }

    public void Advance(float deltaTime)
    {
        var gm = GameManager.Instance;
        if (!gm || !gm.IsPlaying) return;
        for (int i = trips.Count - 1; i >= 0; i--)
        {
            var trip = trips[i];
            if (!trip.ball) { trips.RemoveAt(i); continue; }
            trip.elapsed += Mathf.Max(0f, deltaTime);
            float progress = Mathf.Clamp01(trip.elapsed / Mathf.Max(.1f, travelSeconds));
            trip.ball.position = gm.board.TransformPoint(PositionAt(trip.start, progress));
            if (progress < 1f) continue;
            if (!OutletClear(trip.ball))
            { trip.ball.position = gm.board.TransformPoint(outlet + Vector3.up * 1.1f); continue; }
            trip.ball.position = gm.board.TransformPoint(outlet);
            trip.ball.useGravity = trip.gravity;
            trip.ball.detectCollisions = trip.collisions;
            trip.ball.isKinematic = false;
            var aim = exitDirection.sqrMagnitude > .01f ? new Vector3(exitDirection.x, 0f, exitDirection.y) : new Vector3(-side * .85f, 0f, .5f);
            trip.ball.linearVelocity = gm.board.TransformDirection(aim.normalized) * exitSpeed;
            trip.state.InTransit = false;
            trip.state.ReadyAt = Time.time + reentryCooldown;
            trips.RemoveAt(i);
        }
    }

    Vector3 PositionAt(Vector3 start, float progress)
    {
        float total = Vector3.Distance(start, path[0]);
        for (int i = 1; i < path.Length; i++) total += Vector3.Distance(path[i - 1], path[i]);
        float distance = total * progress;
        Vector3 previous = start;
        foreach (var point in path)
        {
            float length = Vector3.Distance(previous, point);
            if (distance <= length && length > .0001f) return Vector3.Lerp(previous, point, distance / length);
            distance -= length;
            previous = point;
        }
        return outlet;
    }

    public bool OutletClear(Rigidbody ignored)
    {
        var gm = GameManager.Instance;
        var world = gm.board.TransformPoint(outlet);
        foreach (var hit in Physics.OverlapSphere(world, gm.spawning.ballRadius + .12f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.attachedRigidbody == ignored) continue;
            if (hit.GetComponentInParent<Enemy>() || hit.CompareTag("Player")) return false;
        }
        return true;
    }

    public bool BlocksSpawn(Vector3 position, float radius, bool descending, float dangerZ)
    {
        float z = descending ? Mathf.Clamp(outlet.z, Mathf.Min(position.z, dangerZ), Mathf.Max(position.z, dangerZ)) : position.z;
        return Vector2.Distance(new Vector2(position.x, z), new Vector2(outlet.x, outlet.z)) < radius + .7f;
    }

    void OnDisable()
    {
        foreach (var trip in trips)
        {
            if (!trip.ball) continue;
            var gm = GameManager.Instance;
            trip.ball.position = gm ? gm.board.TransformPoint(outlet) : trip.ball.position;
            trip.ball.detectCollisions = trip.collisions;
            trip.ball.useGravity = trip.gravity;
            trip.ball.isKinematic = gm && gm.IsGameOver;
            trip.state.InTransit = false;
        }
        trips.Clear();
    }
}
