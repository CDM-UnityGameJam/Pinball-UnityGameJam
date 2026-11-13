using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Fixed spring post (port of the v0.6 TopDeflector / SpringBumper): a ball touching it is sent off at a
    // set speed, either straight away from the post or guided along a board direction. No damage, no charge.
    // Put it on the post's collider root.
    [RequireComponent(typeof(Collider))]
    public sealed class Deflector : MonoBehaviour
    {
        [MinValue(0), SuffixLabel("m/s")] public float kickSpeed = 11f;
        [Range(0f, 0.5f), Tooltip("Without guiding: how much the kick leans toward the top of the board.")]
        public float upwardBias;
        [Tooltip("Send the ball along Guide Direction instead of straight away from the post.")]
        public bool guide = true;
        [ShowIf(nameof(guide)), Tooltip("Board-space direction the ball is sent (0,0,-1 = straight down the table).")]
        public Vector3 guideDirection = new Vector3(0f, 0f, -1f);

        [Title("Hooks")]
        public UltEvent onKick;

        void OnCollisionEnter(Collision collision)
        {
            var ball = collision.rigidbody ? collision.rigidbody.GetComponent<Ball>() : null;
            var context = ball ? ball.Context : null;
            if (context == null || !context.Run.IsPlaying || ball.Body.isKinematic) return;

            var board = context.Board;
            var away = Vector3.ProjectOnPlane(ball.Body.position - transform.position, board.Up).normalized;
            if (away.sqrMagnitude < 0.01f) away = -board.Forward;
            var direction = (away + board.Forward * upwardBias).normalized;
            if (guide)
            {
                direction = board.transform.TransformDirection(guideDirection).normalized;
                // Keep some separation from the post, even for off-centre hits.
                float separation = Vector3.Dot(direction, away);
                if (separation < 0.15f) direction = (direction + away * (0.15f - separation)).normalized;
            }
            ball.Body.linearVelocity = direction * kickSpeed;
            onKick?.Invoke();
        }
    }
}
