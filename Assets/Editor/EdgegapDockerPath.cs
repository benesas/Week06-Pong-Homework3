// Pong — makes the Edgegap plugin find Docker on a Mac (Week 4, edge).
// Symptom in the Edgegap window: "Containerization Error ... Couldn't complete containerization",
// and in the Console just before it: "/bin/bash: docker: command not found".
// Cause: the Edgegap plugin starts docker through /bin/bash -c with the Unity Editor's PATH plus /usr/local/bin.
// Docker Desktop installed in "user" mode puts the CLI only in ~/.docker/bin, which is in neither place.
// Fix: prepend the Docker CLI folders to the Editor process PATH when the editor loads. The plugin reads
// PATH with Environment.GetEnvironmentVariable, so it picks this up. No sudo and no package edit needed.
// Machine-wide alternative: Docker Desktop > Settings > Advanced > "System" install (writes /usr/local/bin symlinks).
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
            Path.Combine(home, ".docker", "bin"),        // Docker Desktop, "user" install (this Mac)
            "/usr/local/bin",                             // Docker Desktop, "system" install; Intel Homebrew
            "/opt/homebrew/bin",                          // Apple Silicon Homebrew (docker CLI via brew)
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
