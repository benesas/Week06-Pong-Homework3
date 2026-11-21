using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class Week06CloudProject
{
    public const string ProjectId = "4ecf72e7-6c1c-475f-b5a2-b6a17a4ff17f";

    static Week06CloudProject()
    {
        EditorApplication.delayCall += ApplyIfUnlinked;
    }

    public static void ApplyIfUnlinked()
    {
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        string current = settings.FindProperty("cloudProjectId").stringValue;
        if (string.IsNullOrEmpty(current)) Apply();
    }

    [MenuItem("Week06/Link to pong Unity Cloud project")]
    public static void Apply()
    {
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        settings.FindProperty("cloudProjectId").stringValue = ProjectId;
        settings.FindProperty("projectName").stringValue = "pong";
        settings.FindProperty("organizationId").stringValue = "18968333733802";
        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("[Week06] Linked Unity Cloud project pong: " + ProjectId);
    }
}
