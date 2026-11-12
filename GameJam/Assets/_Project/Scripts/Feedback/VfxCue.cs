using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pinball.Feedback
{
    // One visual effect as an asset (Create > Pinball > Feedback > VFX Cue). Reference it from a VfxPlayer,
    // a UltEvent, or any script field, and call Play. Instances are pooled: a finished effect goes back to the
    // pool instead of being destroyed. A cue with no prefab does nothing.
    //
    // The prefab can hold Particle Systems and/or IVfxEffect components (line bolts, rings...). It counts as
    // finished when every particle system has died and every IVfxEffect reports done.
    [CreateAssetMenu(fileName = "NewVfxCue", menuName = "Pinball/Feedback/VFX Cue")]
    public class VfxCue : ScriptableObject
    {
        public GameObject prefab;
        [Tooltip("World-space offset added to the spawn position, such as lifting the effect off the board.")]
        public Vector3 offset;
        [Min(0f), Tooltip("Seconds to ignore repeat plays of this cue from any object. 0 = never throttle " +
                          "(effects that fire for several targets at once need 0).")]
        public float minInterval;

        [NonSerialized] double lastPlayed = double.NegativeInfinity;
        [NonSerialized] readonly Stack<VfxInstance> pool = new Stack<VfxInstance>();
        [NonSerialized] int active;

        public int ActiveCount => active;
        public int PooledCount => pool.Count;

        public void Play(Vector3 position) => Play(position, prefab ? prefab.transform.rotation : Quaternion.identity);

        public void Play(Vector3 position, Quaternion rotation) => Play(VfxRequest.At(position, rotation));

        public void Play(in VfxRequest request)
        {
            if (!Ready()) return;
            var r = request;
            r.position += offset;
            Spawn(null).Begin(this, r);
        }

        // The effect moves with the target (and is lost if the target is destroyed).
        public void PlayAttached(Transform target)
        {
            if (!target || !Ready()) return;
            var instance = Spawn(target);
            instance.Begin(this, VfxRequest.At(target.position + offset, target.rotation));
        }

        bool Ready()
        {
            if (!Application.isPlaying || !prefab) return false;
            double now = Time.unscaledTimeAsDouble;
            // In the editor this asset keeps lastPlayed after Play mode ends but the clock restarts,
            // so a lastPlayed in the future means a new session.
            if (minInterval > 0f && now >= lastPlayed && now - lastPlayed < minInterval) return false;
            lastPlayed = now;
            return true;
        }

        VfxInstance Spawn(Transform parent)
        {
            VfxInstance instance = null;
            // Pooled instances can be gone after a scene change or when an attached target was destroyed.
            while (pool.Count > 0 && !instance) instance = pool.Pop();
            if (!instance)
            {
                var go = Instantiate(prefab);
                go.name = prefab.name;
                instance = go.GetComponent<VfxInstance>();
                if (!instance) instance = go.AddComponent<VfxInstance>();
            }
            instance.transform.SetParent(parent ? parent : VfxInstance.PoolRoot, false);
            instance.gameObject.SetActive(true);
            active++;
            return instance;
        }

        internal void Release(VfxInstance instance)
        {
            active = Math.Max(0, active - 1);
            if (!instance) return;
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(VfxInstance.PoolRoot, false);
            pool.Push(instance);
        }

        void OnEnable()
        {
            pool.Clear();
            active = 0;
        }
    }
}
