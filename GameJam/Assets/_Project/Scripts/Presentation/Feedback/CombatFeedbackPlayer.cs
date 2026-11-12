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

        [Title("Run")]
        [Tooltip("A life is lost (drain or breach).")]
        public SfxCue lifeLostSfx;
        public SfxCue gameOverSfx;

        int lives;

        void Start()
        {
            context.Feedback.Raised += OnFeedback;
            context.Casts.CastResolved += OnCast;
            lives = context.Run.Lives;
            context.Run.LivesChanged += OnLivesChanged;
            context.Run.StateChanged += state => { if (state == RunState.GameOver && gameOverSfx) gameOverSfx.Play(Listener); };
        }

        void OnDestroy()
        {
            if (!context || !context.IsInitialized) return;
            context.Feedback.Raised -= OnFeedback;
            context.Casts.CastResolved -= OnCast;
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
                    if (e.element.activeHitVfx) e.element.activeHitVfx.Play(request);
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

        void OnCast(CastResult cast)
        {
            var sfx = cast.card.element ? cast.card.element.castSfx : null;
            if (cast.outcome == CastOutcome.Cast && sfx) sfx.Play(Listener);
        }

        void OnLivesChanged(int now)
        {
            if (now < lives && lifeLostSfx) lifeLostSfx.Play(Listener);
            lives = now;
        }
    }
}
