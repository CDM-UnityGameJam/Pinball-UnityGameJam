using System;
using UnityEngine;

// A one-shot particle effect that can be added as a field to any script.
// Each play spawns a copy that destroys itself when finished. An empty cue does nothing.
[Serializable]
public class VfxCue
{
    public ParticleSystem prefab;
    [Tooltip("World-space offset added to the spawn position, such as lifting the effect off the board.")]
    public Vector3 offset;
    [Min(0f), Tooltip("Seconds to ignore repeat plays of this cue, so rapid contacts don't stack up.")]
    public float minInterval = 0.05f;

    double lastPlayed = double.NegativeInfinity;

    public void Play(Vector3 position)
    {
        if (prefab) Play(position, prefab.transform.rotation);
    }

    // Spawned unparented, so the effect outlives whatever triggered it.
    public void Play(Vector3 position, Quaternion rotation)
    {
        if (Ready()) Finish(UnityEngine.Object.Instantiate(prefab, position + offset, rotation));
    }

    // The effect moves with the target, and is destroyed along with it.
    public void PlayAttached(Transform target)
    {
        if (target && Ready()) Finish(UnityEngine.Object.Instantiate(prefab, target.position + offset, target.rotation, target));
    }

    bool Ready()
    {
        if (!Application.isPlaying || !prefab) return false;
        double now = Time.unscaledTimeAsDouble;
        if (now - lastPlayed < minInterval) return false;
        lastPlayed = now;
        return true;
    }

    static void Finish(ParticleSystem effect)
    {
        var main = effect.main;
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.Destroy;
        effect.Play(true);
    }
}
