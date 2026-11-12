using System.Collections.Generic;
using UnityEngine;

namespace Pinball.Feedback
{
    // Drives one spawned VFX until it finishes, then hands it back to its cue's pool. Added automatically.
    // Effects run on scaled time, so they freeze with the game (pause, launch wait, game over).
    public sealed class VfxInstance : MonoBehaviour
    {
        static Transform poolRoot;

        // Scene-level parent for idle and unattached effects.
        internal static Transform PoolRoot
        {
            get
            {
                if (!poolRoot) poolRoot = new GameObject("VFX Pool").transform;
                return poolRoot;
            }
        }

        readonly List<IVfxEffect> effects = new List<IVfxEffect>();
        readonly List<ParticleSystem> particles = new List<ParticleSystem>();
        VfxCue cue;
        bool running;

        internal void Begin(VfxCue owner, in VfxRequest request)
        {
            cue = owner;
            if (effects.Count == 0) GetComponentsInChildren(true, effects);
            if (particles.Count == 0) GetComponentsInChildren(true, particles);
            transform.SetPositionAndRotation(request.position, request.rotation);
            foreach (var effect in effects) effect.Begin(request);
            foreach (var ps in particles)
            {
                var main = ps.main;
                main.loop = false;
                main.stopAction = ParticleSystemStopAction.None;
                ps.Clear(true);
                ps.Play(true);
            }
            running = true;
        }

        void Update()
        {
            if (!running) return;
            bool alive = false;
            float dt = Time.deltaTime;
            foreach (var effect in effects) alive |= effect.Tick(dt);
            foreach (var ps in particles) alive |= ps && ps.IsAlive(true);
            if (alive) return;
            running = false;
            cue.Release(this);
        }

        void OnDestroy()
        {
            if (running && cue) cue.Release(null);
        }
    }
}
