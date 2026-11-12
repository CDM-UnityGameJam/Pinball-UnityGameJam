using UnityEngine;

namespace Pinball.Gameplay
{
    // Ball contact. Port of the old Enemy.TryBallHit / NudgeSlowBall:
    // - A valid hit (playing, enemy alive, ball moving freely, cooldown passed on the combat clock) deals
    //   GameConfig.ballHitDamage, then reports the hit so held enchantments can trigger. A lethal hit still
    //   reports, with the contact point.
    // - A ball touching the enemy slower than the unstick speed is pushed away every physics step,
    //   so it can't come to rest against it. That push never counts as a hit.
    public sealed class EnemyContact : MonoBehaviour
    {
        Enemy enemy;
        double lastHit = double.NegativeInfinity;

        // (ball, contact point) for every valid hit, after its base damage. For presentation hooks.
        public event System.Action<Ball, Vector3> BallHit;

        internal void ResetFor(Enemy owner)
        {
            enemy = owner;
            lastHit = double.NegativeInfinity;
        }

        void OnCollisionEnter(Collision collision)
        {
            Nudge(collision);
            var ball = BallOf(collision);
            if (ball) TryBallHit(ball, collision.contactCount > 0 ? collision.GetContact(0).point : transform.position);
        }

        void OnCollisionStay(Collision collision) => Nudge(collision);

        static Ball BallOf(Collision collision) => collision.rigidbody ? collision.rigidbody.GetComponent<Ball>() : null;

        public bool TryBallHit(Ball ball, Vector3 point)
        {
            var context = enemy ? enemy.Context : null;
            if (context == null || !context.Run.IsPlaying || !enemy.Alive || !ball || ball.Body.isKinematic) return false;
            if (context.Clock.Now - lastHit < enemy.Definition.hitCooldown - Core.RuleMath.TimeEpsilon) return false;
            lastHit = context.Clock.Now;
            enemy.Health.TakeDamage(context.config.ballHitDamage);
            BallHit?.Invoke(ball, point);
            context.Enemies.ReportBallHit(enemy, ball, point);
            return true;
        }

        void Nudge(Collision collision)
        {
            var context = enemy ? enemy.Context : null;
            var ball = BallOf(collision);
            if (context == null || !context.Run.IsPlaying || !enemy.Health.Alive || !ball || ball.Body.isKinematic) return;

            var board = context.Board;
            var unstick = enemy.Definition.unstick;
            var velocity = Vector3.ProjectOnPlane(ball.Body.linearVelocity, board.Up);
            if (velocity.sqrMagnitude >= unstick.speed * unstick.speed) return;

            var away = Vector3.ProjectOnPlane(ball.Body.position - transform.position, board.Up).normalized;
            // A shared small diagonal bias keeps opposing enemies from cancelling each other out.
            var direction = (away + board.Forward * unstick.forwardBias + board.transform.right * unstick.sideBias).normalized;
            ball.Body.AddForce(direction * unstick.acceleration, ForceMode.Acceleration);
        }
    }
}
