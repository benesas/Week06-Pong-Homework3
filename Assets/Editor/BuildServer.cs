// Pong — builds the Linux Dedicated Server with one menu click or from the command line (Week 4, edge).
//   Menu:          Pong > Build Linux Dedicated Server
//   Command line:  Unity -batchmode -quit -projectPath . -executeMethod BuildServer.Linux
// Output: Builds/LinuxServer/Pong.x86_64 (+ Pong_Data). The Dockerfile at the project root copies that folder.
// Needs the "Linux Dedicated Server Build Support" module in the Hub.
// Scripting backend: IL2CPP. Mono's JIT aborts under x86 emulation (Rosetta and QEMU) on Apple Silicon Docker
// with "Assertion at x86-codegen.h:410", so the local docker run test only works with IL2CPP. Needs the
// com.unity.toolchain.macos-arm64-linux (Mac) or com.unity.toolchain.win-x86_64-linux-x86_64 (Windows) package.
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
            subtarget        = (int)StandaloneBuildSubtarget.Server,   // THE line: Dedicated Server, not a windowed player
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
