using System;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Hit points. Damage only counts while the run is playing; reaching zero kills the enemy once.
    public sealed class EnemyHealth : MonoBehaviour
    {
        public int Current { get; private set; }
        public int Max { get; private set; }
        public bool Alive => Current > 0;

        // (amount, source). Raised for every damage that lands, including the killing blow.
        public event Action<int, AttackRecord> Damaged;
        // (source). Raised once, after the killing Damaged.
        public event Action<AttackRecord> Died;

        GameContext context;

        internal void ResetFor(int maxHealth, GameContext context)
        {
            this.context = context;
            Max = Current = Mathf.Max(1, maxHealth);
        }

        // For tests and debug tools.
        internal void SetCurrent(int value)
        {
            Max = Mathf.Max(Max, value);
            Current = value;
        }

        // Returns true if damage landed.
        public bool TakeDamage(int amount, AttackRecord source = null)
        {
            if (!Alive || !isActiveAndEnabled || context == null || !context.Run.IsPlaying) return false;
            amount = Mathf.Max(0, amount);
            if (amount == 0) return false;
            Current = Mathf.Max(0, Current - amount);
            Damaged?.Invoke(amount, source);
            if (Current > 0) return true;
            Died?.Invoke(source);
            return true;
        }

        // Breach: dies without counting as a kill.
        internal void Kill() => Current = 0;
    }
}
