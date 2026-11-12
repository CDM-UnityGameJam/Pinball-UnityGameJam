using System.Collections.Generic;
using Pinball.Gameplay;
using UnityEngine;

namespace Pinball.Presentation
{
    // A ball aura prefab: animated around the ball, lying in the board's plane.
    public interface IBallAura
    {
        // center: ball position; frame: board rotation; phase: combat-time based animation phase.
        void UpdateAura(Vector3 center, Quaternion frame, float phase);
        void SetVisible(bool visible);
        bool Visible { get; }
    }

    // Shows each element's ballAuraPrefab on the ball while that element enchants it (at least one enabled
    // card of that element is held; short-circuited elements show nothing).
    [RequireComponent(typeof(Ball))]
    public sealed class BallAuraView : MonoBehaviour
    {
        [Tooltip("Animation speed (old prototype: 5 per second of combat time).")]
        public float phaseSpeed = 5f;

        readonly Dictionary<ElementDefinition, IBallAura> auras = new Dictionary<ElementDefinition, IBallAura>();
        Ball ball;

        public bool IsShowing(ElementDefinition element) => auras.TryGetValue(element, out var aura) && aura != null && aura.Visible;

        void Awake() => ball = GetComponent<Ball>();

        void LateUpdate()
        {
            var context = ball.Context;
            if (context == null || !context.IsInitialized) return;
            float phase = (float)context.Clock.Now * phaseSpeed;
            var frame = context.Board.transform.rotation;
            foreach (var element in context.config.elements)
            {
                var aura = AuraFor(element);
                if (aura == null) continue;
                bool visible = context.Hand.EnabledCount(element) > 0;
                aura.SetVisible(visible);
                if (visible) aura.UpdateAura(transform.position, frame, phase);
            }
        }

        IBallAura AuraFor(ElementDefinition element)
        {
            if (auras.TryGetValue(element, out var aura)) return aura;
            if (element.ballAuraPrefab)
            {
                var go = Instantiate(element.ballAuraPrefab, transform);
                go.name = element.ballAuraPrefab.name;
                aura = go.GetComponent<IBallAura>();
            }
            auras[element] = aura;
            return aura;
        }
    }
}
