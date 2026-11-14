using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pinball.Editor
{
    // Pinball > Build Windows Game: builds Game.unity as a 64-bit Windows player into Builds/Windows
    // (LuckyPinball.exe plus its _Data folder; ship the whole folder). Windows players run the PC quality level
    // (the Mobile level excludes Standalone), so the PC pipeline is made active for shader stripping, and the
    // editor's quality level and pipeline are restored afterwards.
    static class WindowsBuild
    {
        const string Scene = "Assets/_Project/Scenes/Game.unity";
        const string PcPipeline = "Assets/Settings/PC_RPAsset.asset";
        const int PcQualityLevel = 1;
        const string ExeName = "LuckyPinball.exe";

        [MenuItem("Pinball/Build Windows Game")]
        static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Windows Build Support isn't installed for this editor (Unity Hub > Installs > Add modules).");

            var folder = Path.GetFullPath("Builds/Windows");
            Directory.CreateDirectory(folder);
            var exe = Path.Combine(folder, ExeName);

            // URP keeps only the pipeline assets and shader variants of the *active* platform's quality levels, so a
            // build for another platform than the active one strips what its player needs. Switch first; the switch
            // reimports assets and reloads scripts, so run the menu item again afterwards.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                Debug.Log("Switching the active platform to Windows first. Run this menu item again when the editor is ready.");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                return;
            }

            int quality = QualitySettings.GetQualityLevel();
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            try
            {
                QualitySettings.SetQualityLevel(PcQualityLevel, true);
                GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PcPipeline);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Scene },
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Windows build failed: " + report.summary.result);
                if (!File.Exists(exe))
                    throw new BuildFailedException("Windows build wrote no player to " + exe +
                        ". If Windows Build Support was just installed, restart the Unity editor and build again.");
                Debug.Log("Windows build succeeded: " + exe);
            }
            finally
            {
                QualitySettings.SetQualityLevel(quality, true);
                GraphicsSettings.defaultRenderPipeline = pipeline;
            }
        }
    }
}
