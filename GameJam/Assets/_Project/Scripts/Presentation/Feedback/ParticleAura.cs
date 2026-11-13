using System.Collections.Generic;
using UnityEngine;

namespace Pinball.Presentation
{
    // A ball aura made of particle systems (the held-card effects): it follows the ball, lies in the board's
    // frame, and emits while the element enchants the ball. Hiding stops emission and lets live particles fade.
    public sealed class ParticleAura : MonoBehaviour, IBallAura
    {
        [Tooltip("Height above the ball centre.")] public float lift = 0f;
        [Tooltip("Systems that start and stop with the aura. Empty = every system under this object.")]
        public List<ParticleSystem> emitters = new List<ParticleSystem>();

        bool visible;
        bool initialised;

        public bool Visible => visible;

        void Init()
        {
            if (initialised) return;
            initialised = true;
            if (emitters.Count == 0) GetComponentsInChildren(true, emitters);
            foreach (var ps in emitters) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void SetVisible(bool show)
        {
            Init();
            if (visible == show) return;
            visible = show;
            foreach (var ps in emitters)
            {
                if (show) ps.Play(true);
                else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public void UpdateAura(Vector3 center, Quaternion frame, float phase)
        {
            Init();
            transform.SetPositionAndRotation(center + frame * Vector3.up * lift, frame);
        }

        void OnDisable()
        {
            visible = false;
            initialised = false;
        }
    }
}
