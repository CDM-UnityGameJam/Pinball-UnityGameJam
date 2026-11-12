using FMODUnity;
using UnityEngine;

namespace Pinball.Feedback
{
    public static class Sfx
    {
        public static void SetGlobal(string name, float value, bool ignoreSeekSpeed = false)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(name)) return;
            var result = RuntimeManager.StudioSystem.setParameterByName(name, value, ignoreSeekSpeed);
            if (result != FMOD.RESULT.OK) Debug.LogWarning($"[Sfx] Could not set global parameter '{name}': {result}");
        }
    }
}
