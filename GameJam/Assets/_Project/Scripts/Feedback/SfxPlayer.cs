using UnityEngine;

namespace Pinball.Feedback
{
    // Plays an SfxCue asset from this object. The FMOD event and parameters are set on the asset.
    // Play() can be called from scripts or UnityEvents.
    [AddComponentMenu("Pinball/Feedback/SFX Player")]
    public class SfxPlayer : MonoBehaviour
    {
        [SerializeField] SfxCue cue;
        [SerializeField, Tooltip("Keep the sound on this object as it moves, instead of leaving it where it started.")]
        bool followObject;

        public SfxCue Cue { get => cue; set => cue = value; }

        [ContextMenu("Play")]
        public void Play() => Fire(null, 0f);

        public void Play(string parameter, float value) => Fire(parameter, value);

        public void PlayAt(Vector3 position)
        {
            if (cue) cue.Play(position);
        }

        void Fire(string parameter, float value)
        {
            if (cue) cue.Fire(transform.position, followObject ? gameObject : null, null, parameter, value);
        }
    }
}
