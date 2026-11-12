using System;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Trigger below the flippers. Reports every ball that falls in.
    [RequireComponent(typeof(Collider))]
    public sealed class DrainZone : MonoBehaviour
    {
        public event Action<Ball> Drained;

        void OnTriggerEnter(Collider other)
        {
            var ball = other.attachedRigidbody ? other.attachedRigidbody.GetComponent<Ball>() : null;
            if (ball) Drained?.Invoke(ball);
        }
    }
}
