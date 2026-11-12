using UnityEditor;
using UnityEditorInternal;

namespace Pinball.Editor
{
    // Keeps Play Mode ticking while the Unity Editor is not the focused window, so play-mode checks
    // (including ones driven through the Unity MCP tools) don't freeze when another app is in front.
    // Toggle it from the menu: Pinball > Keep Play Mode Running In Background (per user, on by default).
    [InitializeOnLoad]
    static class BackgroundPlayMode
    {
        const string MenuPath = "Pinball/Keep Play Mode Running In Background";
        const string PrefKey = "Pinball.BackgroundPlayMode";
        const double FrameInterval = 1.0 / 60.0;

        static double nextFrameTime;

        static BackgroundPlayMode() => EditorApplication.update += Tick;

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static void Tick()
        {
            if (!Enabled || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (InternalEditorUtility.isApplicationActive) return; // focused: Unity drives frames itself

            double now = EditorApplication.timeSinceStartup;
            if (now < nextFrameTime) return;
            nextFrameTime = now + FrameInterval;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        [MenuItem(MenuPath)]
        static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
