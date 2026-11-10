using UnityEngine;

// Plays one SfxCue from this object. Play() can be called from scripts or UnityEvents.
[AddComponentMenu("Feedback/SFX Player")]
public class SfxPlayer : MonoBehaviour
{
    [SerializeField] SfxCue cue = new SfxCue();
    [SerializeField, Tooltip("Keep the sound on this object as it moves, instead of leaving it where it started.")]
    bool followObject;

    [ContextMenu("Play")]
    public void Play()
    {
        if (followObject) cue.PlayAttached(gameObject);
        else cue.Play(transform.position);
    }

    public void Play(string parameter, float value)
    {
        if (followObject) cue.PlayAttached(gameObject, parameter, value);
        else cue.Play(transform.position, parameter, value);
    }

    public void PlayAt(Vector3 position) => cue.Play(position);
}
