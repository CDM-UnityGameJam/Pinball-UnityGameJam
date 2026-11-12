using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pinball.Editor
{
    // Pinball > Build Web Game: builds Game.unity for WebGL into Builds/WebGL (needs the WebGL Build Support
    // module). Same web settings as the old prototype's build, but the editor's quality level and render pipeline
    // are restored afterwards instead of being left switched to the mobile setup.
    static class WebGLBuild
    {
        const string Scene = "Assets/_Project/Scenes/Game.unity";
        const string WebPipeline = "Assets/Settings/Mobile_RPAsset.asset";
        const int WebQualityLevel = 0;

        [MenuItem("Pinball/Build Web Game")]
        static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new BuildFailedException("WebGL Build Support isn't installed for this editor (Unity Hub > Installs > Add modules).");

            var output = Path.GetFullPath("Builds/WebGL");
            Directory.CreateDirectory(output);
            // Fallback decompression allows hosting on ordinary static web servers.
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "PROJECT:LuckyPinball";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;

            int quality = QualitySettings.GetQualityLevel();
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            try
            {
                // Web uses forward rendering: activate its pipeline before shader stripping.
                QualitySettings.SetQualityLevel(WebQualityLevel, true);
                GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(WebPipeline);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Scene },
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Web build failed: " + report.summary.result);
                Debug.Log("Web build succeeded: " + output);
            }
            finally
            {
                QualitySettings.SetQualityLevel(quality, true);
                GraphicsSettings.defaultRenderPipeline = pipeline;
            }
        }
    }
}
