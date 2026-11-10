using System;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

[Serializable]
public struct SfxParameter
{
    public string name;
    public float value;

    public SfxParameter(string name, float value)
    {
        this.name = name;
        this.value = value;
    }
}

// One FMOD one-shot event as an asset (Create > Feedback > SFX Cue).
// Reference it from an SfxPlayer, or from any script as a field and call Play.
// A cue with no event picked does nothing, so it can be wired up before the sound exists.
[CreateAssetMenu(fileName = "NewSfxCue", menuName = "Feedback/SFX Cue")]
public class SfxCue : ScriptableObject
{
    public EventReference sound;
    [Min(0f), Tooltip("Seconds to ignore repeat plays of this cue from any object, so rapid contacts don't stack up.")]
    public float minInterval = 0.05f;
    [Tooltip("Applied on every play. Values from an SfxPlayer or from code override these.")]
    public SfxParameter[] parameters;

    [NonSerialized] double lastPlayed = double.NegativeInfinity;

    public void Play(Vector3 position) => Fire(position, null, null, null, 0f);
    public void Play(Vector3 position, string parameter, float value) => Fire(position, null, null, parameter, value);
    public void Play(Vector3 position, params SfxParameter[] overrides) => Fire(position, null, overrides, null, 0f);

    public void PlayAttached(GameObject target)
    {
        if (target) Fire(target.transform.position, target, null, null, 0f);
    }

    public void PlayAttached(GameObject target, string parameter, float value)
    {
        if (target) Fire(target.transform.position, target, null, parameter, value);
    }

    public void PlayAttached(GameObject target, params SfxParameter[] overrides)
    {
        if (target) Fire(target.transform.position, target, overrides, null, 0f);
    }

    // A null target plays at the position; otherwise the sound follows the target.
    internal void Fire(Vector3 position, GameObject target, SfxParameter[] overrides, string parameter, float value)
    {
        if (!Application.isPlaying || sound.IsNull) return;
        double now = Time.unscaledTimeAsDouble;
        // In the editor this asset keeps lastPlayed after Play mode ends but the clock restarts,
        // so a lastPlayed in the future means a new session.
        if (now >= lastPlayed && now - lastPlayed < minInterval) return;
        lastPlayed = now;

        bool hasParameters = parameter != null || overrides is { Length: > 0 } || parameters is { Length: > 0 };
        if (!hasParameters)
        {
            if (target) RuntimeManager.PlayOneShotAttached(sound, target);
            else RuntimeManager.PlayOneShot(sound, position);
            return;
        }

        // PlayOneShot can't set parameters, so this does the same steps by hand.
        EventInstance instance;
        try
        {
            instance = RuntimeManager.CreateInstance(sound);
        }
        catch (EventNotFoundException)
        {
            Debug.LogWarning($"[Sfx] Event not found on {name}: {sound}", this);
            return;
        }

        if (target) RuntimeManager.AttachInstanceToGameObject(instance, target, target.GetComponent<Rigidbody>());
        else instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));

        Apply(instance, parameters);
        Apply(instance, overrides);
        if (parameter != null) Apply(instance, parameter, value);

        instance.start();
        instance.release();
    }

    void Apply(EventInstance instance, SfxParameter[] values)
    {
        if (values == null) return;
        foreach (var entry in values) Apply(instance, entry.name, entry.value);
    }

    void Apply(EventInstance instance, string parameterName, float value)
    {
        if (string.IsNullOrEmpty(parameterName)) return;
        // One-shots are short, so skip the seek speed and use the value immediately.
        var result = instance.setParameterByName(parameterName, value, true);
        if (result != FMOD.RESULT.OK) Debug.LogWarning($"[Sfx] Could not set parameter '{parameterName}' on {name}: {result}", this);
    }
}
