using System;
using UnityEngine;

// One particle effect as an asset (Create > Feedback > VFX Cue).
// Reference it from a VfxPlayer, or from any script as a field and call Play.
// Each play spawns a copy that destroys itself when finished. A cue with no prefab does nothing.
[CreateAssetMenu(fileName = "NewVfxCue", menuName = "Feedback/VFX Cue")]
public class VfxCue : ScriptableObject
{
    public ParticleSystem prefab;
    [Tooltip("World-space offset added to the spawn position, such as lifting the effect off the board.")]
    public Vector3 offset;
    [Min(0f), Tooltip("Seconds to ignore repeat plays of this cue from any object, so rapid contacts don't stack up.")]
    public float minInterval = 0.05f;

    [NonSerialized] double lastPlayed = double.NegativeInfinity;

    public void Play(Vector3 position)
    {
        if (prefab) Play(position, prefab.transform.rotation);
    }

    // Spawned unparented, so the effect outlives whatever triggered it.
    public void Play(Vector3 position, Quaternion rotation)
    {
        if (Ready()) Finish(Instantiate(prefab, position + offset, rotation));
    }

    // The effect moves with the target, and is destroyed along with it.
    public void PlayAttached(Transform target)
    {
        if (target && Ready()) Finish(Instantiate(prefab, target.position + offset, target.rotation, target));
    }

    bool Ready()
    {
        if (!Application.isPlaying || !prefab) return false;
        double now = Time.unscaledTimeAsDouble;
        // In the editor this asset keeps lastPlayed after Play mode ends but the clock restarts,
        // so a lastPlayed in the future means a new session.
        if (now >= lastPlayed && now - lastPlayed < minInterval) return false;
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
