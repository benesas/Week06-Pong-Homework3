using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

[InitializeOnLoad]
public static class Week06Dependencies
{
    private const string Symbol = "WEEK06_RELAY_READY";
    private static AddAndRemoveRequest _request;
    private static readonly Dictionary<string, string> Required = new Dictionary<string, string>
    {
        { "com.unity.services.multiplayer", "com.unity.services.multiplayer@1.1.8" },
        { "com.alven.fishnet.unitytransport", "https://github.com/ooonush/FishyUnityTransport.git?path=Assets/FishNet/Plugins/FishyUnityTransport#08756d9733a556018041e52fcb9d2a6035346aeb" }
    };

    static Week06Dependencies()
    {
        EditorApplication.delayCall += CheckInstalled;
    }

    private static void CheckInstalled()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        var installed = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
        bool ready = Required.Keys.All(name => installed.Any(p => p.name == name));
        if (ready)
        {
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string symbols = PlayerSettings.GetScriptingDefineSymbols(target);
            if (!symbols.Split(';').Contains(Symbol))
            {
                PlayerSettings.SetScriptingDefineSymbols(target, symbols + ";" + Symbol);
                SessionState.SetBool("Week06.ConfigureAfterInstall", true);
                Debug.Log("[Week06] Dependencies ready. Compiling Relay support...");
            }
            else if (SessionState.GetBool("Week06.ConfigureAfterInstall", false))
            {
                SessionState.SetBool("Week06.ConfigureAfterInstall", false);
                typeof(Week06Setup).GetMethod("Prepare").Invoke(null, null);
            }
        }
        else if (!Application.isBatchMode) Install();
    }

    [MenuItem("Week06/Install required Relay packages")]
    public static void Install()
    {
        if (_request != null) return;
        var installed = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
        string[] missing = Required.Where(pair => !installed.Any(p => p.name == pair.Key)).Select(pair => pair.Value).ToArray();
        if (missing.Length == 0) { CheckInstalled(); return; }
        Debug.Log("[Week06] Installing Multiplayer Services and FishyUnityTransport. Internet and Git are required.");
        _request = Client.AddAndRemove(missing);
        SessionState.SetBool("Week06.ConfigureAfterInstall", true);
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (_request == null || !_request.IsCompleted) return;
        bool success = _request.Status == StatusCode.Success;
        if (!success) Debug.LogError("[Week06] Package install failed: " + _request.Error.message);
        _request = null;
        EditorApplication.update -= Poll;
        if (success) EditorApplication.delayCall += CheckInstalled;
    }
}
