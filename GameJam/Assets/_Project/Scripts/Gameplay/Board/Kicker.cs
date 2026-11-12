using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Kicker wall (the side walls): a ball striking it is launched away from the wall and up the table,
    // faster than it arrived. Put it on the wall's collider root. The wall's local X runs along its length
    // when `plainInnerFraction` is used; it leaves the end nearest the board centre as a plain wall.
    [RequireComponent(typeof(Collider))]
    public sealed class Kicker : MonoBehaviour
    {
        [MinValue(0), SuffixLabel("m/s"), Tooltip("Minimum launch speed.")]
        public float kickSpeed = 28f;
        [MinValue(1), Tooltip("A fast incoming ball leaves this many times faster, if that beats Kick Speed.")]
        public float speedMultiplier = 1.3f;
        // 2.5 aims about 68 degrees up the table: lower values zig-zag the ball between the side walls.
        [Range(0f, 4f), Tooltip("How much the kick leans toward the top of the board.")]
        public float upwardBias = 2.5f;
        [Range(0f, 0.9f), Tooltip("Share of the wall's length, from the end nearest the board centre, that doesn't kick. 0 = the whole wall kicks.")]
        public float plainInnerFraction = 0f;
        [MinValue(0), SuffixLabel("s"), Tooltip("Gameplay time between two kicks.")]
        public float cooldown = 0.1f;

        [Title("Hooks")]
        public UltEvent onKick;

        Collider wall;
        double lastKick = double.NegativeInfinity;

        Collider Wall => wall ? wall : wall = GetComponent<Collider>();

        void OnCollisionEnter(Collision collision)
        {
            var ball = collision.rigidbody ? collision.rigidbody.GetComponent<Ball>() : null;
            var context = ball ? ball.Context : null;
            if (context == null || !context.Run.IsPlaying || ball.Body.isKinematic) return;
            if (context.Clock.Now - lastKick < cooldown) return;

            var board = context.Board;
            var contact = Wall.ClosestPoint(ball.Body.position);
            if (plainInnerFraction > 0f && AlongFromInnerEnd(board, contact) < plainInnerFraction) return;
            lastKick = context.Clock.Now;

            var away = Vector3.ProjectOnPlane(ball.Body.position - contact, board.Up).normalized;
            if (away.sqrMagnitude < 0.01f) away = board.Forward;
            var direction = (away + board.Forward * upwardBias).normalized;
            float speed = Mathf.Max(kickSpeed, Vector3.ProjectOnPlane(ball.Body.linearVelocity, board.Up).magnitude * speedMultiplier);
            ball.Body.linearVelocity = direction * speed;
            onKick?.Invoke();
        }

        // 0 at the end of the wall nearest the board centre, 1 at the far end.
        float AlongFromInnerEnd(BoardRoot board, Vector3 contact)
        {
            float a = Mathf.Abs(board.ToBoard(transform.TransformPoint(new Vector3(-0.5f, 0f, 0f))).x);
            float b = Mathf.Abs(board.ToBoard(transform.TransformPoint(new Vector3(0.5f, 0f, 0f))).x);
            float innerSign = a < b ? -1f : 1f;
            return 0.5f - innerSign * transform.InverseTransformPoint(contact).x;
        }
    }
}
