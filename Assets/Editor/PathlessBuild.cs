using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

/// <summary>Builds the playable Pathless entry scene for Windows x64.</summary>
public static class PathlessBuild
{
    public static void BuildWindows()
    {
        string outputPath = Environment.GetEnvironmentVariable("PATHLESS_BUILD_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputPath))
            outputPath = "Builds/Windows/Pathless.exe";

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Pathless Windows build failed: " + report.summary.result);
    }
}
