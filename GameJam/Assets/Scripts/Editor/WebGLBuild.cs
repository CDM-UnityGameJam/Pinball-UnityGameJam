using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class WebGLBuild
{
    [MenuItem("LuckyPinball/Build Web Game")]
    public static void Build()
    {
        ScoringCheck.Run();
        CardMathCheck.Run();
        var output = Path.GetFullPath("Builds/WebGL");
        Directory.CreateDirectory(output);
        // Fallback decompression allows hosting on ordinary static web servers.
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "PROJECT:LuckyPinball";
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        // Web uses forward rendering. Activate its pipeline before shader stripping,
        // rather than stripping against the editor's deferred PC quality level.
        QualitySettings.SetQualityLevel(0, true);
        GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Pinball.unity" },
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Web build failed: " + report.summary.result);
        UnityEngine.Debug.Log("Web build succeeded: " + output);
    }
}
