#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EdgegapDockerPath
{
    static EdgegapDockerPath()
    {
        string home = Environment.GetEnvironmentVariable("HOME") ?? "";
        string[] candidates =
        {
            Path.Combine(home, ".docker", "bin"),
            "/usr/local/bin",
            "/opt/homebrew/bin",
            "/Applications/Docker.app/Contents/Resources/bin"
        };

        string current = Environment.GetEnvironmentVariable("PATH") ?? "";
        var parts = current.Split(':').ToList();
        var missing = candidates.Where(dir => Directory.Exists(dir) && !parts.Contains(dir)).ToList();
        if (missing.Count == 0) return;

        Environment.SetEnvironmentVariable("PATH", string.Join(":", missing.Concat(parts)));

        bool dockerFound = Environment.GetEnvironmentVariable("PATH")
            .Split(':').Any(dir => File.Exists(Path.Combine(dir, "docker")));
        if (!dockerFound)
            Debug.LogWarning("[EdgegapDockerPath] docker CLI still not on PATH. Is Docker Desktop installed?");
    }
}
#endif
