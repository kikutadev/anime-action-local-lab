using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AnimeActionWebBuild
{
    [MenuItem("Anime Action/Build WebGL")]
    public static void BuildWebGl()
    {
        AnimeActionAcceptance.Validate();
        AnimeActionSetup.GenerateDemo();
        AnimeActionAcceptance.ValidateGeneratedScene();

        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string output = Path.GetFullPath(Path.Combine(projectRoot, "..", "publish"));
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }
        Directory.CreateDirectory(output);

        PlayerSettings.productName = "Anime Action Local Lab";
        PlayerSettings.companyName = "kikutadev";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, "dev.kikuta.animeactionlocallab");
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.memorySize = 512;
        PlayerSettings.runInBackground = true;

        BuildPlayerOptions options = new()
        {
            scenes = new[] { "Assets/Generated/AnimeActionDemo.unity" },
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.CleanBuildCache,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"WebGL build failed: {report.summary.result}, errors={report.summary.totalErrors}");
        }

        File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
        Debug.Log($"WebGL build succeeded: {output} ({report.summary.totalSize} bytes)");
    }
}
