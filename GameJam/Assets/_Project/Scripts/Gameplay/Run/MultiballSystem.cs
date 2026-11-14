using System;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Extra balls (port of the v0.5 multiball integration):
    // - The second kill of one attack earns a ball, once per attack. An attack is a ball hit with its
    //   enchantments (and the burns they cause), or a whole played batch (every element, wave and burn).
    //   Up to GameConfig.multiball.maxBalls on the table (balls waiting to enter count); a ball earned while
    //   the table is full is lost, not banked.
    // - Earned balls enter one at a time at the board's ExtraBallEntry (top centre, above the enemies):
    //   held still for `entryHold` gameplay seconds, then, once no other ball sits on the entry, sent down the
    //   table at `entrySpeed`, angled `entryAngle` degrees to a random side. Straight down it fell through the gap
    //   between the flippers whenever no enemy was in the way. A bottom launch would only reach mid-table while
    //   the player is busy with another ball.
    // - Drains never cost lives (GameContext.OnDrained).
    public sealed class MultiballSystem
    {
        readonly GameContext context;
        MultiballRules Rules => context.config.multiball;

        int pending;   // earned, not spawned yet
        Ball held;     // spawned and waiting at the entry
        float holdLeft;
        readonly Collider[] overlap = new Collider[8];

        public int Pending => pending;
        public Ball Held => held;

        // A ball was earned and appeared at the entry (world position). For "+BALL" popups and sounds.
        public event Action<Ball> BallEarned;

        public MultiballSystem(GameContext context)
        {
            this.context = context;
            context.Score.MultiKill += OnMultiKill;
            context.Run.StateChanged += state => { if (state == RunState.GameOver) pending = 0; };
        }

        // MultiKill is raised on every kill from the second on: only the second earns a ball.
        void OnMultiKill(AttackRecord source)
        {
            if (Rules.ballOnMultiKill && source.Kills == 2) AddBall();
        }

        // Queue an extra ball. False when the run isn't playing or the table is full.
        public bool AddBall()
        {
            if (!context.Run.IsPlaying || context.Balls.Count + pending >= Mathf.Clamp(Rules.maxBalls, 1, 3)) return false;
            pending++;
            return true;
        }

        public bool IsHeld(Ball ball) => ball && ball == held;

        // Advance by gameplay time (0 while paused or counting down). GameContext calls it every frame.
        public void Tick(float deltaTime)
        {
            if (!context.Run.IsPlaying) return;
            if (held && !held.isActiveAndEnabled) held = null;
            if (!held)
            {
                if (pending == 0) return;
                pending--;
                held = context.Balls.Spawn(context.Board.extraBallEntry.Position);
                holdLeft = Rules.entryHold;
                BallEarned?.Invoke(held);
            }
            holdLeft -= deltaTime;
            if (holdLeft > 0f || !EntryClear()) return;

            var entry = context.Board.extraBallEntry;
            var ball = held;
            held = null;
            ball.Launch(entry.Position, EntryDirection(entry) * Rules.entrySpeed);
        }

        // A random angle in entryAngle, to a random side.
        Vector3 EntryDirection(BallLaunchPoint entry)
        {
            var range = Rules.entryAngle;
            float side = context.Random.Value() < 0.5f ? -1f : 1f;
            float angle = Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), context.Random.Value());
            return entry.DirectionAt(side * angle);
        }

        // No other ball overlapping the entry, so a dropped ball never starts inside another.
        bool EntryClear()
        {
            float radius = BallRadius(held) * 2f + Rules.entryClearance;
            int count = Physics.OverlapSphereNonAlloc(context.Board.extraBallEntry.Position, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = overlap[i].attachedRigidbody ? overlap[i].attachedRigidbody.GetComponent<Ball>() : null;
                if (other && other != held) return false;
            }
            return true;
        }

        static float BallRadius(Ball ball)
        {
            var sphere = ball.GetComponent<SphereCollider>();
            if (!sphere) return 0.375f;
            var s = ball.transform.lossyScale;
            return sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
    }
}
