using System;
using System.IO;
using FishNet.Managing;
using FishNet.Managing.Transporting;
#if WEEK06_RELAY_READY
using FishNet.Transporting.UTP;
#endif
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class Week06Setup
{
    [MenuItem("Week06/Prepare and validate project")]
    public static void Prepare()
    {
#if !WEEK06_RELAY_READY
        Debug.LogWarning("[Week06] Önce Week06 > Install required Relay packages seç ve derlemenin bitmesini bekle.");
        return;
#else
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Pong.unity");
        var manager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
        if (manager == null) throw new Exception("NetworkManager missing");
        var transport = manager.GetComponent<UnityTransport>();
        if (transport == null) transport = manager.gameObject.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", 7770, "0.0.0.0");
        var tm = manager.GetComponent<TransportManager>();
        if (tm == null) tm = manager.gameObject.AddComponent<TransportManager>();
        tm.Transport = transport;
        EditorUtility.SetDirty(manager.gameObject);
        EditorUtility.SetDirty(transport);
        EditorUtility.SetDirty(tm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Pong.unity", true) };
        PlayerSettings.companyName = "Student";
        PlayerSettings.productName = "Week06 Pong";
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var input = ps.FindProperty("activeInputHandler");
        if (input != null) { input.intValue = 2; ps.ApplyModifiedPropertiesWithoutUndo(); }

        Directory.CreateDirectory("Assets/Settings");
        const string rendererPath = "Assets/Settings/PongRenderer.asset";
        const string pipelinePath = "Assets/Settings/PongPipeline.asset";
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null) { renderer = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(renderer, rendererPath); }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null) { pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>(); AssetDatabase.CreateAsset(pipeline, pipelinePath); }
        var so = new SerializedObject(pipeline);
        var list = so.FindProperty("m_RendererDataList");
        list.arraySize = 1; list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        so.ApplyModifiedPropertiesWithoutUndo();
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        AssetDatabase.SaveAssets();
        if (tm.Transport != transport || transport.GetPort() != 7770) throw new Exception("Transport validation failed");
        Debug.Log("[Week06] Scene and transport validated: UnityTransport, UDP 7770, server-authoritative paddle.");
#endif
    }

    public static void PrepareAndBuild()
    {
#if !WEEK06_RELAY_READY
        throw new InvalidOperationException("Önce Week06 > Install required Relay packages seç ve derlemeyi bekle.");
#else
        Prepare();
        string path = Path.GetFullPath("Builds/Windows/Pong.exe");
        string[] args = Environment.GetCommandLineArgs();
        int output = Array.IndexOf(args, "-week06-build-output");
        if (output >= 0 && output + 1 < args.Length) path = Path.GetFullPath(args[output + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/Pong.unity" }, locationPathName = path,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("Windows build failed: " + report.summary.result);
        Debug.Log("[Week06] Windows build succeeded: " + path);
#endif
    }

    [MenuItem("Week06/Export complete unitypackage")]
    public static void Export()
    {
        AssetDatabase.Refresh();
        string path = Path.GetFullPath("Week06_Pong_Tamamlanmis.unitypackage");
        string[] args = Environment.GetCommandLineArgs();
        int output = Array.IndexOf(args, "-week06-package-output");
        if (output >= 0 && output + 1 < args.Length) path = Path.GetFullPath(args[output + 1]);
        AssetDatabase.ExportPackage("Assets", path, ExportPackageOptions.Recurse);
        Debug.Log("[Week06] Package exported: " + path);
    }
}
