using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildServer
{
    private const string OutputPath = "Builds/LinuxServer/Pong.x86_64";

    [MenuItem("Pong/Build Linux Dedicated Server")]
    public static void Linux()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) { Debug.LogError("[Build] No scenes in Build Settings."); return; }

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.IL2CPP);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes           = scenes,
            locationPathName = OutputPath,
            target           = BuildTarget.StandaloneLinux64,
            subtarget        = (int)StandaloneBuildSubtarget.Server,
            options          = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[Build] Linux Dedicated Server OK: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0} s).");
        }
        else
        {
            Debug.LogError($"[Build] FAILED: {summary.result}, {summary.totalErrors} error(s).");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
