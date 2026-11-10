using UnityEngine;

// Plays one VfxCue from this object. Play() can be called from scripts or UnityEvents.
[AddComponentMenu("Feedback/VFX Player")]
public class VfxPlayer : MonoBehaviour
{
    [SerializeField] VfxCue cue = new VfxCue();
    [SerializeField, Tooltip("Parent the effect to this object. It will be destroyed if this object is.")]
    bool followObject;

    [ContextMenu("Play")]
    public void Play()
    {
        if (followObject) cue.PlayAttached(transform);
        else cue.Play(transform.position, transform.rotation);
    }

    public void PlayAt(Vector3 position) => cue.Play(position);
}
