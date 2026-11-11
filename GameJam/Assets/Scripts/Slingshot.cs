using UnityEngine;

// Kicker wall: a ball striking it is launched away and up the table, faster than it arrived.
// innerFreeFraction can leave the end nearest the board centre as a plain wall (0 = whole wall kicks).
[RequireComponent(typeof(Collider))]
public class Slingshot : MonoBehaviour
{
    [Min(0f), Tooltip("Minimum launch speed.")]
    public float kickSpeed = 28f;
    [Min(1f), Tooltip("A fast incoming ball leaves this many times faster, if that beats kickSpeed.")]
    public float speedMultiplier = 1.3f;
    // 2.5 aims about 68 degrees up the table: lower values zig-zag the ball between the side walls.
    [Range(0f, 4f), Tooltip("How much the kick leans toward the top of the board.")]
    public float upwardBias = 2.5f;
    [Range(0f, .9f), Tooltip("Share of the guide next to the flipper that does not kick.")]
    public float innerFreeFraction = .4f;
    [Min(0f)] public float cooldown = .1f;

    Collider wall;
    float innerEndSign; // guide-local X sign of the end nearest the board centre
    float lastKick = float.NegativeInfinity;

    void Awake()
    {
        wall = GetComponent<Collider>();
        var board = GameManager.Instance ? GameManager.Instance.board : transform.parent;
        float a = Mathf.Abs(board.InverseTransformPoint(transform.TransformPoint(new Vector3(-.5f, 0f, 0f))).x);
        float b = Mathf.Abs(board.InverseTransformPoint(transform.TransformPoint(new Vector3(.5f, 0f, 0f))).x);
        innerEndSign = a < b ? -1f : 1f;
    }

    void OnCollisionEnter(Collision c)
    {
        var gm = GameManager.Instance;
        var ball = c.rigidbody;
        if (!gm || !gm.IsPlaying || !ball || ball.isKinematic || !c.collider.CompareTag("Player")) return;
        if (Time.time - lastKick < cooldown) return;

        var contact = wall.ClosestPoint(ball.position);
        // Guide-local X runs -0.5..0.5 along its length; skip contacts in the inner (flipper-side) part.
        float alongFromInner = .5f - innerEndSign * transform.InverseTransformPoint(contact).x; // 0 at the inner end, 1 at the outer
        // Skip the check entirely at 0: contacts on the end face can come out a hair below zero.
        if (innerFreeFraction > 0f && alongFromInner < innerFreeFraction) return;
        lastKick = Time.time;

        var up = gm.board.up;
        var away = Vector3.ProjectOnPlane(ball.position - contact, up).normalized;
        if (away.sqrMagnitude < .01f) away = gm.board.forward;
        var direction = (away + gm.board.forward * upwardBias).normalized;
        float speed = Mathf.Max(kickSpeed, Vector3.ProjectOnPlane(ball.linearVelocity, up).magnitude * speedMultiplier);
        ball.linearVelocity = direction * speed;
    }
}
