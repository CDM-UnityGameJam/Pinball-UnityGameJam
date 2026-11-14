using Pinball.Core;
using Pinball.Feedback;
using Pinball.Gameplay;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Presentation
{
    // Turns gameplay feedback events into VFX and SFX. Element-specific cues live on each ElementDefinition
    // (passive/active hit VFX and SFX, cast SFX); status and run cues live here. Every cue is an asset, so
    // artists and sound designers swap effects without code.
    public sealed class CombatFeedbackPlayer : MonoBehaviour
    {
        [Required, SceneObjectsOnly] public GameContext context;

        [Title("Status")]
        [Tooltip("An enemy starts or refreshes burning from a ball enchantment; also each passive burn tick.")]
        public VfxCue igniteVfx;
        [Tooltip("Same, for skill (active) burns.")]
        public VfxCue igniteStrongVfx;
        public SfxCue igniteSfx;

        [Title("Cards and score")]
        [Tooltip("A batch of cards was played.")]
        public SfxCue cardsPlayedSfx;
        [Tooltip("The score went up (kills, time, combos).")]
        public SfxCue scoreSfx;
        [Tooltip("An enemy reacts to every new ball contact, at the enemy.")]
        public SfxCue enemyContactSfx;

        [Title("Run")]
        [Tooltip("A life is lost (an enemy breached).")]
        public SfxCue lifeLostSfx;
        public SfxCue gameOverSfx;

        int lives;

        void Start()
        {
            context.Feedback.Raised += OnFeedback;
            context.Casts.BatchCast += OnBatchCast;
            context.Hand.BatchPlayed += OnBatchPlayed;
            context.Score.ScoreChanged += OnScoreChanged;
            context.Enemies.BallContact += OnBallContact;
            lives = context.Run.Lives;
            context.Run.LivesChanged += OnLivesChanged;
            context.Run.StateChanged += state => { if (state == RunState.GameOver && gameOverSfx) gameOverSfx.Play(Listener); };
        }

        void OnDestroy()
        {
            if (!context || !context.IsInitialized) return;
            context.Feedback.Raised -= OnFeedback;
            context.Casts.BatchCast -= OnBatchCast;
            context.Hand.BatchPlayed -= OnBatchPlayed;
            context.Score.ScoreChanged -= OnScoreChanged;
            context.Enemies.BallContact -= OnBallContact;
            context.Run.LivesChanged -= OnLivesChanged;
        }

        Quaternion Frame => context.Board.transform.rotation;
        Vector3 Listener => Camera.main ? Camera.main.transform.position : context.Board.transform.position;

        void OnFeedback(CombatFeedbackEvent e)
        {
            var request = new VfxRequest { position = e.position, rotation = Frame, from = e.from, radius = e.radius, strong = e.strong };
            switch (e.kind)
            {
                case FeedbackKind.PassiveHit:
                case FeedbackKind.PassivePulse:
                    if (e.element.passiveHitVfx) e.element.passiveHitVfx.Play(request);
                    if (e.element.passiveHitSfx) e.element.passiveHitSfx.Play(e.position);
                    break;
                case FeedbackKind.ActiveHit:
                    var hit = e.strong && e.element.luckyHitVfx ? e.element.luckyHitVfx : e.element.activeHitVfx;
                    if (hit) hit.Play(request);
                    if (e.element.activeHitSfx) e.element.activeHitSfx.Play(e.position);
                    break;
                case FeedbackKind.Ignite:
                case FeedbackKind.BurnTick:
                    var cue = e.strong && igniteStrongVfx ? igniteStrongVfx : igniteVfx;
                    if (cue) cue.Play(request);
                    if (e.kind == FeedbackKind.Ignite && igniteSfx) igniteSfx.Play(e.position);
                    break;
            }
        }

        // Every element in the group casts at once.
        void OnBatchCast(CastBatch batch)
        {
            foreach (var cast in batch.Casts)
                if (cast.element && cast.element.castSfx) cast.element.castSfx.Play(Listener);
        }

        void OnBatchPlayed(CastBatch batch)
        {
            if (cardsPlayedSfx) cardsPlayedSfx.Play(Listener);
        }

        void OnScoreChanged(long score)
        {
            if (scoreSfx) scoreSfx.Play(Listener);
        }

        void OnBallContact(Enemy enemy, Ball ball, Vector3 point, bool damageAllowed)
        {
            if (enemyContactSfx && enemy) enemyContactSfx.Play(enemy.transform.position);
        }

        void OnLivesChanged(int now)
        {
            if (now < lives && lifeLostSfx) lifeLostSfx.Play(Listener);
            lives = now;
        }
    }
}
