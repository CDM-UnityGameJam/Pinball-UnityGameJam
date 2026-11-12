using UnityEngine;

namespace Pinball.Feedback
{
    // Plays a VfxCue asset from this object. The prefab and offset are set on the asset.
    // Play() can be called from scripts or UnityEvents.
    [AddComponentMenu("Pinball/Feedback/VFX Player")]
    public class VfxPlayer : MonoBehaviour
    {
        [SerializeField] VfxCue cue;
        [SerializeField, Tooltip("Parent the effect to this object. It will be destroyed if this object is.")]
        bool followObject;

        public VfxCue Cue { get => cue; set => cue = value; }

        [ContextMenu("Play")]
        public void Play()
        {
            if (!cue) return;
            if (followObject) cue.PlayAttached(transform);
            else cue.Play(transform.position, transform.rotation);
        }

        public void PlayAt(Vector3 position)
        {
            if (cue) cue.Play(position);
        }
    }
}
