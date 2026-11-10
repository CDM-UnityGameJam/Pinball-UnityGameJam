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

// A one-shot FMOD event that can be added as a field to any script.
// An empty cue does nothing, so calls can be wired up before the sound exists.
[Serializable]
public class SfxCue
{
    public EventReference sound;
    [Min(0f), Tooltip("Seconds to ignore repeat plays of this cue, so rapid contacts don't stack up.")]
    public float minInterval = 0.05f;
    [Tooltip("Applied on every play. Values passed from code override these.")]
    public SfxParameter[] parameters;

    double lastPlayed = double.NegativeInfinity;

    public void Play(Vector3 position) => Fire(position, null, null, 0f, null);
    public void Play(Vector3 position, string parameter, float value) => Fire(position, null, parameter, value, null);
    public void Play(Vector3 position, params SfxParameter[] overrides) => Fire(position, null, null, 0f, overrides);

    public void PlayAttached(GameObject target)
    {
        if (target) Fire(target.transform.position, target, null, 0f, null);
    }

    public void PlayAttached(GameObject target, string parameter, float value)
    {
        if (target) Fire(target.transform.position, target, parameter, value, null);
    }

    public void PlayAttached(GameObject target, params SfxParameter[] overrides)
    {
        if (target) Fire(target.transform.position, target, null, 0f, overrides);
    }

    void Fire(Vector3 position, GameObject target, string parameter, float value, SfxParameter[] overrides)
    {
        if (!Application.isPlaying || sound.IsNull) return;
        double now = Time.unscaledTimeAsDouble;
        if (now - lastPlayed < minInterval) return;
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
            Debug.LogWarning("[Sfx] Event not found: " + sound);
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

    void Apply(EventInstance instance, string name, float value)
    {
        if (string.IsNullOrEmpty(name)) return;
        // One-shots are short, so skip the seek speed and use the value immediately.
        var result = instance.setParameterByName(name, value, true);
        if (result != FMOD.RESULT.OK) Debug.LogWarning($"[Sfx] Could not set parameter '{name}' on {sound}: {result}");
    }
}
